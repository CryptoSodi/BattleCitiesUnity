mergeInto(LibraryManager.library, {
  BattleCitiesFullscreenAvailable: function () {
    var root = document.getElementById('unity-fullscreen-container') || Module['canvas'];
    return root && ((document.fullscreenEnabled && root.requestFullscreen) ||
      (document.webkitFullscreenEnabled && root.webkitRequestFullscreen)) ? 1 : 0;
  },
  BattleCitiesIsMobileBrowser: function () {
    var ua = navigator.userAgent || '';
    // iPadOS can identify itself as a desktop Mac. Touch-capable Windows PCs
    // retain keyboard controls; touch support alone does not identify a phone.
    var ipad = /Macintosh|MacIntel/i.test(ua + ' ' + (navigator.platform || '')) && navigator.maxTouchPoints > 1;
    var mobile = /Android|iPhone|iPad|iPod|Mobile/i.test(ua) || ipad;
    if (mobile && Module['canvas']) Module['canvas'].style.touchAction = 'none';
    return mobile ? 1 : 0;
  },
  BattleCitiesHudPixelRatio: function () {
    var canvas = Module['canvas'];
    if (!canvas) return 1;
    var bounds = canvas.getBoundingClientRect();
    return bounds.width > 0 ? Math.max(1, canvas.width / bounds.width) : 1;
  }
});
