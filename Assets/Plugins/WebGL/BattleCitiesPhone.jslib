mergeInto(LibraryManager.library, {
  BattleCitiesPhoneStop: function () {
    const host = window.__battleCitiesPhone;
    if (!host) return;
    host.stopped = true;
    clearTimeout(host.timeout);
    if (host.peer) host.peer.destroy();
    delete window.__battleCitiesPhone;
  },
  BattleCitiesPhoneStart: function (targetPtr, controllerUrlPtr) {
    const target = UTF8ToString(targetPtr);
    const controllerUrl = UTF8ToString(controllerUrlPtr);
    const old = window.__battleCitiesPhone;
    if (old) { old.stopped = true; clearTimeout(old.timeout); if (old.peer) old.peer.destroy(); }
    const host = { stopped: false, peer: null, connection: null };
    window.__battleCitiesPhone = host;
    const send = data => { if (!host.stopped) SendMessage(target, 'OnPhoneEvent', JSON.stringify(data)); };
    const fail = () => {
      if (host.stopped) return;
      send({ type: 'error' }); host.stopped = true;
      clearTimeout(host.timeout); if (host.peer) host.peer.destroy();
    };
    const load = (url, name) => window[name] ? Promise.resolve(window[name]) : new Promise((resolve, reject) => {
      const tag = document.createElement('script'); tag.src = url;
      tag.onload = () => window[name] ? resolve(window[name]) : reject(new Error('Library unavailable'));
      tag.onerror = reject; document.head.appendChild(tag);
    });
    host.timeout = setTimeout(fail, 20000);
    (async () => {
      if (!window.isSecureContext) throw new Error('HTTPS required');
      const libs = await Promise.all([
        load('https://unpkg.com/peerjs@1.4.7/dist/peerjs.min.js', 'Peer'),
        load('https://cdn.jsdelivr.net/npm/qrcode-generator@1.4.4/qrcode.js', 'qrcode')
      ]);
      if (host.stopped) return;
      const alphabet = 'BCDFGHJKLMNPQRSTVWXZ';
      const random = crypto.getRandomValues(new Uint32Array(4));
      const room = Array.from(random, n => alphabet[n % alphabet.length]).join('');
      const hash = await crypto.subtle.digest('SHA-1', new TextEncoder().encode(room));
      const id = Array.from(new Uint8Array(hash), n => n.toString(16).padStart(2, '0')).join('');
      if (host.stopped) return;
      // The controller ships in StreamingAssets alongside this build, including subdirectory deployments.
      // Never place account session credentials in this URL.
      const url = new URL(controllerUrl, document.baseURI);
      const controllerPage = await fetch(url.toString(), {credentials:'omit',cache:'no-store'});
      if (!controllerPage.ok) throw new Error('Controller page unavailable');
      if (host.stopped) return;
      url.searchParams.set('rc', room); url.hash = '?rc=' + room;
      const peer = host.peer = new libs[0](id);
      peer.on('open', () => {
        if (host.stopped) return;
        clearTimeout(host.timeout);
        const code = libs[1](0, 'M'); code.addData(url.toString()); code.make();
        const count = code.getModuleCount(), scale = 6, border = 4;
        const canvas = document.createElement('canvas'); canvas.width = canvas.height = (count + border * 2) * scale;
        const ctx = canvas.getContext('2d'); ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, canvas.width, canvas.height); ctx.fillStyle = '#000';
        for (let y = 0; y < count; y++) for (let x = 0; x < count; x++) if (code.isDark(y, x)) ctx.fillRect((x + border) * scale, (y + border) * scale, scale, scale);
        send({ type: 'ready', room, qr: canvas.toDataURL('image/png') });
      });
      peer.on('error', fail);
      peer.on('disconnected', fail);
      peer.on('connection', connection => {
        if (host.stopped || (host.connection && host.connection.open)) { connection.close(); return; }
        host.connection = connection; let sequence = 0;
        connection.on('data', data => {
          if (host.stopped || host.connection !== connection || data?.type !== 'gamepads') return;
          if (!Number.isSafeInteger(data.seq) || data.seq <= sequence) return;
          const pad = data.gamepads?.[0];
          if (!pad || !pad.connected || !Array.isArray(pad.axes) || !Array.isArray(pad.buttons)) return;
          if (pad.axes.length < 2 || pad.axes.length > 8 || pad.buttons.length > 32) return;
          if (pad.axes.some(n => typeof n !== 'number' || !Number.isFinite(n))) return;
          sequence = data.seq;
          send({ type: 'state', axes: pad.axes, buttons: pad.buttons.map(b => b?.pressed === true || (typeof b?.value === 'number' && b.value > .5)) });
        });
        const closed = () => { if (host.connection === connection) { host.connection = null; send({ type: 'closed' }); } };
        connection.on('close', closed); connection.on('error', closed);
      });
    })().catch(fail);
  }
});
