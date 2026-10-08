import { Transaction, VersionedTransaction } from '@solana/web3.js';

function sameBytes(left, right) {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

window.BattleCitiesCheckoutWallet = {
  async sign(encoded, wallet) {
    const provider = window.phantom?.solana || window.solana;
    if (!provider?.isPhantom || typeof provider.signTransaction !== 'function') throw new Error('Install or open Phantom to approve checkout.');
    if (provider.publicKey?.toString() !== wallet) throw new Error('Select the wallet used to sign in to Battle Cities.');
    const bytes = Uint8Array.from(atob(encoded), c => c.charCodeAt(0));
    if (bytes.length <= 65 || bytes.length > 1232 || bytes[0] !== 1) throw new Error('The payment quote has an unsupported transaction format.');

    // This container also supports legacy messages. Preserve the server's
    // compiled account order instead of reconstructing mutable instructions.
    const transaction = VersionedTransaction.deserialize(bytes);
    const original = bytes.slice(65);
    if (transaction.version !== 'legacy' || transaction.message.header.numRequiredSignatures !== 1
        || transaction.message.staticAccountKeys[0].toBase58() !== wallet
        || !sameBytes(transaction.message.serialize(), original)) throw new Error('The payment quote does not match this wallet.');

    const signed = await provider.signTransaction(transaction);
    if (provider.publicKey?.toString() !== wallet) throw new Error('The wallet account changed. Sign in again.');
    if (typeof signed?.serialize !== 'function') throw new Error('Phantom did not return a signed payment transaction.');
    const signedBytes = Uint8Array.from(signed.serialize());
    if (signedBytes[0] !== 1 || !sameBytes(signedBytes.slice(65), original)) throw new Error('Phantom changed the quoted transaction. Request a fresh price and try again.');

    // Verify the returned wire bytes with our own parser; wallet object methods
    // must not recompile or replace the message being checked.
    if (!Transaction.from(signedBytes).verifySignatures()) throw new Error('Phantom did not return a valid payment signature. No payment was submitted.');
    return btoa(String.fromCharCode(...signedBytes));
  }
};
