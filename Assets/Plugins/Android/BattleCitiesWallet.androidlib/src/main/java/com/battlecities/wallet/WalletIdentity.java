package com.battlecities.wallet;

import android.net.Uri;

/** MWA icon paths are resolved relative to the dapp identity URI. */
final class WalletIdentity {
    static final Uri URI = Uri.parse("https://battlecities.com");
    static final Uri ICON = Uri.parse("images/battle-cities-wallet-bc-20261015.png");
    static final String NAME = "Battle Cities";
    static final int ASSOCIATION_REQUEST = 7431;
    static String chainForApi(String api) {
        if ("https://api.battlecities.com".equals(api)) return "solana:mainnet";
        if ("https://test.battlecities.com".equals(api)) return "solana:devnet";
        throw new IllegalArgumentException("Unexpected wallet API origin");
    }
    private WalletIdentity() { }
}
