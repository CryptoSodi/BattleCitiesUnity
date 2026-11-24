package com.battlecities.notifications;

/** Decisions shared by startup and the Settings action; no Android state is changed here. */
final class NotificationPromptPolicy {
    private NotificationPromptPolicy() {}

    static boolean shouldAskOnStartup(boolean hasChoice, boolean supported, int sdk) {
        return !hasChoice && supported && sdk >= 33;
    }

    static boolean canRequestAgain(int sdk, boolean requested, boolean showRationale) {
        return sdk >= 33 && (!requested || showRationale);
    }
}
