package com.battlecities.notifications;

public final class NotificationPromptPolicyTest {
    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
    public static void main(String[] args) {
        check(NotificationPromptPolicy.shouldAskOnStartup(false, true, 33), "fresh install asks");
        check(!NotificationPromptPolicy.shouldAskOnStartup(true, true, 33), "existing choice is preserved");
        check(!NotificationPromptPolicy.shouldAskOnStartup(false, false, 33), "unsupported device does not ask");
        check(!NotificationPromptPolicy.shouldAskOnStartup(false, true, 32), "older Android does not request runtime permission");
        check(NotificationPromptPolicy.canRequestAgain(33, false, false), "initial or dismissed prompt can be requested");
        check(NotificationPromptPolicy.canRequestAgain(33, true, true), "Android permits another request after denial");
        check(!NotificationPromptPolicy.canRequestAgain(33, true, false), "blocked request opens system settings instead");
        check(!NotificationPromptPolicy.canRequestAgain(32, false, false), "older Android uses settings");
        System.out.println("Notification prompt policy checks passed.");
    }
}
