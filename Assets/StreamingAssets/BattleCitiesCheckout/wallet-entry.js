import { Transaction } from '@solana/web3.js';

window.BattleCitiesCheckoutWallet = {
  async sign(encoded, wallet) {
    const provider = window.phantom?.solana || window.solana;
    if (!provider?.isPhantom || typeof provider.signTransaction !== 'function') throw new Error('Install or open Phantom to approve checkout.');
    if (provider.publicKey?.toString() !== wallet) throw new Error('Select the wallet used to sign in to Battle Cities.');
    const bytes = Uint8Array.from(atob(encoded), c => c.charCodeAt(0));
    const transaction = Transaction.from(bytes);
    const original = transaction.serializeMessage();
    const signed = await provider.signTransaction(transaction);
    if (provider.publicKey?.toString() !== wallet) throw new Error('The wallet account changed. Sign in again.');
    const message = signed.serializeMessage();
    if (message.length !== original.length || message.some((value, index) => value !== original[index]) || !signed.verifySignatures()) throw new Error('The wallet returned an invalid transaction.');
    return btoa(String.fromCharCode(...signed.serialize()));
  }
};
