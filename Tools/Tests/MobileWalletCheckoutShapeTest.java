package com.battlecities.wallet;

public final class MobileWalletCheckoutShapeTest {
    private static void check(boolean result, String description) {
        if (!result) throw new AssertionError(description);
    }
    public static void main(String[] args) {
        check(!CheckoutResponseShape.isValid(null), "null response is rejected");
        check(!CheckoutResponseShape.isValid(new byte[65]), "truncated response is rejected");
        check(!CheckoutResponseShape.isValid(new byte[1233]), "oversized response is rejected");
        byte[] result = new byte[100];
        check(!CheckoutResponseShape.isValid(result), "unexpected signature count is rejected");
        result[0] = 1;
        check(CheckoutResponseShape.isValid(result), "a correctly shaped response reaches Unity for quote validation");
        byte[] longer = new byte[200]; longer[0] = 1;
        check(CheckoutResponseShape.isValid(longer), "wallet-added assertions can reach Unity for quote validation");
        System.out.println("Mobile wallet response shape checks passed.");
    }
}
