// Cherry chat is an optional surface shown only while the Unity home page is active.
export function createCherryChat({ state, CherryEmbed, document, fetch: request }) {
  let home = false, open = false, widget = null, pending = null, root, panel, content, status, launcher;

  function createUi() {
    if (root) return;
    const style = document.createElement('style');
    style.textContent = `
      @font-face{font-family:BattleCitiesChat;src:url('/StreamingAssets/BattleCitiesPhone/BarlowCondensed-Bold.ttf')}
      #bc-cherry-chat{position:fixed;right:max(12px,env(safe-area-inset-right));bottom:max(12px,env(safe-area-inset-bottom));z-index:2147483000;font-family:BattleCitiesChat,Arial,sans-serif;color:#fff}
      #bc-cherry-chat [hidden]{display:none!important}
      #bc-cherry-chat button{font:700 21px BattleCitiesChat,Arial,sans-serif;cursor:pointer;touch-action:manipulation}
      #bc-cherry-chat-launcher{min-width:92px;min-height:64px;padding:8px 18px;border:2px solid #d7a641;border-radius:10px;background:#0754b8;color:#fff;box-shadow:0 3px 10px #0009}
      #bc-cherry-chat button:focus-visible{outline:3px solid #ffcc45;outline-offset:3px}
      #bc-cherry-chat-panel{position:absolute;right:0;bottom:72px;width:min(380px,calc(100vw - 24px));height:min(560px,calc(100dvh - 120px));min-height:180px;display:flex;flex-direction:column;background:#061d36;border:2px solid #d7a641;border-radius:12px;box-shadow:0 6px 24px #000a;overflow:hidden}
      #bc-cherry-chat-header{height:52px;flex:none;display:flex;align-items:center;justify-content:space-between;padding:0 12px;background:#102f51}
      #bc-cherry-chat-header strong{font-size:24px;color:#ffcf65}
      #bc-cherry-chat-close{min-width:44px;min-height:44px;border:0;border-radius:8px;background:transparent;color:white;font-size:30px}
      #bc-cherry-chat-content{flex:1;min-height:0;position:relative}
      #bc-cherry-chat-content iframe{width:100%;height:100%;border:0}
      #bc-cherry-chat-status{padding:14px;font-size:20px;line-height:1.2}
    `;
    document.head.appendChild(style);
    root = document.createElement('div'); root.id = 'bc-cherry-chat'; root.hidden = true;
    launcher = document.createElement('button'); launcher.id = 'bc-cherry-chat-launcher'; launcher.type = 'button';
    launcher.textContent = 'CHAT'; launcher.setAttribute('aria-expanded', 'false'); launcher.setAttribute('aria-controls', 'bc-cherry-chat-panel');
    panel = document.createElement('section'); panel.id = 'bc-cherry-chat-panel'; panel.hidden = true; panel.setAttribute('aria-label', 'Battle Cities chat');
    const header = document.createElement('div'); header.id = 'bc-cherry-chat-header';
    const title = document.createElement('strong'); title.textContent = 'BATTLE CITIES CHAT';
    const close = document.createElement('button'); close.id = 'bc-cherry-chat-close'; close.type = 'button'; close.textContent = '×'; close.setAttribute('aria-label', 'Close chat');
    content = document.createElement('div'); content.id = 'bc-cherry-chat-content';
    status = document.createElement('div'); status.id = 'bc-cherry-chat-status'; status.setAttribute('role', 'status');
    content.appendChild(status); header.appendChild(title); header.appendChild(close); panel.appendChild(header); panel.appendChild(content);
    root.appendChild(panel); root.appendChild(launcher); document.body.appendChild(root);
    launcher.addEventListener('click', () => { if (open) closeChat(); else void openChat(); });
    close.addEventListener('click', closeChat);
    root.addEventListener('keydown', event => { if (event.key === 'Escape') { event.stopPropagation(); closeChat(); launcher.focus(); } });
  }

  function closeChat() { open = false; if (panel) panel.hidden = true; if (launcher) launcher.setAttribute('aria-expanded', 'false'); }
  async function openChat() {
    if (!home || !root) return;
    open = true; panel.hidden = false; launcher.setAttribute('aria-expanded', 'true');
    if (widget || pending) return;
    status.hidden = false; status.textContent = 'Connecting to chat...';
    pending = (async () => {
      if (!await state.sessionReady || !state.sessionToken) throw new Error('Cherry sign-in is unavailable.');
      const loadToken = async () => {
      const response = await request('https://api.battlecities.com/api/auth/cherry-chat', {
        method: 'POST', credentials: 'omit', cache: 'no-store', redirect: 'error',
        headers: { Accept: 'application/json', Authorization: `Bearer ${state.sessionToken}` },
      });
      if (!response.ok) throw new Error('Chat is temporarily unavailable.');
      return response.json();
      };
      const config = await loadToken();
      const app = await state.ready;
      if (!app || !config.appId || !config.roomId || !config.token || !config.walletAddress ||
          (app.paymentWallet || app.wallet.publicKey) !== config.walletAddress) throw new Error('Chat is temporarily unavailable.');
      const next = new CherryEmbed({ appId: config.appId, roomId: config.roomId, token: config.token,
        embedUrl: 'https://embed.cherry.fun', walletAddress: config.walletAddress,
        signChallengeHandler: async message => {
          if ((app.paymentWallet || app.wallet.publicKey) !== config.walletAddress) throw new Error('Your wallet changed. Reopen the game.');
          const signature = await app.wallet.signMessage(message);
          if ((app.paymentWallet || app.wallet.publicKey) !== config.walletAddress) throw new Error('Your wallet changed. Reopen the game.');
          return signature;
        },
        container: content, mode: 'single', position: 'inline' });
      next.on('walletConnectRequested', async () => {
        try {
          const fresh = await loadToken();
          if (fresh.walletAddress !== config.walletAddress || !fresh.token) throw new Error('Wallet changed');
          next.setToken(fresh.token); next.setWalletAddress(fresh.walletAddress);
        } catch { status.hidden = false; status.textContent = 'Chat is unavailable. Close and try again.'; }
      });
      try { await next.mount(); widget = next; status.hidden = true; status.textContent = ''; }
      catch (error) { next.destroy(); throw error; }
    })();
    try { await pending; } catch { status.textContent = 'Chat is unavailable. Close and try again.'; }
    finally { pending = null; }
  }
  return {
    setHomeVisible(visible) {
      home = !!visible;
      if (!home) { closeChat(); if (root) root.hidden = true; return; }
      createUi(); root.hidden = false;
    },
    destroy() { closeChat(); if (widget) widget.destroy(); if (root) root.remove(); widget = root = null; },
  };
}
