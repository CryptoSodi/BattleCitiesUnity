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
  BattleCitiesConnectWallet: function (gameObjectNamePtr, callbackMethodPtr, baseUrlPtr) {
    const gameObjectName = UTF8ToString(gameObjectNamePtr);
    const callbackMethod = UTF8ToString(callbackMethodPtr);
    const apiUrl = new URL(UTF8ToString(baseUrlPtr));
    if (['localhost', '127.0.0.1'].includes(apiUrl.hostname) &&
        ['localhost', '127.0.0.1'].includes(window.location.hostname)) apiUrl.hostname = window.location.hostname;
    const baseUrl = apiUrl.toString().replace(/\/$/, '');
    const reply = (value) => SendMessage(gameObjectName, callbackMethod, JSON.stringify(value));
    const request = async (url, options) => {
      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), 15000);
      try { return await fetch(url, Object.assign({}, options, { signal: controller.signal })); }
      finally { clearTimeout(timer); }
    };

    (async () => {
      try {
        const provider = (window.phantom && window.phantom.solana) || window.solana;
        if (!provider || !provider.isPhantom) throw new Error('Install Phantom wallet to connect.');
        const connected = await provider.connect();
        const walletAddress = connected.publicKey.toString();

        const challengeResponse = await request(baseUrl + '/api/session', {
          method: 'PUT',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
          body: JSON.stringify({ walletAddress })
        });
        const challenge = await challengeResponse.json();
        if (!challengeResponse.ok) throw new Error(challenge.error || 'Could not create wallet challenge.');

        const encoded = new TextEncoder().encode(challenge.message);
        const signed = await provider.signMessage(encoded, 'utf8');
        const bytes = signed instanceof Uint8Array ? signed : signed.signature;
        let binary = '';
        for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
        const signature = btoa(binary);

        const sessionResponse = await request(baseUrl + '/api/session', {
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
          body: JSON.stringify({
            provider: 'wallet',
            walletAddress,
            nonce: challenge.nonce,
            message: challenge.message,
            signature
          })
        });
        const session = await sessionResponse.json();
        if (!sessionResponse.ok) throw new Error(session.error || 'Wallet signature was rejected.');

        const playerResponse = await request(baseUrl + '/api/player', {
          credentials: 'include',
          cache: 'no-store',
          headers: { 'Accept': 'application/json' }
        });
        const playerBody = await playerResponse.json();
        if (!playerResponse.ok || playerBody.authenticated !== true || !playerBody.player || playerBody.player.provider !== 'wallet')
          throw new Error(playerBody.error || 'Wallet session could not load the player.');

        reply({ ok: true, player: playerBody.player });
      } catch (error) {
        reply({ ok: false, error: error && error.message ? error.message : 'Wallet login failed.' });
      }
    })();
  }
});
