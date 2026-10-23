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
    // Observe Unity's actual progress; never advance the label with a timer.
    var observer = new MutationObserver(update);
    observer.observe(fill, { attributes: true, attributeFilter: ['style'] });
    observer.observe(bar, { attributes: true, attributeFilter: ['aria-valuenow'] });
    window.addEventListener('pagehide', function () { observer.disconnect(); }, { once: true });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initialize, { once: true });
  else initialize();
}());
