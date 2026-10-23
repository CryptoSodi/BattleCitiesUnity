import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import { webcrypto } from 'node:crypto';
const source=fs.readFileSync(new URL('../../Assets/Plugins/WebGL/BattleCitiesScreenUpdates.jslib',import.meta.url),'utf8')
  .replace("{{{ makeDynCall('vii', 'callback') }}}",'callback');
const context={LibraryManager:{library:{}},mergeInto:(target,value)=>Object.assign(target,value),UTF8ToString:value=>value,
  crypto:webcrypto,atob:value=>Buffer.from(value,'base64').toString('binary'),Uint8Array};
vm.runInNewContext(source,context);
const {publicKey,privateKey}=await webcrypto.subtle.generateKey({name:'RSASSA-PKCS1-v1_5',modulusLength:2048,publicExponent:new Uint8Array([1,0,1]),hash:'SHA-256'},true,['sign','verify']);
const jwk=await webcrypto.subtle.exportKey('jwk',publicKey);
const payload=Buffer.from('{"sequence":1,"channel":"web-ui-v1"}');
const signature=Buffer.from(await webcrypto.subtle.sign('RSASSA-PKCS1-v1_5',privateKey,payload));
const base64=value=>Buffer.from(value,'base64url').toString('base64');
const verify=(data,sig,modulus=base64(jwk.n))=>new Promise(resolve=>context.LibraryManager.library.BattleCitiesVerifyScreenSignature(7,modulus,base64(jwk.e),data,sig,(id,valid)=>{assert.equal(id,7);resolve(valid);}));
assert.equal(await verify(payload.toString('base64'),signature.toString('base64')),1);
assert.equal(await verify(Buffer.from('tampered').toString('base64'),signature.toString('base64')),0);
assert.equal(await verify(payload.toString('base64'),'invalid'),0);
assert.equal(await verify(payload.toString('base64'),signature.toString('base64'),'AA=='),0);
console.log('PASS: browser WebCrypto accepts a valid signature and rejects altered content, invalid signatures and weak keys.');
