import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';

const source=fs.readFileSync(new URL('../../Assets/Plugins/WebGL/BattleCitiesWallet.jslib',import.meta.url),'utf8');
function library(window) {
  const LibraryManager={library:{}};
  vm.runInNewContext(source,{window,LibraryManager,UTF8ToString:value=>value,mergeInto:Object.assign});
  return LibraryManager.library;
}

test('Unity checks the active wallet instead of the saved game session',()=>{
  let connected=true;
  const bridge={ready:wallet=>connected&&wallet==='expected'};
  const check=library({BattleCitiesPhantom:bridge}).BattleCitiesIsPhantomConnected;
  assert.equal(check('expected'),1);
  connected=false;
  assert.equal(check('expected'),0);
  connected=true;
  assert.equal(check('other-account'),0);
});

test('native fallback rejects locked, disconnected, and changed accounts',()=>{
  const provider={isPhantom:true,isConnected:true,publicKey:'expected'};
  const check=library({phantom:{solana:provider}}).BattleCitiesIsPhantomConnected;
  assert.equal(check('expected'),1);
  provider.isConnected=false;assert.equal(check('expected'),0);
  provider.isConnected=true;provider.publicKey=null;assert.equal(check('expected'),0);
  provider.publicKey='other-account';assert.equal(check('expected'),0);
});

test('stale or unavailable browser bridges never claim purchase readiness',()=>{
  const stale=library({BattleCitiesPhantom:{ready:()=>false},phantom:{solana:{isPhantom:true,isConnected:true,publicKey:'expected'}}});
  assert.equal(stale.BattleCitiesIsPhantomConnected('expected'),0);
  const broken=library({BattleCitiesPhantom:{ready(){throw new Error('provider disappeared');}}});
  assert.equal(broken.BattleCitiesIsPhantomConnected('expected'),0);
});

test('checkout reports a missing wallet to Unity without treating cancellation as disconnect',async()=>{
  const checkout=fs.readFileSync(new URL('../../Assets/Plugins/WebGL/BattleCitiesCheckout.jslib',import.meta.url),'utf8');
  async function run(code) {
    let resolve;const completed=new Promise(done=>resolve=done);
    const error=Object.assign(new Error('wallet operation failed'),{code});
    const window={location:{href:'https://game.example/',origin:'https://game.example'},BattleCitiesCheckoutWallet:{sign:async()=>{throw error;}}};
    const LibraryManager={library:{}};
    vm.runInNewContext(checkout,{window,LibraryManager,UTF8ToString:value=>value,mergeInto:Object.assign,
      SendMessage:(_target,_callback,value)=>resolve(JSON.parse(value)),URL,Date,setTimeout,clearTimeout});
    LibraryManager.library.BattleCitiesSignCheckout('target','callback','attempt','transaction','wallet',
      new Date(Date.now()+60000).toISOString(),'/wallet.js','item');
    return completed;
  }
  assert.equal((await run('BC_WALLET_NOT_READY')).errorCode,'wallet-not-ready');
  assert.equal((await run(4001)).errorCode,null);
});
