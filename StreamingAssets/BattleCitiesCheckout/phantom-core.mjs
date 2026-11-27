// Phantom connects the wallet; the API separately verifies its signed challenge.
const APP_ID = '4d0d9b85-042f-477b-93d4-2176fd01d740';

export function createPhantomBridge({ BrowserSDK, AddressType, nativeProvider = () =>
  typeof window === 'undefined' ? null : window.phantom?.solana || window.solana }) {
  let sdk, network = 'mainnet';
  const getSdk = () => sdk || (sdk = new BrowserSDK({
    providers: ['injected'],
    addressTypes: [AddressType.solana],
    appId: APP_ID
  }));
  const address = () => {
    if (!sdk?.isConnected()) return null;
    const key = sdk.solana.publicKey;
    return typeof key === 'string' ? key : key?.toString() || null;
  };
  const isInjected = () => sdk?.getCurrentProviderInfo()?.type === 'injected';
  const ready = expected => {
    const key = address();
    if (!isInjected() || !key || (expected && key !== expected)) return false;
    // The SDK can retain the last account while the extension is locked. When
    // Phantom exposes its native provider, confirm that it still has that key.
    const native = nativeProvider();
    if (native?.isPhantom && (native.isConnected === false || !native.publicKey
        || native.publicKey.toString() !== key)) return false;
    return true;
  };
  const adapter = () => ({
    isPhantom: true,
    get publicKey() { const key = address(); return key ? { toString: () => key } : null; },
    signMessage: bytes => getSdk().solana.signMessage(bytes),
    signTransaction: transaction => getSdk().solana.signTransaction(transaction),
    on: (event, listener) => getSdk().solana.on(event, listener),
    removeListener: (event, listener) => getSdk().solana.off(event, listener)
  });
  async function restore(expected, targetNetwork = 'mainnet') {
    network = targetNetwork;
    const instance = getSdk();
    if (!instance.isConnected()) await instance.autoConnect();
    if (!ready(expected)) return null;
    await instance.solana.switchNetwork(network);
    return adapter();
  }
  async function connect(targetNetwork = 'mainnet') {
    network = targetNetwork;
    let instance = getSdk();
    if (instance.isConnected() && !ready()) {
      try { await instance.disconnect(); }
      catch { sdk = null; instance = getSdk(); }
    }
    if (!instance.isConnected()) {
      // The SDK starts discovery in the background during construction. Connecting
      // before its registry is populated fails on the first click.
      await instance.discoverWallets();
      try { await instance.connect({ provider: 'injected', walletId: 'phantom' }); }
      catch (error) {
        if (error?.message === 'Unknown injected wallet id: phantom')
          throw new Error('Phantom wallet was not found. Install or enable Phantom, reload this page, and try again.');
        throw error;
      }
    }
    if (!ready()) throw new Error('Unlock Phantom and connect your wallet to continue.');
    await instance.solana.switchNetwork(network);
    return adapter();
  }
  return {
    connect, restore,
    hasCallback: () => false,
    cancelChoice: () => {},
    ready,
    current: expected => ready(expected) ? adapter() : null,
    async disconnect() { if (sdk?.isConnected()) await sdk.disconnect(); }
  };
}
