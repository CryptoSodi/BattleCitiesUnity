// Phantom connects the wallet; the API separately verifies its signed challenge.
const APP_ID = '4d0d9b85-042f-477b-93d4-2176fd01d740';

export function createPhantomBridge({ BrowserSDK, AddressType }) {
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
    if (!instance.isConnected() || !isInjected() || !address() || (expected && address() !== expected)) return null;
    await instance.solana.switchNetwork(network);
    return adapter();
  }
  async function connect(targetNetwork = 'mainnet') {
    network = targetNetwork;
    const instance = getSdk();
    if (instance.isConnected() && !isInjected()) await instance.disconnect();
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
    if (!isInjected() || !address()) throw new Error('Phantom did not return a Solana wallet address.');
    await instance.solana.switchNetwork(network);
    return adapter();
  }
  return {
    connect, restore,
    hasCallback: () => false,
    cancelChoice: () => {},
    current: expected => isInjected() && address() && (!expected || address() === expected) ? adapter() : null,
    async disconnect() { if (sdk?.isConnected()) await sdk.disconnect(); }
  };
}
