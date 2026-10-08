import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import { Keypair, Transaction, VersionedTransaction, TransactionMessage, ComputeBudgetProgram,
  SystemProgram, PublicKey } from '@solana/web3.js';

const bundle = fs.readFileSync(new URL('../../Assets/StreamingAssets/BattleCitiesCheckout/wallet.js',import.meta.url),'utf8');
const key = Keypair.generate();
const wallet = key.publicKey.toBase58();
const treasury = new PublicKey('6wQz66BgRsX6DVHAD3PDCXjKVpe3LLrj3FGiQwCSZV7F');
function quote({ price = true } = {}) {
  const tx = new Transaction({feePayer:key.publicKey,recentBlockhash:Keypair.generate().publicKey.toBase58()});
  tx.add(ComputeBudgetProgram.setComputeUnitLimit({units:250000}));
  if (price) tx.add(ComputeBudgetProgram.setComputeUnitPrice({microLamports:0}));
  tx.add(SystemProgram.transfer({fromPubkey:key.publicKey,toPubkey:treasury,lamports:10000000}));
  return tx.serialize({requireAllSignatures:false,verifySignatures:false}).toString('base64');
}
function helper(signTransaction, connected = key.publicKey) {
  const provider = {isPhantom:true,publicKey:connected,signTransaction};
  const context = vm.createContext({window:{phantom:{solana:provider}},atob,btoa,Uint8Array,
    TextEncoder,TextDecoder,console,setTimeout,clearTimeout});
  vm.runInContext(bundle,context);
  return {sign:context.window.BattleCitiesCheckoutWallet.sign,provider};
}
function signImmutable(tx) {
  const result = VersionedTransaction.deserialize(tx.serialize());
  result.sign([key]); return result;
}
function legacy(tx) { return Transaction.from(tx.serialize()); }
const original = quote();

test('immutable legacy wallet response preserves the server message and valid signature',async()=>{
  const h=helper(async tx=>signImmutable(tx));
  const signed=Buffer.from(await h.sign(original,wallet),'base64');
  assert.deepEqual(signed.subarray(65),Buffer.from(original,'base64').subarray(65));
  assert.ok(Transaction.from(signed).verifySignatures());
});
test('legacy wallet response is verified from its signed wire bytes',async()=>{
  const h=helper(async tx=>{const result=legacy(tx);result.sign(key);return result;});
  assert.ok(Transaction.from(Buffer.from(await h.sign(original,wallet),'base64')).verifySignatures());
});
test('explicit quote fee prevents simulated wallet fee insertion',async()=>{
  let modified=false;
  const h=helper(async tx=>{
    const result=legacy(tx);
    if(!result.instructions.some(ix=>ix.programId.equals(ComputeBudgetProgram.programId)&&ix.data[0]===3)) {
      modified=true;result.add(ComputeBudgetProgram.setComputeUnitPrice({microLamports:1000}));
    }
    result.sign(key);return result;
  });
  await assert.rejects(h.sign(quote({price:false}),wallet),/changed the quoted transaction/);
  assert.ok(modified);modified=false;
  await h.sign(original,wallet);assert.equal(modified,false);
});
test('changed payment amount and recipient are rejected despite valid wallet signatures',async()=>{
  for (const destination of [treasury,Keypair.generate().publicKey]) {
    const h=helper(async tx=>{
      const result=legacy(tx);result.instructions[2]=SystemProgram.transfer({fromPubkey:key.publicKey,toPubkey:destination,lamports:20000000});
      result.sign(key);return result;
    });
    await assert.rejects(h.sign(original,wallet),/changed the quoted transaction/);
  }
});
test('wallet replacement of the quoted blockhash is rejected',async()=>{
  const h=helper(async tx=>{const result=legacy(tx);result.recentBlockhash=Keypair.generate().publicKey.toBase58();result.sign(key);return result;});
  await assert.rejects(h.sign(original,wallet),/changed the quoted transaction/);
});
test('missing and incorrect signatures are rejected',async()=>{
  for (const fill of [0,1]) {
    const h=helper(async tx=>{const result=VersionedTransaction.deserialize(tx.serialize());result.signatures[0].fill(fill);return result;});
    await assert.rejects(h.sign(original,wallet),/valid payment signature/);
  }
});
test('account changes during signing do not yield a payment receipt',async()=>{
  let h;h=helper(async tx=>{h.provider.publicKey=Keypair.generate().publicKey;return signImmutable(tx);});
  await assert.rejects(h.sign(original,wallet),/account changed/);
});
test('wrong wallet and malformed quote fail before invoking wallet signing',async()=>{
  let calls=0;const h=helper(async tx=>{calls++;return signImmutable(tx);});
  await assert.rejects(h.sign(original,Keypair.generate().publicKey.toBase58()),/Select the wallet/);
  await assert.rejects(h.sign(Buffer.from([1,0]).toString('base64'),wallet),/unsupported transaction format/);
  assert.equal(calls,0);
});
test('a quote whose payer differs from the signed-in wallet is rejected',async()=>{
  const other=Keypair.generate();let calls=0;const h=helper(async tx=>{calls++;return tx;},other.publicKey);
  await assert.rejects(h.sign(original,other.publicKey.toBase58()),/does not match this wallet/);
  assert.equal(calls,0);
});
test('unsupported v0 quotes fail before invoking the wallet',async()=>{
  const tx=new VersionedTransaction(new TransactionMessage({payerKey:key.publicKey,recentBlockhash:Keypair.generate().publicKey.toBase58(),
    instructions:[SystemProgram.transfer({fromPubkey:key.publicKey,toPubkey:treasury,lamports:1})]}).compileToV0Message());
  let calls=0;const h=helper(async tx=>{calls++;return tx;});
  await assert.rejects(h.sign(Buffer.from(tx.serialize()).toString('base64'),wallet),/does not match this wallet/);
  assert.equal(calls,0);
});
