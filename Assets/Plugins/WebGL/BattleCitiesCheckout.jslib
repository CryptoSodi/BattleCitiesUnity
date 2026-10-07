mergeInto(LibraryManager.library, {
  BattleCitiesCancelCheckout: function(attemptPtr) {
    const pending=window.__battleCitiesCheckout;
    if(pending?.id===UTF8ToString(attemptPtr))pending.cancel();
  },
  BattleCitiesSignCheckout: function(targetPtr,callbackPtr,attemptPtr,transactionPtr,walletPtr,expiryPtr,scriptPtr) {
    const target=UTF8ToString(targetPtr),callback=UTF8ToString(callbackPtr),id=UTF8ToString(attemptPtr);
    const transaction=UTF8ToString(transactionPtr),wallet=UTF8ToString(walletPtr),expiry=Date.parse(UTF8ToString(expiryPtr));
    const scriptUrl=new URL(UTF8ToString(scriptPtr),window.location.href);
    window.__battleCitiesCheckout?.cancel();
    let done=false;
    const finish=value=>{
      if(done)return;done=true;clearTimeout(timer);
      if(window.__battleCitiesCheckout?.id===id)delete window.__battleCitiesCheckout;
      SendMessage(target,callback,JSON.stringify({...value,attemptId:id}));
    };
    const timer=setTimeout(()=>finish({ok:false,error:'Wallet signing timed out. No payment was submitted.'}),145000);
    window.__battleCitiesCheckout={id,cancel:()=>{done=true;clearTimeout(timer);if(window.__battleCitiesCheckout?.id===id)delete window.__battleCitiesCheckout;}};
    (async()=>{
      try {
        if(scriptUrl.origin!==window.location.origin)throw new Error('Wallet checkout assets are unavailable.');
        if(!window.BattleCitiesCheckoutWallet) {
          await new Promise((resolve,reject)=>{
            const script=document.createElement('script');script.src=scriptUrl.toString();script.onload=resolve;
            script.onerror=()=>reject(new Error('Could not load wallet checkout.'));document.head.appendChild(script);
          });
        }
        if(done)return;
        if(!Number.isFinite(expiry)||Date.now()>=expiry)throw new Error('The price quote expired. Request a fresh price.');
        const signedTransaction=await window.BattleCitiesCheckoutWallet.sign(transaction,wallet);
        finish({ok:true,signedTransaction});
      } catch(error) {finish({ok:false,error:error.message||'Wallet signing failed. No payment was submitted.'});}
    })();
  }
});
