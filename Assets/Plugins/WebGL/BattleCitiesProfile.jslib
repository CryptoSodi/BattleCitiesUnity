mergeInto(LibraryManager.library, {
  BattleCitiesProfileShare: function(targetPtr,namePtr,urlPtr) {
    const target=UTF8ToString(targetPtr),name=UTF8ToString(namePtr),url=UTF8ToString(urlPtr);
    const reply=value=>SendMessage(target,'OnProfileShareResult',value);
    (async()=>{
      try {
        if(navigator.share){await navigator.share({title:name+' | Battle Cities',text:'View this Battle Cities combat record.',url});reply('shared');}
        else{await navigator.clipboard.writeText(url);reply('copied');}
      }catch(error){reply(error.name==='AbortError'?'cancelled':'error');}
    })();
  }
});
