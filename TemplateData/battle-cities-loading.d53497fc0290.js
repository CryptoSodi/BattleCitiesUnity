(function () {
  'use strict';
  function initialize() {
    var bar = document.getElementById('unity-progress-bar-empty');
    var fill = document.getElementById('unity-progress-bar-full');
    if (!bar || !fill) return;
    var percentage = document.getElementById('unity-progress-percentage');
    if (!percentage) {
      percentage = document.createElement('output');
      percentage.id = 'unity-progress-percentage';
      percentage.setAttribute('aria-hidden', 'true');
      bar.parentNode.insertBefore(percentage, bar.nextSibling);
    }
    function update() {
      var value = parseFloat(fill.style.width);
      if (!Number.isFinite(value)) value = Number(bar.getAttribute('aria-valuenow')) || 0;
      percentage.textContent = Math.floor(Math.max(0, Math.min(100, value))) + '%';
    }
    update();
    var fullscreenRoot = document.getElementById('unity-fullscreen-container');
    var requestFullscreen = fullscreenRoot && (fullscreenRoot.requestFullscreen || fullscreenRoot.webkitRequestFullscreen);
    if (/Android/i.test(navigator.userAgent || '') && requestFullscreen && (document.fullscreenEnabled || document.webkitFullscreenEnabled)) {
      var fullscreen = document.createElement('button');
      fullscreen.id = 'unity-loading-fullscreen';
      fullscreen.type = 'button';
      percentage.insertAdjacentElement('afterend', fullscreen);
      var autoAttempted = false;
      function refreshFullscreen() {
        var active = !!(document.fullscreenElement || document.webkitFullscreenElement);
        if (active) autoAttempted = true;
        fullscreen.textContent = active ? 'EXIT FULLSCREEN' : 'ENTER FULLSCREEN';
        fullscreen.setAttribute('aria-pressed', String(active));
      }
      fullscreen.addEventListener('click', function () {
        autoAttempted = true;
        var active = document.fullscreenElement || document.webkitFullscreenElement;
        var exit = document.exitFullscreen || document.webkitExitFullscreen;
        try {
          // Invoke synchronously inside the trusted tap; never wait for Unity or a download.
          var result = active && exit ? exit.call(document) : requestFullscreen.call(fullscreenRoot);
          if (result && result.catch) result.catch(refreshFullscreen);
        } catch (_) { refreshFullscreen(); }
      });
      document.addEventListener('fullscreenchange', refreshFullscreen);
      document.addEventListener('webkitfullscreenchange', refreshFullscreen);
      fullscreenRoot.addEventListener('click', function (event) {
        if (!event.isTrusted || autoAttempted || event.target === fullscreen) return;
        autoAttempted = true;
        // Let the game's normal input frame run first. A wallet/popup action can
        // consume activation; in that case leave fullscreen to the explicit button.
        window.requestAnimationFrame(function () {
          if (document.fullscreenElement || document.webkitFullscreenElement) return;
          if (navigator.userActivation && !navigator.userActivation.isActive) return;
          try {
            var result = requestFullscreen.call(fullscreenRoot);
            if (result && result.catch) result.catch(refreshFullscreen);
          } catch (_) { refreshFullscreen(); }
        });
      });
      window.BattleCitiesLoadingFullscreen = {
        setStartupVisible: function (visible) {
          if (visible) { fullscreenRoot.appendChild(fullscreen); fullscreen.classList.add('unity-startup-fullscreen'); }
          fullscreen.hidden = !visible;
          refreshFullscreen();
        }
      };
      refreshFullscreen();
    }
    // Observe Unity's actual progress; never advance the label with a timer.
    var observer = new MutationObserver(update);
    observer.observe(fill, { attributes: true, attributeFilter: ['style'] });
    observer.observe(bar, { attributes: true, attributeFilter: ['aria-valuenow'] });
    window.addEventListener('pagehide', function () { observer.disconnect(); }, { once: true });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initialize, { once: true });
  else initialize();
}());
