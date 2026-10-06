package com.battlecities.notifications;

import android.Manifest;
import android.app.Activity;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import androidx.core.app.NotificationManagerCompat;
import androidx.core.content.ContextCompat;
import com.google.android.gms.common.ConnectionResult;
import com.google.android.gms.common.GoogleApiAvailability;
import com.google.firebase.FirebaseApp;
import com.google.firebase.messaging.FirebaseMessaging;
import org.json.JSONObject;

/** JNI boundary only: no Unity or Capacitor dependency, and no server credentials. */
public final class BattleCitiesNotifications {
    public interface Callback { void onComplete(String json); }
    static final String CHANNEL = "battle-cities-notifications";
    static final String PREFS = "battle_cities_notifications";
    static final String ENABLED = "notifications_enabled";
    static final String REQUESTED = "requested_permission";
    static final String TOKEN = "fcm_token";
    static final String PENDING = "pending_notification";
    private BattleCitiesNotifications() {}

    static SharedPreferences prefs(Context context) { return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE); }
    static boolean enabled(Context context) { return prefs(context).getBoolean(ENABLED, false); }
    static void createChannel(Context context) {
        NotificationChannel channel = new NotificationChannel(CHANNEL, "Battle Cities", NotificationManager.IMPORTANCE_DEFAULT);
        channel.setDescription("Battle Cities announcements, rewards and events");
        context.getSystemService(NotificationManager.class).createNotificationChannel(channel);
    }
    static boolean allowed(Context context) {
        if (Build.VERSION.SDK_INT >= 33 && ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) return false;
        NotificationChannel channel = context.getSystemService(NotificationManager.class).getNotificationChannel(CHANNEL);
        return NotificationManagerCompat.from(context).areNotificationsEnabled() && (channel == null || channel.getImportance() != NotificationManager.IMPORTANCE_NONE);
    }
    private static boolean supported(Context context) {
        if (GoogleApiAvailability.getInstance().isGooglePlayServicesAvailable(context) != ConnectionResult.SUCCESS) return false;
        try { return !FirebaseApp.getApps(context).isEmpty() || FirebaseApp.initializeApp(context) != null; }
        catch (RuntimeException ignored) { return false; }
    }
    private static String permission(Context context) {
        if (!enabled(context)) return "denied";
        if (allowed(context)) return "granted";
        if (Build.VERSION.SDK_INT >= 33 && !prefs(context).getBoolean(REQUESTED, false)) return "prompt";
        return "denied";
    }
    private static void complete(Context context, Callback callback, boolean available, String error) {
        try {
            JSONObject json = new JSONObject();
            json.put("supported", available);
            json.put("enabled", enabled(context));
            json.put("permission", available ? permission(context) : "unavailable");
            json.put("token", prefs(context).getString(TOKEN, ""));
            json.put("error", error == null ? "" : error);
            callback.onComplete(json.toString());
        } catch (Exception ignored) { /* Do not log the registration token. */ }
    }
    public static void refresh(Activity activity, Callback callback) {
        Context context = activity.getApplicationContext();
        createChannel(context);
        boolean available = supported(context);
        if (!available || !enabled(context)) { complete(context, callback, available, null); return; }
        FirebaseMessaging messaging = FirebaseMessaging.getInstance();
        messaging.setAutoInitEnabled(true);
        messaging.getToken().addOnCompleteListener(task -> {
            if (task.isSuccessful() && task.getResult() != null) prefs(context).edit().putString(TOKEN, task.getResult()).apply();
            complete(context, callback, true, task.isSuccessful() ? null : "Could not register notifications. Please retry.");
        });
    }
    public static void setEnabled(Activity activity, boolean value, Callback callback) {
        Context context = activity.getApplicationContext();
        prefs(context).edit().putBoolean(ENABLED, value).apply();
        createChannel(context);
        if (!value) {
            context.getSystemService(NotificationManager.class).cancelAll();
            if (supported(context)) FirebaseMessaging.getInstance().setAutoInitEnabled(false);
        }
        activity.runOnUiThread(() -> {
            if (value && supported(context) && !allowed(context)) {
                if (Build.VERSION.SDK_INT >= 33 && !prefs(context).getBoolean(REQUESTED, false)) {
                    activity.startActivity(new Intent(activity, NotificationPermissionActivity.class));
                } else {
                    Intent settings = new Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(Settings.EXTRA_APP_PACKAGE, activity.getPackageName());
                    activity.startActivity(settings);
                }
            }
            refresh(activity, callback);
        });
    }
    public static String peekPending(Activity activity) { return prefs(activity).getString(PENDING, ""); }
    public static void acknowledgePending(Activity activity, String value) {
        SharedPreferences state = prefs(activity);
        if (value != null && value.equals(state.getString(PENDING, ""))) state.edit().remove(PENDING).apply();
    }
}
