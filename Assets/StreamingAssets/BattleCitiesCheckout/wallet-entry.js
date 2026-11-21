import { PublicKey, VersionedTransaction } from '@solana/web3.js';
import { getAppReferenceKeyBytes } from '@cherrydotfun/miniapp-sdk';
import nacl from 'tweetnacl';
import quotedTransaction from '../../../Tools/Wallet/quoted-transaction.cjs';

const CHERRY_APP_ID = 'c0dcc3df-8b13-445b-afa1-573c8c567315';

function sameBytes(left, right) {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}
function walletNotReady(message) {
  const error = new Error(message);
  error.code = 'BC_WALLET_NOT_READY';
  return error;
}

window.BattleCitiesCheckoutWallet = {
  async sign(encoded, wallet, label = 'Buy game item') {
    const cherry = window.BattleCitiesCherry?.insideCherry ? window.BattleCitiesCherry : null;
    const provider = cherry ? null : window.BattleCitiesPhantom?.current(wallet) || window.phantom?.solana || window.solana;
    if (!cherry && (!provider?.isPhantom || typeof provider.signTransaction !== 'function')) throw walletNotReady('Install or open Phantom to approve checkout.');
    if (!cherry && provider.publicKey?.toString() !== wallet) throw walletNotReady('Select the wallet used to sign in to Battle Cities.');
    const bytes = Uint8Array.from(atob(encoded), c => c.charCodeAt(0));
    if (bytes.length <= 65 || bytes.length > 1232 || bytes[0] !== 1) throw new Error('The payment quote has an unsupported transaction format.');

    // This container also supports legacy messages. Preserve the server's
    // compiled account order instead of reconstructing mutable instructions.
    const transaction = VersionedTransaction.deserialize(bytes);
    const original = bytes.slice(65);
    if (transaction.version !== 'legacy' || transaction.message.header.numRequiredSignatures !== 1
        || transaction.message.staticAccountKeys[0].toBase58() !== wallet
        || !sameBytes(transaction.message.serialize(), original)) throw new Error('The payment quote does not match this wallet.');

    let signedBytes;
    if (cherry) {
      const app = await cherry.ready;
      if (!app) throw new Error('Cherry wallet is unavailable. Please reopen the game.');
      if ((app.paymentWallet || app.wallet.publicKey) !== wallet) throw new Error('Your Cherry payment wallet changed. Request a new price.');
      const key = new PublicKey(getAppReferenceKeyBytes(CHERRY_APP_ID)).toBase58();
      const keys = transaction.message.staticAccountKeys;
      const index = keys.findIndex(item => item.toBase58() === key);
      const header = transaction.message.header;
      const readonlyStart = keys.length - header.numReadonlyUnsignedAccounts;
      if (index < readonlyStart || !transaction.message.compiledInstructions.some(ix => Array.from(ix.accountKeyIndexes).includes(index)))
        throw new Error('The payment quote is missing Cherry attribution. Request a new price.');
      const description = String(label || 'Buy game item').trim().slice(0, 80);
      try { signedBytes = Uint8Array.from(await app.wallet.signTransaction(transaction, {label:description})); }
      catch (error) {
        if (error?.code === 'USER_REJECTED') throw new Error('You declined the payment. No payment was submitted.');
        if (error?.code === 'POLICY_DENIED') throw new Error('Cherry could not approve this payment under its wallet limits. Try a smaller amount if available.');
        if (error?.code === 'PRECHECK_UNAVAILABLE') throw new Error('Cherry could not check this payment. Please try again.');
        if (error?.code === 'WALLET_CHANGED' || error?.code === 'NOT_A_SIGNER') throw new Error('Your Cherry payment wallet changed. Request a new price.');
        throw error;
      }
      if ((app.paymentWallet || app.wallet.publicKey) !== wallet) throw new Error('Your Cherry payment wallet changed. Request a new price.');
    } else {
      const signed = await provider.signTransaction(transaction);
      if (provider.publicKey?.toString() !== wallet) throw walletNotReady('The wallet account changed. Sign in again.');
      if (typeof signed?.serialize !== 'function') throw new Error('Phantom did not return a signed payment transaction.');
      signedBytes = Uint8Array.from(signed.serialize());
    }
    if (signedBytes.length <= 65 || signedBytes.length > 1232 || signedBytes[0] !== 1
        || (!sameBytes(signedBytes.slice(65), original)
          && (cherry || !quotedTransaction.matchesApprovedTransaction(bytes, signedBytes))))
      throw new Error('Phantom changed the quoted transaction. Request a fresh price and try again.');

    // Verify the returned wire bytes with our own parser; wallet object methods
    // must not recompile or replace the message being checked.
    if (!nacl.sign.detached.verify(signedBytes.slice(65), signedBytes.slice(1, 65), transaction.message.staticAccountKeys[0].toBytes()))
      throw new Error('Phantom did not return a valid payment signature. No payment was submitted.');
    return btoa(String.fromCharCode(...signedBytes));
  }
};
