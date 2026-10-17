package com.battlecities.wallet;

import android.content.Context;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.AtomicFile;
import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.util.Arrays;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** Device-bound encrypted game session; never stores wallet keys or payment authority. */
public final class WalletSessionStore {
    private static final String ALIAS = "battlecities.game-session.v1";
    private static AtomicFile file(Context context) {
        return new AtomicFile(new File(context.getNoBackupFilesDir(), "wallet-session-v1"));
    }
    private static SecretKey key(boolean create) throws Exception {
        KeyStore store = KeyStore.getInstance("AndroidKeyStore");
        store.load(null);
        if (store.containsAlias(ALIAS)) return (SecretKey)store.getKey(ALIAS, null);
        if (!create) return null;
        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
        generator.init(new KeyGenParameterSpec.Builder(ALIAS,
            KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
            .setKeySize(256).build());
        return generator.generateKey();
    }
    public static synchronized String load(Context context) {
        try {
            AtomicFile saved = file(context);
            if (!saved.getBaseFile().exists()) return "";
            if (saved.getBaseFile().length() > 16384) { clear(context); return ""; }
            byte[] bytes = saved.readFully();
            SecretKey secret = key(false);
            if (secret == null || bytes.length < 29) { clear(context); return ""; }
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, secret, new GCMParameterSpec(128, Arrays.copyOf(bytes, 12)));
            cipher.updateAAD(ALIAS.getBytes(StandardCharsets.UTF_8));
            return new String(cipher.doFinal(Arrays.copyOfRange(bytes, 12, bytes.length)), StandardCharsets.UTF_8);
        } catch (Exception ignored) { clear(context); return ""; }
    }
    public static synchronized boolean save(Context context, String value) {
        AtomicFile saved = file(context);
        FileOutputStream output = null;
        try {
            byte[] plain = value.getBytes(StandardCharsets.UTF_8);
            if (plain.length == 0 || plain.length > 12000) return false;
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, key(true));
            cipher.updateAAD(ALIAS.getBytes(StandardCharsets.UTF_8));
            output = saved.startWrite();
            output.write(cipher.getIV());
            output.write(cipher.doFinal(plain));
            saved.finishWrite(output);
            return value.equals(load(context));
        } catch (Exception ignored) { if (output != null) saved.failWrite(output); return false; }
    }
    public static synchronized void clear(Context context) {
        file(context).delete();
        try { KeyStore store = KeyStore.getInstance("AndroidKeyStore"); store.load(null); store.deleteEntry(ALIAS); }
        catch (Exception ignored) { }
    }
}
