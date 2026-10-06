package com.battlecities.notifications;

import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.graphics.Bitmap;
import android.service.notification.StatusBarNotification;
import androidx.core.app.NotificationCompat;
import androidx.work.Worker;
import androidx.work.WorkerParameters;

/** Enrich an already displayed notification outside FCM's short execution window. */
public final class NotificationImageWorker extends Worker {
    public NotificationImageWorker(Context context, WorkerParameters parameters) { super(context, parameters); }
    @Override public Result doWork() {
        Context context = getApplicationContext();
        if (!BattleCitiesNotifications.enabled(context) || !BattleCitiesNotifications.allowed(context)) return Result.success();
        int id = getInputData().getInt("id", 0);
        NotificationManager manager = context.getSystemService(NotificationManager.class);
        if (!visible(manager, id)) return Result.success();
        Bitmap image = BattleCitiesMessagingService.download(getInputData().getString("imageUrl"));
        if (image == null || !visible(manager, id) || !BattleCitiesNotifications.enabled(context) || !BattleCitiesNotifications.allowed(context)) return Result.success();
        Intent tap = new Intent(context, NotificationTapActivity.class).putExtra("battlecities_notification", getInputData().getString("payload"));
        PendingIntent pending = PendingIntent.getActivity(context, id, tap, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
        String body = getInputData().getString("body");
        NotificationCompat.Builder notification = new NotificationCompat.Builder(context, BattleCitiesNotifications.CHANNEL)
            .setSmallIcon(R.drawable.battlecities_notification).setColor(0xFF087DCE).setContentTitle(getInputData().getString("title"))
            .setContentText(body).setLargeIcon(image).setStyle(new NotificationCompat.BigPictureStyle().bigPicture(image).setSummaryText(body))
            .setAutoCancel(true).setContentIntent(pending).setOnlyAlertOnce(true);
        String action = getInputData().getString("action");
        if (action != null && !action.isEmpty()) notification.addAction(0, action, pending);
        manager.notify(id, notification.build());
        return Result.success();
    }
    private static boolean visible(NotificationManager manager, int id) {
        for (StatusBarNotification notification : manager.getActiveNotifications()) if (notification.getId() == id) return true;
        return false;
    }
}
