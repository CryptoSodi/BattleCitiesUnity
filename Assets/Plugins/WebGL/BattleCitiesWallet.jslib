mergeInto(LibraryManager.library, {
  BattleCitiesApiRequest: function (targetPtr, callbackPtr, idPtr, urlPtr, methodPtr, payloadPtr, timeout) {
    const target = UTF8ToString(targetPtr), callback = UTF8ToString(callbackPtr), id = UTF8ToString(idPtr);
    const url = new URL(UTF8ToString(urlPtr));
    const method = UTF8ToString(methodPtr), payload = UTF8ToString(payloadPtr);
    // Keep local development same-site: 127.0.0.1 and localhost are different cookie sites.
    if (['localhost', '127.0.0.1'].includes(url.hostname) &&
        ['localhost', '127.0.0.1'].includes(window.location.hostname)) url.hostname = window.location.hostname;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), Math.max(1, timeout) * 1000);
    const options = { method, credentials: 'include', cache: 'no-store', signal: controller.signal,
      headers: { Accept: 'application/json' } };
    if (payload) { options.body = payload; options.headers['Content-Type'] = 'application/json'; }
    (async () => {
      let result;
      try {
        const response = await fetch(url.toString(), options);
        const body = await response.json();
        result = { id, status: response.status, body, error: response.ok ? null : body.error || 'API request failed.' };
      } catch (e) {
        result = { id, status: 0, body: null, error: e.name === 'AbortError' ? 'The API request timed out.' : 'Cannot reach the Battle Cities API.' };
      } finally { clearTimeout(timer); }
      SendMessage(target, callback, JSON.stringify(result));
    })();
  },
  BattleCitiesCancelWallet: function (attemptIdPtr) {
    const attempt = window.__battleCitiesWalletAttempt;
    if (attempt && attempt.id === UTF8ToString(attemptIdPtr)) attempt.cancel();
  },
  BattleCitiesConnectWallet: function (gameObjectNamePtr, callbackMethodPtr, baseUrlPtr, attemptIdPtr) {
    const gameObjectName = UTF8ToString(gameObjectNamePtr);
    const callbackMethod = UTF8ToString(callbackMethodPtr);
    const attemptId = UTF8ToString(attemptIdPtr);
    const apiUrl = new URL(UTF8ToString(baseUrlPtr));
    if (['localhost', '127.0.0.1'].includes(apiUrl.hostname) &&
        ['localhost', '127.0.0.1'].includes(window.location.hostname)) apiUrl.hostname = window.location.hostname;
    const baseUrl = apiUrl.toString().replace(/\/$/, '');
    if (window.__battleCitiesWalletAttempt) window.__battleCitiesWalletAttempt.cancel();
    let finished = false, provider, walletAddress, overallTimer;
    const controllers = new Set();
    const ensureActive = () => {
      if (finished) throw new Error('Wallet sign-in cancelled.');
    };
    const accountChanged = () => finish({ ok: false, error: 'The wallet account changed. Please sign in again.' });
    const disconnected = () => finish({ ok: false, error: 'The wallet disconnected. Please try again.' });
    const cleanup = () => {
      finished = true;
      clearTimeout(overallTimer);
      controllers.forEach(controller => controller.abort());
      if (provider && provider.removeListener) {
        provider.removeListener('accountChanged', accountChanged);
        provider.removeListener('disconnect', disconnected);
      }
      if (window.__battleCitiesWalletAttempt && window.__battleCitiesWalletAttempt.id === attemptId)
        delete window.__battleCitiesWalletAttempt;
    };
    const finish = value => {
      if (finished) return;
      cleanup();
      SendMessage(gameObjectName, callbackMethod, JSON.stringify(Object.assign({ attemptId }, value)));
    };
    window.__battleCitiesWalletAttempt = { id: attemptId, cancel: cleanup };
    overallTimer = setTimeout(() => finish({ ok: false, error: 'Wallet sign-in timed out. Please try again.' }), 180000);
    const request = async (path, options) => {
      ensureActive();
      const controller = new AbortController();
      controllers.add(controller);
      const timer = setTimeout(() => controller.abort(), 15000);
      try {
        const response = await fetch(baseUrl + path, Object.assign({ credentials: 'include', cache: 'no-store',
          redirect: 'error', headers: { Accept: 'application/json', 'Content-Type': 'application/json' } },
          options, { signal: controller.signal }));
        const body = await response.json();
        ensureActive();
        if (!response.ok) throw new Error(body.error || 'The Battle Cities API could not complete sign-in.');
        return body;
      } finally { clearTimeout(timer); controllers.delete(controller); }
    };

    (async () => {
      try {
        provider = (window.phantom && window.phantom.solana) || window.solana;
        if (!provider || !provider.isPhantom) throw new Error('Install Phantom wallet to connect.');
        const connected = await provider.connect();
        ensureActive();
        walletAddress = connected.publicKey.toString();
        if (provider.on) {
          provider.on('accountChanged', accountChanged);
          provider.on('disconnect', disconnected);
        }
        const challenge = await request('/api/session', {
          method: 'PUT',
          body: JSON.stringify({ walletAddress })
        });
        if (typeof challenge.message !== 'string' || !challenge.message || challenge.message.length > 4096 ||
            typeof challenge.nonce !== 'string' || !/^[a-f0-9]{32}$/i.test(challenge.nonce))
          throw new Error('The server returned an invalid sign-in challenge.');

        const encoded = new TextEncoder().encode(challenge.message);
        const signed = await provider.signMessage(encoded, 'utf8');
        ensureActive();
        if (!provider.publicKey || provider.publicKey.toString() !== walletAddress ||
            (signed.publicKey && signed.publicKey.toString() !== walletAddress))
          throw new Error('The wallet account changed. Please sign in again.');
        const bytes = signed instanceof Uint8Array ? signed : signed.signature;
        if (!(bytes instanceof Uint8Array) || bytes.length !== 64)
          throw new Error('Phantom returned an invalid signature.');
        let binary = '';
        for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
        const signature = btoa(binary);

        const session = await request('/api/session', {
          method: 'POST',
          body: JSON.stringify({
            provider: 'wallet',
            walletAddress,
            nonce: challenge.nonce,
            message: challenge.message,
            signature
          })
        });
        if (session.authenticated !== true || session.provider !== 'wallet')
          throw new Error('The API did not establish a verified wallet session.');

        const playerBody = await request('/api/player', { method: 'GET' });
        if (playerBody.authenticated !== true || !playerBody.player || playerBody.player.provider !== 'wallet' ||
            playerBody.player.walletAddress !== walletAddress)
          throw new Error(playerBody.error || 'Wallet session could not load the player.');

        finish({ ok: true, player: playerBody.player });
      } catch (error) {
        finish({ ok: false, error: error && error.code === 4001 ? 'Wallet approval was cancelled. Please try again.'
          : error && error.name === 'AbortError' ? 'The API request timed out. Please try again.'
          : error && error.message ? error.message : 'Wallet login failed.' });
      }
    })();
  }
});
