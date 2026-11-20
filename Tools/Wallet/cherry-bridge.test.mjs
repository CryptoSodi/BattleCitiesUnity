import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';

const bundle = fs.readFileSync(new URL('../Publishing/Cherry/cherry.js', import.meta.url), 'utf8');
const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
const token = `${encode({alg:'RS256'})}.${encode({sub:'11111111111111111111111111111111', user:{display_name:'Commander',avatar_url:''}, room_id:'room',room:{title:'Test',member_count:1}})}.signature`;

function launch(embedded) {
  const messages = [], requests = [], listeners = new Map();
  const location = new URL('https://cherry.battlecities.com/' + (embedded ? '?cherry_embed=1&cherry_lang=en&cherry_theme=dark' : ''));
  const parent = { postMessage(message, target) { messages.push({message,target}); } };
  let reloads=0;
  location.reload = () => { reloads++; };
  const window = { location, parent, addEventListener(type, fn) { listeners.set(type, [...(listeners.get(type)||[]),fn]); }, removeEventListener() {} };
  const document = { documentElement:{ lang:'', dataset:{}, style:{setProperty() {}}, setAttribute() {} } };
  const fetch = async (url, options) => { requests.push({url,options}); return {ok:true,json:async()=>({authenticated:true,sessionToken:'sess-test-123',player:{id:'ply-test',walletAddress:'11111111111111111111111111111111'}})}; };
  const context = vm.createContext({window,document,URL,URLSearchParams,TextEncoder,TextDecoder,Uint8Array,atob,btoa,console,setTimeout,clearTimeout,fetch});
  vm.runInContext(bundle, context);
  return { window, parent, messages, requests, get reloads(){return reloads;}, receive(origin, data) { for(const fn of listeners.get('message')||[]) fn({source:parent,origin,data}); } };
}

test('outside Cherry the bootstrap never initiates a host handshake', async () => {
  const game = launch(false);
  assert.equal(game.window.BattleCitiesCherry.insideCherry, false);
  assert.equal(await game.window.BattleCitiesCherry.ready, null);
  assert.equal(game.messages.length, 0);
});

test('Cherry iframe answers only a trusted host and exposes the launch to the application', async () => {
  const game = launch(true);
  game.receive('https://example.com', {type:'cherry:init',token});
  assert.equal(game.messages.length, 0);
  game.receive('https://chat.cherry.fun', {type:'cherry:init',token});
  const app = await game.window.BattleCitiesCherry.ready;
  assert.equal(app.launchToken, token);
  assert.equal(app.user.displayName, 'Commander');
  assert.equal(game.messages.some(item => item.message.type === 'cherry:ready'), true);
  const player = await game.window.BattleCitiesCherry.sessionReady;
  assert.equal(player.id, 'ply-test');
  assert.equal(game.requests.length, 1);
  assert.equal(game.requests[0].options.headers.Authorization, `Bearer ${token}`);
  assert.equal(game.requests[0].options.credentials, 'omit');
  assert.equal(game.window.BattleCitiesCherry.sessionToken, 'sess-test-123');
  game.receive('https://example.com', {type:'cherry:init',token:token+'changed'});
  assert.equal(game.reloads, 0);
  game.receive('https://chat.cherry.fun', {type:'cherry:init',token:token+'changed'});
  assert.equal(game.reloads, 1);
  assert.equal(game.window.BattleCitiesCherry.sessionToken, null);
  app.destroy();
});
