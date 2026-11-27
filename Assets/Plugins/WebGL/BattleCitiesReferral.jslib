mergeInto(LibraryManager.library, {
  BattleCitiesCopyWalletAddress: function(targetPtr, textPtr, requestPtr) {
    var target=UTF8ToString(targetPtr), text=UTF8ToString(textPtr), id=UTF8ToString(requestPtr);
    var reply=function(result){SendMessage(target,'OnWalletAddressCopyResult',id+':'+result);};
    if (!navigator.clipboard || !navigator.clipboard.writeText) { reply('error'); return; }
    try { navigator.clipboard.writeText(text).then(function(){reply('copied');}).catch(function(){reply('error');}); }
    catch(error) { reply('error'); }
  },
  BattleCitiesCopyReferral: function(targetPtr, textPtr) {
    var target=UTF8ToString(targetPtr), text=UTF8ToString(textPtr);
    var reply=function(result){SendMessage(target,'OnReferralCopyResult',result);};
    if (!navigator.clipboard || !navigator.clipboard.writeText) { reply('error'); return; }
    navigator.clipboard.writeText(text).then(function(){reply('copied');}).catch(function(){reply('error');});
  },
  BattleCitiesPasteReferral: function(targetPtr, requestIdPtr) {
    var target=UTF8ToString(targetPtr), id=UTF8ToString(requestIdPtr);
    var reply=function(ok,text){SendMessage(target,'OnReferralPasteResult',JSON.stringify({id:id,ok:ok,text:text}));};
    if (!navigator.clipboard || !navigator.clipboard.readText) { reply(false,''); return; }
    try { navigator.clipboard.readText().then(function(text){reply(true,text.slice(0,2049));}).catch(function(){reply(false,'');}); }
    catch(error) { reply(false,''); }
  }
});
