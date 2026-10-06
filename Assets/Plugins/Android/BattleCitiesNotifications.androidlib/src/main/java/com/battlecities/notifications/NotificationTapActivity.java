package com.battlecities.notifications;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import org.json.JSONObject;

/** A system-notification tap queues navigation until the player reaches the menu. */
public final class NotificationTapActivity extends Activity {
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        String payload = getIntent().getStringExtra("battlecities_notification");
        try {
            JSONObject data = new JSONObject(payload == null ? "{}" : payload);
            String route = data.optString("route", "home");
            String url = data.optString("externalUrl", "");
            if ("external".equals(route) && "https".equalsIgnoreCase(Uri.parse(url).getScheme()) && Uri.parse(url).getHost() != null) {
                startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(url))); finish(); return;
            }
            if ("share".equals(route)) {
                Intent share = new Intent(Intent.ACTION_SEND).setType("text/plain").putExtra(Intent.EXTRA_TEXT, "Join me in Battle Cities! https://battlecities.com/");
                startActivity(Intent.createChooser(share, "Share Battle Cities")); finish(); return;
            }
            BattleCitiesNotifications.prefs(this).edit().putString(BattleCitiesNotifications.PENDING, data.toString()).commit();
            Intent launch = getPackageManager().getLaunchIntentForPackage(getPackageName());
            if (launch != null) startActivity(launch.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP));
        } catch (Exception ignored) { /* Invalid payloads cannot crash Unity startup. */ }
        finish();
    }
}
