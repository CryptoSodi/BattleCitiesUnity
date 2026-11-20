import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createCherryChat } from './cherry-chat.mjs';

class Element {
  constructor(tag) { this.tag = tag; this.children = []; this.listeners = {}; this.attributes = {}; this.hidden = false; }
  appendChild(child) { this.children.push(child); return child; }
  setAttribute(name, value) { this.attributes[name] = value; }
  addEventListener(name, handler) { this.listeners[name] = handler; }
  click() { this.listeners.click?.(); }
  focus() {}
  remove() { this.removed = true; }
}
function fixture() {
  const document = { head: new Element('head'), body: new Element('body'), createElement: tag => new Element(tag) };
  const calls = [];
  class Embed {
    constructor(config) { this.config = config; calls.push(config); }
    async mount() { this.mounted = true; }
    destroy() { this.destroyed = true; }
    on() {}
  }
  const fetch = async (url, options) => {
    calls.push({ url, options });
    return { ok: true, json: async () => ({ appId: 'embed', roomId: 'room', walletAddress: 'wallet', token: 'signed-token' }) };
  };
  const state = { sessionReady: Promise.resolve({ id: 'player' }), sessionToken: 'sess-test', ready: Promise.resolve({ wallet: { publicKey: 'wallet', signMessage: async bytes => bytes } }) };
  const chat = createCherryChat({ state, CherryEmbed: Embed, window: {}, document, fetch });
  return { chat, state, document, calls };
}
const tick = () => new Promise(resolve => setImmediate(resolve));

test('chat appears only on the home page and opens an authenticated room on demand', async () => {
  const f = fixture();
  assert.equal(f.document.body.children.length, 0);
  f.chat.setHomeVisible(true);
  const root = f.document.body.children[0], panel = root.children[0], button = root.children[1];
  assert.equal(root.hidden, false); assert.equal(panel.hidden, true); assert.equal(f.calls.length, 0);
  button.click(); await tick();
  assert.equal(panel.hidden, false);
  assert.equal(f.calls[0].url, 'https://api.battlecities.com/api/auth/cherry-chat');
  assert.equal(f.calls[0].options.credentials, 'omit');
  assert.equal(f.calls[0].options.headers.Authorization, 'Bearer sess-test');
  assert.equal(f.calls[1].roomId, 'room'); assert.equal(f.calls[1].token, 'signed-token');
  assert.deepEqual(await f.calls[1].signChallengeHandler(new Uint8Array([1, 2])), new Uint8Array([1, 2]));
  f.chat.setHomeVisible(false);
  assert.equal(root.hidden, true); assert.equal(panel.hidden, true);
  f.chat.setHomeVisible(true); assert.equal(panel.hidden, true);
  button.click(); await tick(); assert.equal(f.calls.length, 2);
});

test('unavailable Cherry sign-in never sends a chat authentication request', async () => {
  const f = fixture(); f.state.sessionReady = Promise.resolve(null);
  f.chat.setHomeVisible(true);
  const root = f.document.body.children[0]; root.children[1].click(); await tick();
  assert.equal(f.calls.length, 0);
  assert.match(root.children[0].children[1].children[0].textContent, /unavailable/i);
});
