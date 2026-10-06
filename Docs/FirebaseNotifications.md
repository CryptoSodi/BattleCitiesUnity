# Unity Firebase notifications

The Android Unity client uses Firebase Cloud Messaging directly through
`BattleCitiesNotifications.androidlib`. It does not require the old Capacitor app.

- Android package: `com.battlecities.solanaconsole`.
- Firebase project: `battle-cities-84acd`.
- Firebase Android app: `1:295482350041:android:8bc8bd10a22300e1fce1c8`.
- Client configuration: `Assets/BattleCities/Notifications/google-services.json`.
- Notification channel: `battle-cities-notifications`.
- Device registration: `POST https://api.battlecities.com/api/notifications/devices`.

## Player controls and registration

Android Settings has an Enable Alerts / Disable Alerts action in the shared white
footer. Notifications are off until the player enables them. Android 13 and later
request `POST_NOTIFICATIONS` when enabling. After a denied request, enabling again
opens the app's Android notification settings. Devices without working Google Play
services show notifications as unavailable.

The persistent Unity controller obtains/caches the FCM token, checks permission on
resume and every minute while foregrounded, and registers it with the existing
API. The registration payload is `{token, platform:"android", permission}`;
`permission` is granted only when the local preference and Android permission both
allow delivery. Wallet ownership comes from the same session cookie used by the
existing Unity API client, without Firebase Authentication or a supplied player ID.
Changed accounts and rotated tokens resynchronize. Failed requests retry after
30 seconds. After logout succeeds, outstanding registrations finish before an
anonymous registration removes the previous account association. If this update
fails, the controller retains it for retry.

Opting out cancels currently displayed notifications, disables FCM automatic
initialization, suppresses data-message rendering, and registers permission denied
with the backend when a token exists. Existing Web Settings and phone pairing are
unchanged.

## Existing sender contract

Reuse the old app's API server / Cloudflare deployment and Firebase HTTP v1 sender.
Send **data-only** messages with these existing data fields:

```json
{
  "title": "Battle Cities",
  "body": "A new event is available.",
  "route": "home",
  "type": "event",
  "imageUrl": "https://example.com/event.png",
  "externalUrl": "https://battlecities.com/",
  "actionLabel": "Open"
}
```

The native service shows data messages in the foreground and background. Optional
images enrich an immediately displayed text notification through WorkManager;
downloads are HTTPS, limited to 2 MiB, sampled to 1024 pixels, and cannot restore
a notification the player dismissed or disabled. Notification-only messages from
Firebase's composer use Firebase's own background tray behavior and do not follow
the data-only routing or local opt-out handler. Use the existing API sender for
production and route testing.

| Route | Result after the player taps the notification |
| --- | --- |
| `home` or unknown | Play tab |
| `play` | Tank selection / battle preparation |
| `shop` | Shop |
| `rewards` | Quarters |
| `social` | Socials |
| `external` | Valid HTTPS link, opened by the native tap activity |
| `share` | Android share sheet for Battle Cities |

Internal navigation stays queued through Login or an active battle until the main
menu is available. It never signs in, claims rewards, purchases items, or starts a
match automatically. Acknowledgement removes only the pending payload that was
handled, preserving a newer tap that arrived meanwhile.

## Android configuration and validation

`FirebaseAndroidConfiguration` validates the client package and generates Android
string resources from the downloaded public client configuration before Android
builds. Missing or mismatched configuration fails the build. Server service-account
keys stay on the backend and must never be added to Unity.

The native library pins Firebase BoM 33.10.0, matching the existing Android client,
with dependencies that compile against Unity's installed Android API 36 toolchain.
The Firebase Unity C++ SDK and analytics are not needed for this Android messaging
bridge. Unity's existing GameActivity and wallet integration remain in place.

Run **Battle Cities > Notifications > Run isolated Firebase checks** for the local
registration fixtures. These use an API override and never send fake tokens or
messages to production. The current integration passes 25 checks covering route
validation, local/OS opt-out, token rotation, request contract, retries, and stale
account responses. Another 24 footer checks verified captions, status messages,
and clean lettering at 360, 620, and 840-unit content widths. The native Java
library compiles and passes Android lint with the installed Unity Gradle/Android
toolchain (zero lint errors; dependency-version and durable-write warnings).
Full delivery and cold/warm notification-tap verification
must be performed on an Android device using this integration in a game APK.

Official references: [Android FCM setup](https://firebase.google.com/docs/cloud-messaging/android/get-started),
[receiving messages](https://firebase.google.com/docs/cloud-messaging/android/receive-messages).
