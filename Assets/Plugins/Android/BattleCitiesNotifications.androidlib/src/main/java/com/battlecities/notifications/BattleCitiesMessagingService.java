package com.battlecities.notifications;

import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import androidx.core.app.NotificationCompat;
import com.google.firebase.messaging.FirebaseMessagingService;
import com.google.firebase.messaging.RemoteMessage;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.Map;
import org.json.JSONObject;
import androidx.work.Data;
import androidx.work.OneTimeWorkRequest;
import androidx.work.WorkManager;

/** The existing API sends data-only FCM messages, including rich image/action fields. */
public final class BattleCitiesMessagingService extends FirebaseMessagingService {
    @Override public void onNewToken(String token) {
        BattleCitiesNotifications.prefs(this).edit().putString(BattleCitiesNotifications.TOKEN, token).apply();
        // Unity associates the token with the current session on its next foreground refresh.
    }
    @Override public void onMessageReceived(RemoteMessage message) {
        BattleCitiesNotifications.createChannel(this);
        if (!BattleCitiesNotifications.enabled(this) || !BattleCitiesNotifications.allowed(this)) return;
        Map<String,String> data = message.getData();
        RemoteMessage.Notification standard = message.getNotification();
        String title = value(data, "title", standard != null && standard.getTitle() != null ? standard.getTitle() : "Battle Cities");
        String body = value(data, "body", standard != null && standard.getBody() != null ? standard.getBody() : "You have a new update.");
        String route = value(data, "route", "home");
        JSONObject payload = new JSONObject();
        try {
            payload.put("route", route); payload.put("type", value(data, "type", "announcement"));
            payload.put("externalUrl", value(data, "externalUrl", ""));
            payload.put("id", message.getMessageId() == null ? Long.toString(System.currentTimeMillis()) : message.getMessageId());
        } catch (Exception ignored) { return; }
        int id = payload.optString("id").hashCode();
        Intent tap = new Intent(this, NotificationTapActivity.class).putExtra("battlecities_notification", payload.toString());
        PendingIntent pending = PendingIntent.getActivity(this, id, tap, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        NotificationCompat.Builder notification = new NotificationCompat.Builder(this, BattleCitiesNotifications.CHANNEL)
            .setSmallIcon(R.drawable.battlecities_notification).setColor(0xFF087DCE)
            .setContentTitle(title).setContentText(body).setStyle(new NotificationCompat.BigTextStyle().bigText(body))
            .setAutoCancel(true).setContentIntent(pending).setPriority(NotificationCompat.PRIORITY_DEFAULT);
        String action = value(data, "actionLabel", "");
        if (!action.isEmpty()) notification.addAction(0, action, pending);
        getSystemService(NotificationManager.class).notify(id, notification.build());
        String imageUrl = value(data, "imageUrl", "");
        if (imageUrl.startsWith("https://")) {
            Data imageData = new Data.Builder().putInt("id", id).putString("payload", payload.toString())
                .putString("title", title).putString("body", body).putString("action", action).putString("imageUrl", imageUrl).build();
            WorkManager.getInstance(this).enqueue(new OneTimeWorkRequest.Builder(NotificationImageWorker.class).setInputData(imageData).build());
        }
    }
    private static String value(Map<String,String> data, String key, String fallback) {
        String value = data.get(key); return value == null || value.trim().isEmpty() ? fallback : value;
    }
    static Bitmap download(String url) {
        if (url == null || !url.startsWith("https://")) return null;
        HttpURLConnection connection = null;
        try {
            connection = (HttpURLConnection)new URL(url).openConnection();
            connection.setConnectTimeout(3500); connection.setReadTimeout(3500); connection.setInstanceFollowRedirects(false);
            if (connection.getResponseCode() != 200 || connection.getContentLengthLong() > 2 * 1024 * 1024) return null;
            try (java.io.InputStream stream = connection.getInputStream()) {
                java.io.ByteArrayOutputStream bytes = new java.io.ByteArrayOutputStream();
                byte[] buffer = new byte[8192]; int count;
                while ((count = stream.read(buffer)) != -1) {
                    if (bytes.size() + count > 2 * 1024 * 1024) return null;
                    bytes.write(buffer, 0, count);
                }
                byte[] contents = bytes.toByteArray();
                BitmapFactory.Options options = new BitmapFactory.Options(); options.inJustDecodeBounds = true;
                BitmapFactory.decodeByteArray(contents, 0, contents.length, options);
                options.inSampleSize = 1;
                while (options.outWidth / options.inSampleSize > 1024 || options.outHeight / options.inSampleSize > 1024) options.inSampleSize *= 2;
                options.inJustDecodeBounds = false;
                return BitmapFactory.decodeByteArray(contents, 0, contents.length, options);
            }
        } catch (Exception ignored) { return null; }
        finally { if (connection != null) connection.disconnect(); }
    }
}
