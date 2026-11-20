import { CherryMiniApp, isInsideCherry, isTrustedHostOrigin, readCherryDisplayContext, applyCherryContextToDocument } from '@cherrydotfun/miniapp-sdk';

// This bundle is included only in the Cherry site's HTML. The host must see
// the handshake while the much larger Unity player is still downloading.
const insideCherry = isInsideCherry({ strict: true });
const state = { insideCherry, app: null, ready: Promise.resolve(null), sessionReady: Promise.resolve(null), sessionToken: null, sessionWallet: null };
window.BattleCitiesCherry = state;

if (insideCherry) {
  applyCherryContextToDocument(readCherryDisplayContext());
  const app = new CherryMiniApp({ strict: true });
  state.app = app;
  // A new launch token can select a different payment wallet. Start a fresh
  // launch rather than keep an old session or attempt to reuse a signed quote.
  window.addEventListener('message', event => {
    const trusted = window.parent !== window
      ? event.source === window.parent && isTrustedHostOrigin(event.origin)
      : !!window.__cherry && (event.source == null || event.source === window);
    if (!trusted || !app.isReady) return;
    let message = event.data;
    if (typeof message === 'string') { try { message = JSON.parse(message); } catch { return; } }
    if (message?.type === 'cherry:init' && typeof message.token === 'string' && message.token !== app.launchToken) {
      state.sessionToken = null;
      window.location.reload();
    }
  });
  app.onContextChange(context => {
    applyCherryContextToDocument(context);
    const wallet = app.paymentWallet || app.wallet.publicKey;
    if (state.sessionToken && state.sessionWallet && wallet && wallet !== state.sessionWallet) {
      state.sessionToken = null;
      window.location.reload();
    }
  });
  // SDK context is only presentation data. Authentication must happen on the
  // backend after validating the launch token; never infer it from this object.
  state.ready = app.init().then(() => app).catch(error => {
    console.warn('Cherry connection failed:', error?.message || 'host unavailable');
    return null;
  });
  state.sessionReady = state.ready.then(async readyApp => {
    if (!readyApp?.launchToken) throw new Error('Cherry did not provide a launch token.');
    const response = await fetch('https://api.battlecities.com/api/auth/cherry', {
      method: 'POST', credentials: 'omit', cache: 'no-store', redirect: 'error',
      headers: { Accept: 'application/json', Authorization: `Bearer ${readyApp.launchToken}` },
    });
    const body = await response.json();
    if (!response.ok || body.authenticated !== true || typeof body.sessionToken !== 'string'
      || !/^sess-[a-z0-9-]+$/i.test(body.sessionToken) || !body.player?.id || !body.player?.walletAddress) {
      throw new Error('Cherry sign-in could not be verified.');
    }
    state.sessionToken = body.sessionToken;
    state.sessionWallet = body.player.walletAddress;
    return body.player;
  }).catch(error => {
    console.warn('Cherry sign-in failed:', error?.message || 'API unavailable');
    return null;
  });
}
