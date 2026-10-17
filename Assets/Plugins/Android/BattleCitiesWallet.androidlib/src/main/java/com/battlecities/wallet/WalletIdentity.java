package com.battlecities.wallet;

import android.net.Uri;

/** MWA icon paths are resolved relative to the dapp identity URI. */
final class WalletIdentity {
    static final Uri URI = Uri.parse("https://battlecities.com");
    static final Uri ICON = Uri.parse("images/battle-cities-wallet-bc-20261015.png");
    static final String NAME = "Battle Cities";
    static final int ASSOCIATION_REQUEST = 7431;
    private WalletIdentity() { }
}
