import test from 'node:test';
import assert from 'node:assert/strict';
import { createPhantomBridge } from '../../Assets/StreamingAssets/BattleCitiesCheckout/phantom-core.mjs';

const WALLET='11111111111111111111111111111111';
const KEY='battlecities.phantom.redirect.v1';

function fixture({ choice='injected', initial=null, autoType='embedded', autoAddress=WALLET, status='completed' }={}) {
  const entries=new Map(initial?[[KEY,JSON.stringify(initial)]]:[]);
  const browser={
    sessionStorage:{ getItem:k=>entries.get(k)??null,setItem:(k,v)=>entries.set(k,v),removeItem:k=>entries.delete(k) },
    location:{ origin:'https://test.battlecities.com',pathname:'/',search:'',hash:'' },
    history:{ state:null,replaceState(_state,_title,url){browser.replaced=url;} }
  };
  let mock;
  class FakeSDK {
    constructor(config) {
      mock=this;this.config=config;this.connected=false;this.type=null;this.events=new Map();
      this.solana={publicKey:null,switchNetwork:async n=>{this.network=n;},
        signMessage:async m=>({signature:new Uint8Array(64),publicKey:this.solana.publicKey,message:m}),
        signTransaction:async tx=>tx,on:(n,f)=>this.events.set(n,f),off:n=>this.events.delete(n)};
    }
    isConnected(){return this.connected;}
    async connect({provider}) {this.provider=provider;this.type=provider==='injected'?'injected':'embedded';
      if(status==='pending')return {status};this.connected=true;this.solana.publicKey=WALLET;return {status,addresses:[{address:WALLET}]};}
    async autoConnect(){this.type=autoType;this.connected=!!autoAddress;this.solana.publicKey=autoAddress;}
    getCurrentProviderInfo(){return {type:this.type};}
    async disconnect(){this.connected=false;this.solana.publicKey=null;this.disconnects=(this.disconnects||0)+1;}
  }
  const bridge=createPhantomBridge({BrowserSDK:FakeSDK,AddressType:{solana:'Solana'},browser,chooseProvider:async()=>choice});
  return {bridge,browser,entries,get sdk(){return mock;}};
}

test('configured app supports Phantom, Google and Apple while preserving test network',async()=>{
  const f=fixture();const provider=await f.bridge.connect('devnet');
  assert.equal(f.sdk.config.appId,'4d0d9b85-042f-477b-93d4-2176fd01d740');
  assert.deepEqual(f.sdk.config.providers,['injected','google','apple']);
  assert.equal(f.sdk.config.authOptions.redirectUrl,'https://test.battlecities.com/');
  assert.equal(f.sdk.provider,'injected');assert.equal(f.sdk.network,'devnet');
  assert.equal(provider.publicKey.toString(),WALLET);
});

test('embedded redirect resumes only with a pending request and cleans callback parameters',async()=>{
  const f=fixture({choice:'google',status:'pending'});
  await assert.rejects(f.bridge.connect('devnet'),/Continue signing in/);
  assert.equal(JSON.parse(f.entries.get(KEY)).provider,'google');
  f.browser.location.search='?code=temporary-code&state=session_test';
  assert.equal(f.bridge.hasCallback(),true);
  const provider=await f.bridge.connect('devnet');
  assert.equal(provider.publicKey.toString(),WALLET);assert.equal(f.sdk.type,'embedded');
  assert.equal(f.sdk.network,'devnet');assert.equal(f.entries.has(KEY),false);assert.equal(f.browser.replaced,'/');
});

test('unsolicited, expired and failed redirects cannot log in',async()=>{
  const f=fixture();f.browser.location.search='?code=code&state=session_test';assert.equal(f.bridge.hasCallback(),false);
  const old=fixture({initial:{provider:'google',started:Date.now()-16*60*1000,returnUrl:'/'}});
  old.browser.location.search='?code=code&state=session_test';assert.equal(old.bridge.hasCallback(),true);
  await assert.rejects(old.bridge.connect(),/expired/);assert.equal(old.entries.has(KEY),false);assert.equal(old.browser.replaced,'/');
  const denied=fixture({initial:{provider:'apple',started:Date.now(),returnUrl:'/'}});
  denied.browser.location.search='?error=access_denied&state=session_test';
  await assert.rejects(denied.bridge.connect(),/cancelled/);assert.equal(denied.entries.has(KEY),false);
});

test('a redirect cannot silently fall back to a different injected wallet',async()=>{
  const f=fixture({initial:{provider:'google',started:Date.now(),returnUrl:'/'},autoType:'injected'});
  f.browser.location.search='?code=code&state=session_test';
  await assert.rejects(f.bridge.connect(),/could not be resumed/);assert.equal(f.entries.has(KEY),false);
});

test('restored purchase wallet must match the authenticated account',async()=>{
  const f=fixture({autoAddress:WALLET});assert.equal(await f.bridge.restore('another-wallet','devnet'),null);
  const provider=await f.bridge.restore(WALLET,'devnet');assert.equal(provider.publicKey.toString(),WALLET);
  assert.equal(f.bridge.current('another-wallet'),null);assert.equal(f.sdk.network,'devnet');
  await f.bridge.disconnect();assert.equal(f.bridge.current(WALLET),null);
});
