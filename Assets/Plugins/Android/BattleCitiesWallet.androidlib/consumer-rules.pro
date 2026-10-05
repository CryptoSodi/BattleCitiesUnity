# Unity calls this bridge through JNI, and implements Callback through AndroidJavaProxy.
-keep class com.battlecities.wallet.MobileWalletLogin { public *; }
-keep class com.battlecities.wallet.MobileWalletLogin$Operation { public *; }
-keep interface com.battlecities.wallet.MobileWalletLogin$Callback { *; }
