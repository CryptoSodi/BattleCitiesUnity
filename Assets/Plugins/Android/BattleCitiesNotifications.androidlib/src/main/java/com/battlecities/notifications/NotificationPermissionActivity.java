package com.battlecities.notifications;

import android.Manifest;
import android.app.Activity;
import android.os.Build;
import android.os.Bundle;

/** Requests Android 13 permission without replacing Unity's GameActivity. */
public final class NotificationPermissionActivity extends Activity {
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if (Build.VERSION.SDK_INT < 33) { finish(); return; }
        if (state == null) {
            BattleCitiesNotifications.prefs(this).edit().putBoolean(BattleCitiesNotifications.REQUESTED, true).apply();
            requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS}, 4102);
        }
    }
    @Override public void onRequestPermissionsResult(int code, String[] permissions, int[] results) {
        super.onRequestPermissionsResult(code, permissions, results);
        if (code == 4102) finish();
    }
}
