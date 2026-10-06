using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class SettingsScreen
    {
        Button notifications;
        TMP_Text notificationsCaption;
        SettingsControlVisual notificationsVisual;
        PushNotifications push;

        void ConfigureNotifications()
        {
            notifications = Control("Notifications", footer, "ENABLE ALERTS", ToggleNotifications, true);
            notificationsCaption = notifications.transform.Find("Caption").GetComponent<TMP_Text>();
            notificationsVisual = notifications.GetComponent<SettingsControlVisual>();
            notifications.gameObject.SetActive(PushNotifications.IsNativeAndroid);
            if (PushNotifications.IsNativeAndroid) push = PushNotifications.Ensure();
            LayoutNotifications();
        }
        void LayoutNotifications()
        {
            if (!notifications) return;
            TvStatusFooterLayout.PlaceAction((RectTransform)notifications.transform);
            Fit(version.rectTransform, notifications.gameObject.activeSelf ? new Rect(.03f,.13f,.71f,.74f) : new Rect(.03f,.13f,.94f,.74f));
        }
        void ToggleNotifications() { if (push) { push.Toggle(); RefreshNotifications(); } }
        void RefreshNotifications()
        {
            if (!notifications || !notifications.gameObject.activeSelf) return;
            version.text = "VERSION " + Application.version + " • " + (push ? push.Status : "Checking notifications...");
            notificationsCaption.text = push && push.Enabled ? "DISABLE ALERTS" : "ENABLE ALERTS";
            bool available = push && push.Supported;
            bool changed = notifications.interactable != available;
            notifications.interactable = available;
            notificationsVisual.SetActive(push && push.Receiving);
            if (changed) Psg1UiNavigation.Rows(new Selectable[]{back},new Selectable[]{rowButtons[0]},new Selectable[]{rowButtons[1]},new Selectable[]{logout},new Selectable[]{pair},new Selectable[]{notifications});
        }
    }
}
