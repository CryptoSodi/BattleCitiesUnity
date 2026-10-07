mergeInto(LibraryManager.library, {
  BattleCitiesReplaySync: function () {
    FS.syncfs(false, function (error) { if (error) console.error('Replay persistence failed', error); });
  }
});
