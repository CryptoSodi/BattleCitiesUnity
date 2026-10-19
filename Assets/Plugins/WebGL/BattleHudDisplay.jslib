mergeInto(LibraryManager.library, {
  BattleCitiesHudPixelRatio: function () {
    var canvas = Module['canvas'];
    if (!canvas) return 1;
    var bounds = canvas.getBoundingClientRect();
    return bounds.width > 0 ? Math.max(1, canvas.width / bounds.width) : 1;
  }
});
