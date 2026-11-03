// Phantom authentication stays separate from the Battle Cities API session.
// The server still verifies its own one-time signed challenge after connecting.
const APP_ID = '4d0d9b85-042f-477b-93d4-2176fd01d740';
const PENDING_KEY = 'battlecities.phantom.redirect.v1';
const MAX_PENDING_MS = 15 * 60 * 1000;

export function createPhantomBridge({ BrowserSDK, AddressType, browser = window, chooseProvider = showWalletChoices }) {
  let sdk, network = 'mainnet', lastProvider, pendingChoice;
  const storage = browser.sessionStorage;
  const callbackUrl = () => browser.location.origin + browser.location.pathname;
  const getSdk = () => sdk || (sdk = new BrowserSDK({
    providers: ['injected', 'google', 'apple'],
    addressTypes: [AddressType.solana],
    appId: APP_ID,
    authOptions: { redirectUrl: callbackUrl() }
  }));
  const pendingRecord = () => {
    try {
      const value = JSON.parse(storage.getItem(PENDING_KEY) || 'null');
      return value && ['google', 'apple'].includes(value.provider) && Number.isFinite(value.started) ? value : null;
    } catch { return null; }
  };
  const pending = () => {
    const value = pendingRecord();
    return value && Date.now() >= value.started && Date.now() - value.started < MAX_PENDING_MS ? value : null;
  };
  const hasCallback = () => {
    const params = new URLSearchParams(browser.location.search);
    return !!pendingRecord() && ((!!params.get('state') && (params.has('code') || params.has('error')))
      || (!!params.get('session_id') && (params.has('code') || params.get('response_type') === 'failure')));
  };
  const clearCallback = () => {
    try {
      const value = pendingRecord();
      storage.removeItem(PENDING_KEY);
      const original = value?.returnUrl;
      if (typeof original === 'string' && original.startsWith('/') && !original.startsWith('//'))
        browser.history.replaceState(browser.history.state, '', original);
    } catch { /* Session storage may be unavailable. */ }
  };
  const address = () => {
    if (!sdk?.isConnected()) return null;
    const key = sdk.solana.publicKey;
    return typeof key === 'string' ? key : key?.toString() || null;
  };
  const setNetwork = async () => { await getSdk().solana.switchNetwork(network); };
  const adapter = () => ({
    isPhantom: true,
    get publicKey() { const key = address(); return key ? { toString: () => key } : null; },
    signMessage: bytes => getSdk().solana.signMessage(bytes),
    signTransaction: transaction => getSdk().solana.signTransaction(transaction),
    on: (event, listener) => getSdk().solana.on(event, listener),
    removeListener: (event, listener) => getSdk().solana.off(event, listener)
  });
  async function restore(expected, targetNetwork = 'mainnet') {
    network = targetNetwork;
    const instance = getSdk();
    if (!instance.isConnected()) await instance.autoConnect();
    if (!instance.isConnected() || !address() || (expected && address() !== expected)) return null;
    await setNetwork();
    return adapter();
  }
  async function connect(targetNetwork = 'mainnet') {
    network = targetNetwork;
    if (hasCallback()) {
      try {
        if (!pending()) throw new Error('Wallet sign-in expired. Please try again.');
        if (new URLSearchParams(browser.location.search).has('error')) throw new Error('Wallet sign-in was cancelled.');
        const resumed = await restore(null, targetNetwork);
        if (!resumed || getSdk().getCurrentProviderInfo()?.type !== 'embedded')
          throw new Error('Wallet sign-in could not be resumed. Please try again.');
        return resumed;
      } finally { clearCallback(); }
    }
    const selected = await chooseProvider(browser, value => { pendingChoice = value; });
    pendingChoice = null;
    if (!['injected', 'google', 'apple'].includes(selected)) throw new Error('Wallet sign-in cancelled.');
    const instance = getSdk();
    if (instance.isConnected() && lastProvider !== selected) await instance.disconnect();
    if (selected !== 'injected') {
      try {
        storage.setItem(PENDING_KEY, JSON.stringify({ provider: selected, started: Date.now(),
          returnUrl: browser.location.pathname + browser.location.search + browser.location.hash }));
      } catch { throw new Error('Enable browser storage to continue with this sign-in method.'); }
    }
    let result;
    try { result = await instance.connect({ provider: selected }); }
    catch (error) { if (selected !== 'injected') clearCallback(); throw error; }
    lastProvider = selected;
    if (result?.status === 'pending') throw new Error('Continue signing in with Phantom, then return to Battle Cities.');
    if (selected !== 'injected') clearCallback();
    if (!address()) throw new Error('Phantom did not return a Solana wallet address.');
    await setNetwork();
    return adapter();
  }
  return { connect, restore, hasCallback, cancelChoice: () => pendingChoice?.(),
    current: expected => address() && (!expected || address() === expected) ? adapter() : null,
    async disconnect() { try { if (sdk?.isConnected()) await sdk.disconnect(); } finally { lastProvider = null; clearCallback(); } }
  };
}

function showWalletChoices(browser, registerCancel) {
  return new Promise((resolve, reject) => {
    const doc = browser.document;
    const backdrop = doc.createElement('div');
    Object.assign(backdrop.style, { position:'fixed', inset:'0', zIndex:'2147483647', background:'rgba(0,14,32,.78)', display:'flex', alignItems:'center', justifyContent:'center', padding:'16px' });
    const panel = doc.createElement('div');
    Object.assign(panel.style, { width:'min(420px,100%)', padding:'24px', border:'4px solid #168cdf', borderRadius:'12px', background:'#fff2d8', color:'#06284d', font:'bold 18px system-ui,sans-serif', boxShadow:'0 8px 30px #0009' });
    panel.setAttribute('role','dialog'); panel.setAttribute('aria-modal','true'); panel.setAttribute('aria-label','Choose how to connect');
    const title = doc.createElement('div'); title.textContent = 'CONNECT WALLET'; title.style.cssText = 'font-size:24px;margin-bottom:14px'; panel.appendChild(title);
    const buttons = [],previous=doc.activeElement;
    let closed=false;
    const finish = value => { if(closed)return;closed=true;doc.removeEventListener('keydown', onKey); backdrop.remove(); registerCancel(null);previous?.focus?.();value ? resolve(value) : reject(new Error('Wallet sign-in cancelled.')); };
    const onKey = event => {
      if (event.key === 'Escape') { event.preventDefault(); finish(null); }
      else if(event.key==='Tab') { const all=[...buttons,cancel],index=all.indexOf(doc.activeElement); event.preventDefault();all[(index+(event.shiftKey?-1:1)+all.length)%all.length].focus(); }
    };
    for (const [value,label] of [['injected','PHANTOM WALLET'],['google','CONTINUE WITH GOOGLE'],['apple','CONTINUE WITH APPLE']]) {
      const button = doc.createElement('button'); button.type = 'button'; button.textContent = label;
      button.style.cssText = 'display:block;width:100%;min-height:48px;margin:8px 0;border:2px solid #003668;border-radius:8px;background:#006bd0;color:white;font:inherit;cursor:pointer';
      button.addEventListener('click', () => finish(value)); panel.appendChild(button); buttons.push(button);
    }
    const note = doc.createElement('p'); note.textContent = 'Google or Apple sign-in may use a different wallet address.';
    note.style.cssText = 'font-size:14px;font-weight:normal;line-height:1.3;margin:14px 0'; panel.appendChild(note);
    const cancel = doc.createElement('button'); cancel.type='button'; cancel.textContent='CANCEL'; cancel.style.cssText='min-height:44px;border:0;background:transparent;color:#003668;font:inherit;cursor:pointer';
    cancel.addEventListener('click',()=>finish(null));panel.appendChild(cancel);
    backdrop.appendChild(panel);
    (doc.fullscreenElement || doc.webkitFullscreenElement || doc.body).appendChild(backdrop);
    doc.addEventListener('keydown',onKey);registerCancel(()=>finish(null));buttons[0].focus();
  });
}
