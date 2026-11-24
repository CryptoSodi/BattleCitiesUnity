// Deterministic, local-only test transactions. No RPC, funds, or secret wallets.
const fs = require('fs');
const path = require('path');
const { Keypair, PublicKey, SystemProgram, ComputeBudgetProgram, Transaction, TransactionInstruction } = require('../Wallet/node_modules/@solana/web3.js');
const payer = Keypair.fromSeed(Uint8Array.from({length: 32}, (_, i) => i + 1));
const recipient = Keypair.fromSeed(Uint8Array.from({length: 32}, (_, i) => i + 33)).publicKey;
const extra = Keypair.fromSeed(Uint8Array.from({length: 32}, (_, i) => i + 65)).publicKey;
const blockhash = Keypair.fromSeed(Uint8Array.from({length: 32}, (_, i) => i + 97)).publicKey.toBase58();
const lighthouse = new PublicKey('L2TExMFKdjpN9kozasaurPirfHy9P8sbXoAN1qA3S95');
const transfer = (amount = 100) => SystemProgram.transfer({fromPubkey:payer.publicKey,toPubkey:recipient,lamports:amount});
const assertion = (code, target = payer.publicKey, writable = false) => new TransactionInstruction({programId:lighthouse,
  keys:[{pubkey:target,isSigner:false,isWritable:writable}],data:Buffer.from([code,0])});
function tx(instructions, options = {}) {
  const result = new Transaction({feePayer:payer.publicKey,recentBlockhash:options.blockhash || blockhash});
  result.add(...instructions);
  if (options.signed !== false) result.sign(payer);
  return result.serialize({requireAllSignatures:options.signed !== false,verifySignatures:options.signed !== false}).toString('base64');
}
const data = {
  original:tx([transfer()],{signed:false}), unchanged:tx([transfer()]),
  allowed:tx([assertion(6),transfer(),assertion(10)]),
  changedAmount:tx([transfer(101)]), changedRecipient:tx([SystemProgram.transfer({fromPubkey:payer.publicKey,toPubkey:extra,lamports:100})]),
  unsafeAssertion:tx([transfer(),assertion(0)]), writableAccount:tx([transfer(),assertion(6,extra,true)]),
  extraInstruction:tx([transfer(),ComputeBudgetProgram.setComputeUnitPrice({microLamports:1000})]),
  changedBlockhash:tx([transfer()],{blockhash:extra.toBase58()}),
  tooManyAssertions:tx([transfer(),...Array.from({length:17},()=>assertion(6))]),
};
const matches = require('../Wallet/quoted-transaction.cjs').matchesApprovedTransaction;
for (const [name, encoded] of Object.entries(data)) {
  if (name === 'original') continue;
  const expected = name === 'unchanged' || name === 'allowed';
  if (matches(Buffer.from(data.original,'base64'),Buffer.from(encoded,'base64')) !== expected)
    throw new Error('Reference validation disagrees for ' + name);
}
fs.writeFileSync(path.join(__dirname,'quoted-checkout-fixtures.json'), JSON.stringify(data,null,2) + '\n');
console.log('Checkout fixtures match the web/API validator.');
