package com.battlecities.wallet;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.net.Uri;
import android.os.SystemClock;
import android.util.Base64;
import com.solana.mobilewalletadapter.clientlib.protocol.MobileWalletAdapterClient;
import com.solana.mobilewalletadapter.clientlib.scenario.LocalAssociationIntentCreator;
import com.solana.mobilewalletadapter.clientlib.scenario.LocalAssociationScenario;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.math.BigInteger;
import java.net.HttpCookie;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CancellationException;
import java.util.concurrent.FutureTask;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.atomic.AtomicBoolean;
import org.json.JSONObject;

/** Native MWA and HTTP work continues while Android pauses the Unity player. */
public final class MobileWalletLogin {
    private static final String JUPITER_PACKAGE = "ag.jup.jupiter.android";
    private static final String ALPHABET = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private MobileWalletLogin() { }

    public interface Callback { void onComplete(String json); }

    public static Operation connect(Activity activity, String apiBaseUrl, boolean psg1,
                                    String attemptId, Callback callback) {
        Operation operation = new Operation(activity, apiBaseUrl, psg1, attemptId, callback);
        operation.start();
        return operation;
    }

    public static final class Operation {
        private final Activity activity;
        private final String apiBaseUrl;
        private final boolean psg1;
        private final String attemptId;
        private final Callback callback;
        private final AtomicBoolean completed = new AtomicBoolean();
        private final long deadline = SystemClock.elapsedRealtime() + 180000;
        private volatile LocalAssociationScenario scenario;
        private volatile HttpURLConnection connection;
        private volatile Thread worker;
        private String sessionCookie;

        private Operation(Activity activity, String apiBaseUrl, boolean psg1,
                          String attemptId, Callback callback) {
            this.activity = activity;
            this.apiBaseUrl = apiBaseUrl.replaceAll("/+$", "");
            this.psg1 = psg1;
            this.attemptId = attemptId;
            this.callback = callback;
        }

        private void start() {
            worker = new Thread(this::authenticate, "BattleCities-WalletLogin");
            worker.start();
        }

        public void cancel() {
            finish(false, "Wallet sign-in cancelled.", null);
            closeWallet();
            HttpURLConnection active = connection;
            if (active != null) active.disconnect();
            Thread running = worker;
            if (running != null) running.interrupt();
        }

        private long remaining() throws TimeoutException {
            if (completed.get() || Thread.currentThread().isInterrupted()) throw new CancellationException();
            long value = deadline - SystemClock.elapsedRealtime();
            if (value <= 0) throw new TimeoutException();
            return value;
        }

        private void authenticate() {
            try {
                URL api = new URL(apiBaseUrl);
                if (!"https".equals(api.getProtocol()) || api.getUserInfo() != null)
                    throw new LoginException("Wallet sign-in requires an HTTPS API URL.");
                remaining();
                scenario = new LocalAssociationScenario(120000);
                Intent intent = LocalAssociationIntentCreator.createAssociationIntent(
                    null, scenario.getPort(), scenario.getSession());
                if (psg1) intent.setPackage(JUPITER_PACKAGE);
                if (intent.resolveActivity(activity.getPackageManager()) == null)
                    throw new LoginException(psg1
                        ? "Install or update Jupiter Wallet on your PSG1, then try again."
                        : "Install or update a Mobile Wallet Adapter compatible wallet, then try again.");

                // Establish the encrypted association before authorizing the selected account.
                var connected = scenario.start();
                FutureTask<Void> launch = new FutureTask<>(() -> {
                    if (!completed.get()) activity.startActivity(intent);
                    return null;
                });
                activity.runOnUiThread(launch);
                launch.get(Math.min(remaining(), 10000), TimeUnit.MILLISECONDS);
                MobileWalletAdapterClient client = connected.get(Math.min(remaining(), 35000), TimeUnit.MILLISECONDS);
                var authorization = client.authorize(Uri.parse("https://battlecities.com"), null,
                    "Battle Cities", "solana:mainnet", null, null, null, null)
                    .get(Math.min(remaining(), 120000), TimeUnit.MILLISECONDS);
                if (authorization.accounts.length == 0 || authorization.accounts[0].publicKey.length != 32)
                    throw new LoginException("The wallet did not return a Solana account.");
                byte[] publicKey = authorization.accounts[0].publicKey;
                String address = encodeBase58(publicKey);
                JSONObject challenge = request("PUT", "/api/session", new JSONObject().put("walletAddress", address));
                String nonce = challenge.optString("nonce", "");
                String message = challenge.optString("message", "");
                if (!nonce.matches("[a-fA-F0-9]{32}") || message.isEmpty() || message.length() > 4096)
                    throw new LoginException("The server returned an invalid sign-in challenge.");
                byte[] messageBytes = message.getBytes(StandardCharsets.UTF_8);
                var signed = client.signMessagesDetached(new byte[][] { messageBytes }, new byte[][] { publicKey })
                    .get(Math.min(remaining(), 120000), TimeUnit.MILLISECONDS);
                if (signed.messages.length != 1 || signed.messages[0].signatures.length != 1 ||
                    signed.messages[0].signatures[0].length != 64 ||
                    !Arrays.equals(signed.messages[0].message, messageBytes) ||
                    signed.messages[0].addresses.length != 1 ||
                    !Arrays.equals(signed.messages[0].addresses[0], publicKey))
                    throw new LoginException("The wallet returned an invalid signature.");
                closeWallet();
                remaining();
                JSONObject session = request("POST", "/api/session", new JSONObject()
                    .put("provider", "wallet").put("walletAddress", address)
                    .put("nonce", nonce).put("message", message)
                    .put("signature", Base64.encodeToString(signed.messages[0].signatures[0], Base64.NO_WRAP)));
                if (!session.optBoolean("authenticated") || !"wallet".equals(session.optString("provider")) ||
                    sessionCookie == null || sessionCookie.equals("battlecity_session="))
                    throw new LoginException("The API did not establish a wallet session.");
                JSONObject playerBody = request("GET", "/api/player", null);
                JSONObject player = playerBody.optJSONObject("player");
                if (!playerBody.optBoolean("authenticated") || player == null ||
                    !"wallet".equals(player.optString("provider")) ||
                    !address.equals(player.optString("walletAddress")))
                    throw new LoginException("The verified wallet session could not load its player.");
                remaining();
                finish(true, null, player);
            } catch (Exception error) {
                Throwable cause = error;
                while (cause.getCause() != null) cause = cause.getCause();
                String message = cause instanceof LoginException ? cause.getMessage()
                    : cause instanceof TimeoutException ? "Wallet sign-in timed out. Return to the game and try again."
                    : cause instanceof ActivityNotFoundException ? "No compatible wallet could be opened. Install or update your wallet."
                    : cause instanceof CancellationException || cause instanceof InterruptedException ? "Wallet sign-in cancelled."
                    : cause instanceof IOException ? "Could not reach the wallet or Battle Cities API. Check your connection and try again."
                    : "Wallet approval was cancelled or rejected. Please try again.";
                finish(false, message, null);
            } finally {
                closeWallet();
                HttpURLConnection active = connection;
                if (active != null) active.disconnect();
                connection = null;
            }
        }

        private JSONObject request(String method, String path, JSONObject body) throws Exception {
            int timeout = (int)Math.min(remaining(), 15000);
            HttpURLConnection request = (HttpURLConnection)new URL(apiBaseUrl + path).openConnection();
            connection = request;
            try {
                request.setInstanceFollowRedirects(false);
                request.setConnectTimeout(timeout);
                request.setReadTimeout(timeout);
                request.setRequestMethod(method);
                request.setRequestProperty("Accept", "application/json");
                request.setRequestProperty("Cache-Control", "no-store");
                if (sessionCookie != null) request.setRequestProperty("Cookie", sessionCookie);
                if (body != null) {
                    byte[] bytes = body.toString().getBytes(StandardCharsets.UTF_8);
                    request.setDoOutput(true);
                    request.setRequestProperty("Content-Type", "application/json");
                    request.setFixedLengthStreamingMode(bytes.length);
                    try (var output = request.getOutputStream()) { output.write(bytes); }
                }
                int code = request.getResponseCode();
                for (Map.Entry<String, List<String>> header : request.getHeaderFields().entrySet()) {
                    if (!"Set-Cookie".equalsIgnoreCase(header.getKey())) continue;
                    for (String value : header.getValue()) {
                        for (HttpCookie cookie : HttpCookie.parse(value)) {
                            if ("battlecity_session".equals(cookie.getName()))
                                sessionCookie = cookie.getName() + "=" + cookie.getValue();
                        }
                    }
                }
                if (code < 200 || code >= 300)
                    throw new LoginException(code == 401 ? "The API rejected the wallet signature. Please sign in again."
                        : "The Battle Cities API could not complete sign-in (HTTP " + code + ").");
                try (InputStream input = request.getInputStream(); ByteArrayOutputStream output = new ByteArrayOutputStream()) {
                    byte[] buffer = new byte[4096];
                    int count;
                    while ((count = input.read(buffer)) != -1) {
                        remaining();
                        output.write(buffer, 0, count);
                        if (output.size() > 65536) throw new LoginException("The API returned an invalid response.");
                    }
                    return new JSONObject(output.toString(StandardCharsets.UTF_8.name()));
                }
            } finally {
                request.disconnect();
                if (connection == request) connection = null;
            }
        }

        private void closeWallet() {
            LocalAssociationScenario active = scenario;
            scenario = null;
            if (active != null) active.close();
        }

        private void finish(boolean ok, String error, JSONObject player) {
            if (!completed.compareAndSet(false, true)) return;
            try {
                JSONObject response = new JSONObject().put("ok", ok).put("attemptId", attemptId);
                if (ok) response.put("player", player).put("sessionCookie", sessionCookie);
                else response.put("error", error);
                callback.onComplete(response.toString());
            } catch (Exception ignored) {
                // The Unity scene may have been destroyed while the wallet was open.
            }
        }
    }

    static String encodeBase58(byte[] bytes) {
        BigInteger value = new BigInteger(1, bytes);
        StringBuilder result = new StringBuilder();
        BigInteger radix = BigInteger.valueOf(58);
        while (value.signum() > 0) {
            BigInteger[] parts = value.divideAndRemainder(radix);
            result.append(ALPHABET.charAt(parts[1].intValue()));
            value = parts[0];
        }
        for (byte valueByte : bytes) { if (valueByte != 0) break; result.append('1'); }
        return result.reverse().toString();
    }

    private static final class LoginException extends Exception {
        LoginException(String message) { super(message); }
    }
}
