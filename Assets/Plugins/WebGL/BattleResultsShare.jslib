mergeInto(LibraryManager.library, {
  BattleCitiesResultsShare: function (targetPtr, textPtr) {
    var target = UTF8ToString(targetPtr), text = UTF8ToString(textPtr);
    var reply = function(message) { SendMessage(target, 'OnResultsShared', message); };
    if (navigator.share) {
      navigator.share({title: 'Battle Cities results', text: text})
        .then(function() { reply('RESULTS SHARED'); })
        .catch(function(error) { reply(error.name === 'AbortError' ? 'SHARE CANCELLED' : 'SHARING UNAVAILABLE'); });
    } else if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(function() { reply('RESULTS COPIED'); }).catch(function() { reply('COPY UNAVAILABLE'); });
    } else reply('SHARING UNAVAILABLE');
  }
});
