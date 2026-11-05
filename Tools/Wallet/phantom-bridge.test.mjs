import test from 'node:test';
import assert from 'node:assert/strict';
import { createPhantomBridge } from '../../Assets/StreamingAssets/BattleCitiesCheckout/phantom-core.mjs';

const WALLET = '11111111111111111111111111111111';
function fixture({ autoType = 'injected', autoAddress = WALLET } = {}) {
  let mock;
  class FakeSDK {
    constructor(config) {
      mock = this; this.config = config; this.connected = false; this.type = null;
      this.solana = { publicKey: null, switchNetwork: async value => { this.network = value; },
        signMessage: async message => ({ signature: new Uint8Array(64), message }),
        signTransaction: async tx => tx, on() {}, off() {} };
    }
    isConnected() { return this.connected; }
    async connect({ provider }) { this.provider = provider; this.type = 'injected';
      this.connected = true; this.solana.publicKey = WALLET; }
    async autoConnect() { this.type = autoType; this.connected = !!autoAddress; this.solana.publicKey = autoAddress; }
    getCurrentProviderInfo() { return this.type ? { type: this.type } : null; }
    async disconnect() { this.connected = false; this.solana.publicKey = null; }
  }
  const bridge = createPhantomBridge({ BrowserSDK: FakeSDK, AddressType: { solana: 'Solana' } });
  return { bridge, get sdk() { return mock; } };
}

test('connects directly with the injected wallet on the requested network', async () => {
  const f = fixture(); const provider = await f.bridge.connect('devnet');
  assert.equal(f.sdk.config.appId, '4d0d9b85-042f-477b-93d4-2176fd01d740');
  assert.deepEqual(f.sdk.config.providers, ['injected']);
  assert.equal(f.sdk.provider, 'injected'); assert.equal(f.sdk.network, 'devnet');
  assert.equal(provider.publicKey.toString(), WALLET);
  assert.equal(f.bridge.hasCallback(), false);
});

test('restored purchase wallet must match the authenticated account', async () => {
  const f = fixture(); assert.equal(await f.bridge.restore('another-wallet', 'devnet'), null);
  const provider = await f.bridge.restore(WALLET, 'devnet');
  assert.equal(provider.publicKey.toString(), WALLET);
  assert.equal(f.bridge.current('another-wallet'), null); assert.equal(f.sdk.network, 'devnet');
  await f.bridge.disconnect(); assert.equal(f.bridge.current(WALLET), null);
});

test('a previously restored embedded wallet is not used for purchases or sign-in', async () => {
  const f = fixture({ autoType: 'embedded' });
  assert.equal(await f.bridge.restore(WALLET), null);
  assert.equal(f.bridge.current(WALLET), null);
  const provider = await f.bridge.connect();
  assert.equal(f.sdk.provider, 'injected');
  assert.equal(provider.publicKey.toString(), WALLET);
});
