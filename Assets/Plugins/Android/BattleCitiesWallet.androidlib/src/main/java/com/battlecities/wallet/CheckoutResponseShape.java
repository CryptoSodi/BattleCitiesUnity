package com.battlecities.wallet;

/** Structural gate only. Unity compares the message against the quote before submission. */
final class CheckoutResponseShape {
    private CheckoutResponseShape() {}

    static boolean isValid(byte[] transaction) {
        return transaction != null && transaction.length > 65 && transaction.length <= 1232 && transaction[0] == 1;
    }
}
