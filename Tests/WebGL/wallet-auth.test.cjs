const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const crypto = require('node:crypto');
const source = fs.readFileSync(path.join(__dirname, '../../Assets/Plugins/WebGL/BattleCitiesWallet.jslib'), 'utf8');
const address = '11111111111111111111111111111111';
const nonce = 'ab'.repeat(16);
const message = 'Battle Cities sign-in\nNonce: ' + nonce;
const keys = crypto.generateKeyPairSync('ed25519');
const tick = () => new Promise(resolve => setImmediate(resolve));

function setup(options = {}) {
  const calls = [], replies = [], timers = [], listeners = new Map();
  const provider = {
    isPhantom: true,
    publicKey: { toString: () => address },
    connect: async () => ({ publicKey: provider.publicKey }),
    signMessage: async bytes => ({ signature: new Uint8Array(crypto.sign(null, bytes, keys.privateKey)) }),
    on: (name, fn) => listeners.set(name, fn),
    removeListener: name => listeners.delete(name),
    ...options.provider
  };
  const context = {
    LibraryManager: { library: {} }, mergeInto: Object.assign, UTF8ToString: value => value,
    window: { location: { hostname: 'game.battlecities.com' }, phantom: options.missing ? null : { solana: provider } },
    URL, AbortController, TextEncoder, Uint8Array, Set,
    setTimeout: (fn, ms) => { const timer = { fn, ms, active: true }; timers.push(timer); return timer; },
    clearTimeout: timer => { if (timer) timer.active = false; },
    btoa: value => Buffer.from(value, 'binary').toString('base64'),
    SendMessage: (target, method, json) => replies.push(JSON.parse(json)),
    fetch: async (url, request) => {
      calls.push({ url, ...request });
      assert.equal(request.credentials, 'include');
      assert.equal(request.redirect, 'error');
      let body;
      if (request.method === 'PUT') body = options.challenge || { nonce, message };
      else if (request.method === 'POST') {
        const proof = JSON.parse(request.body);
        assert.equal(proof.walletAddress, address);
        assert.equal(proof.nonce, nonce);
        assert.equal(proof.message, message);
        assert.ok(crypto.verify(null, Buffer.from(proof.message), keys.publicKey, Buffer.from(proof.signature, 'base64')));
        body = options.session || { authenticated: true, provider: 'wallet' };
      } else body = { authenticated: true, player: { provider: 'wallet', walletAddress: address, displayName: 'TEST', ...options.player } };
      return { ok: options.httpError ? false : true, json: async () => body };
    }
  };
  vm.runInNewContext(source, context);
  const bridge = context.LibraryManager.library;
  const connect = id => bridge.BattleCitiesConnectWallet('Login Flow', 'OnWalletLoginResult', 'https://api.battlecities.com', id || 'attempt-1');
  return { connect, bridge, context, provider, calls, replies, timers, listeners };
}

test('Phantom signs the exact server challenge and only accepts a verified matching player', async () => {
  const s = setup(); s.connect(); await tick();
  assert.deepEqual(s.calls.map(x => x.method), ['PUT', 'POST', 'GET']);
  assert.equal(s.replies.length, 1); assert.equal(s.replies[0].ok, true);
  assert.equal(s.replies[0].attemptId, 'attempt-1');
  assert.equal(s.listeners.size, 0); assert.ok(s.timers.every(x => !x.active));
});
test('missing Phantom fails without an API request', async () => {
  const s = setup({ missing: true }); s.connect(); await tick();
  assert.equal(s.calls.length, 0); assert.match(s.replies[0].error, /Install Phantom/);
});
test('user rejection restores the login flow with a cancellation error', async () => {
  const s = setup({ provider: { connect: async () => { throw { code: 4001 }; } } });
  s.connect(); await tick(); assert.match(s.replies[0].error, /cancelled/);
});
test('an expired connection cannot continue when Phantom eventually resolves', async () => {
  let resolve;
  const s = setup({ provider: { connect: () => new Promise(r => resolve = r) } });
  s.connect(); s.timers.find(t => t.ms === 180000).fn();
  resolve({ publicKey: s.provider.publicKey }); await tick();
  assert.equal(s.calls.length, 0); assert.equal(s.replies.length, 1);
  assert.match(s.replies[0].error, /timed out/);
});
test('cancelling a pending signature prevents session creation and late callback', async () => {
  let resolve;
  const s = setup({ provider: { signMessage: () => new Promise(r => resolve = r) } });
  s.connect(); await tick(); s.bridge.BattleCitiesCancelWallet('attempt-1');
  resolve({ signature: new Uint8Array(64) }); await tick();
  assert.deepEqual(s.calls.map(x => x.method), ['PUT']); assert.equal(s.replies.length, 0);
});
test('changing accounts during signature approval rejects the attempt', async () => {
  const s = setup();
  s.provider.signMessage = async () => { s.provider.publicKey = { toString: () => 'another-account' }; return { signature: new Uint8Array(64) }; };
  s.connect(); await tick();
  assert.equal(s.calls.length, 1); assert.match(s.replies[0].error, /account changed/);
});
test('account-change event aborts a pending attempt immediately', async () => {
  const s = setup({ provider: { signMessage: () => new Promise(() => {}) } });
  s.connect(); await tick(); s.listeners.get('accountChanged')();
  assert.equal(s.replies[0].ok, false); assert.equal(s.listeners.size, 0);
});
test('HTTP success without authenticated wallet status is rejected', async () => {
  const s = setup({ session: { authenticated: false, provider: 'wallet' } });
  s.connect(); await tick(); assert.equal(s.replies[0].ok, false); assert.equal(s.calls.length, 2);
});
test('a player response for another wallet is rejected', async () => {
  const s = setup({ player: { walletAddress: 'another-account' } });
  s.connect(); await tick(); assert.equal(s.replies[0].ok, false);
});
test('malformed server challenge is never sent to the wallet for signing', async () => {
  let signed = false;
  const s = setup({ challenge: { nonce: 'bad', message }, provider: { signMessage: async () => { signed = true; } } });
  s.connect(); await tick(); assert.equal(signed, false); assert.equal(s.replies[0].ok, false);
});
test('duplicate attempts discard an older pending connection', async () => {
  let resolve;
  const s = setup({ provider: { connect: () => new Promise(r => resolve = r) } });
  s.connect('old'); const oldResolve = resolve;
  s.provider.connect = async () => ({ publicKey: s.provider.publicKey });
  s.connect('new'); await tick(); oldResolve({ publicKey: s.provider.publicKey }); await tick();
  assert.equal(s.replies.length, 1); assert.equal(s.replies[0].attemptId, 'new');
});
