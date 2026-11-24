using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class ShopScreen
    {
        ShopLiveCatalog liveCatalog;
        JObject walletBalances,checkoutQuote,pendingPayment;
        Button noticeConfirm;
        TMP_Text noticeConfirmText;
        Image purchaseArtwork;
        RawImage purchaseArtworkFallback;
        CheckoutWalletSigner signer;
        Coroutine checkoutRoutine;
        string catalogError,loadedOwner;
        int checkoutIndex=-1,checkoutRevision;
        bool checkoutBusy,currencyChosen,purchaseCardLayout;
        string Owner=>api?.LastPlayer?.id+"|"+api?.LastPlayer?.walletAddress+"|"+api?.BaseUrl;
        string Wallet=>api?.LastPlayer?.walletAddress;
        string PendingKey
        {
            get {using(var hash=SHA256.Create())return "battlecities.shop.payment."+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Owner))).Replace("-","").ToLowerInvariant();}
        }
        void BuildCheckout()
        {
            Fit(noticeCard,ScreenContent.Rect("shop","checkout-region-4",new Rect(.12f,.14f,.76f,.72f)));
            Fit(noticeBody.rectTransform,ScreenContent.Rect("shop","checkout-region-3",new Rect(.07f,.23f,.86f,.50f)));
            purchaseArtwork=Icon("Purchased item artwork",noticeCard,null,new Rect(.20f,.17f,.60f,.30f));
            purchaseArtworkFallback=Raw("Purchased item fallback",noticeCard,new Rect(.20f,.17f,.60f,.30f));
            signer=GetComponent<CheckoutWalletSigner>();if(!signer)signer=gameObject.AddComponent<CheckoutWalletSigner>();
            noticeConfirm=MakeButton("Purchase",noticeCard,"BUY",ConfirmCheckout);
            noticeConfirmText=noticeConfirm.GetComponentInChildren<TMP_Text>();
            Fit((RectTransform)noticeConfirm.transform,ScreenContent.Rect("shop","checkout-region-2",new Rect(.07f,.77f,.55f,.15f)));
            connect.onClick.RemoveAllListeners();connect.onClick.AddListener(()=>
            {
                if(checkoutBusy)return;
                if(api&&api.IsWalletReadyForPurchases){if(accountRequest!=null)StopCoroutine(accountRequest);accountRequest=StartCoroutine(LoadAccount());}
                else if(api)api.ConnectWallet();
            });
            ResetNoticeActions();
        }
        void ResetNoticeActions()
        {
            ResetPurchasePresentation();
            if(noticeConfirm)noticeConfirm.gameObject.SetActive(false);
            if(noticeClose)noticeClose.interactable=true;
            LayoutNoticeButtons();
            checkoutQuote=null;checkoutIndex=-1;
            LinkNoticeButtons();
        }
        void ResetPurchasePresentation()
        {
            purchaseCardLayout=false;
            if(noticeCard)Fit(noticeCard,ScreenContent.Rect("shop","checkout-region-4",new Rect(.12f,.14f,.76f,.72f)));
            if(noticeTitle){Fit(noticeTitle.rectTransform,ScreenContent.Rect("shop","shopscreen-buildnotice-new-rect-07f-07f-86f-19b5bf",new Rect(.07f,.07f,.86f,.16f)));noticeTitle.fontSize=noticeTitle.fontSizeMax=34;}
            if(purchaseArtwork)purchaseArtwork.gameObject.SetActive(false);
            if(purchaseArtworkFallback)purchaseArtworkFallback.transform.parent.gameObject.SetActive(false);
            if(noticeBody)Fit(noticeBody.rectTransform,ScreenContent.Rect("shop","checkout-region-3",new Rect(.07f,.23f,.86f,.50f)));
        }
        void ShowPurchaseCard(JObject receipt,string confirmation)
        {
            noticeTitle.text="PURCHASE CONFIRMED";
            const string prefix="PURCHASE CONFIRMED\n";
            noticeBody.text=confirmation.StartsWith(prefix,StringComparison.Ordinal)?confirmation.Substring(prefix.Length):confirmation;
            ShowItemArtwork(ShopLiveCatalog.Text(receipt?["itemId"]));
        }
        void ShowItemArtwork(string id)
        {
            purchaseCardLayout=true;
            Fit(noticeCard,new Rect(.20f,.05f,.60f,.90f));
            Fit(noticeTitle.rectTransform,new Rect(.07f,.025f,.86f,.11f));
            noticeTitle.fontSize=noticeTitle.fontSizeMax=44;
            int index=Array.FindIndex(ShopCatalog.Products,item=>item.Id==id);
            bool hasArtwork=index>=0&&cards[index]?.Art&&cards[index].Art.sprite;
            bool hasFallback=index>=0&&!hasArtwork&&!string.IsNullOrEmpty(ShopCatalog.Products[index].InventoryId)&&cards[index]?.Fallback&&cards[index].Fallback.texture;
            purchaseArtwork.gameObject.SetActive(hasArtwork);
            purchaseArtworkFallback.transform.parent.gameObject.SetActive(hasFallback);
            if(hasArtwork)SetSprite(purchaseArtwork,cards[index].Art.sprite);
            if(hasFallback){purchaseArtworkFallback.texture=cards[index].Fallback.texture;purchaseArtworkFallback.uvRect=cards[index].Fallback.uvRect;purchaseArtworkFallback.color=Color.white;}
            Fit(noticeBody.rectTransform,hasArtwork||hasFallback?new Rect(.07f,.48f,.86f,.27f):ScreenContent.Rect("shop","checkout-region-3",new Rect(.07f,.23f,.86f,.50f)));
            LayoutNoticeButtons();
        }
        void LayoutNoticeButtons()
        {
            if(!noticeClose||!noticeConfirm)return;
            float textSize=purchaseCardLayout?34f:28f;
            if(noticeConfirmText)noticeConfirmText.fontSize=noticeConfirmText.fontSizeMax=textSize;
            var closeText=noticeClose.GetComponentInChildren<TMP_Text>();
            if(closeText)closeText.fontSize=closeText.fontSizeMax=textSize;
            bool two=noticeConfirm.gameObject.activeSelf;
            if(purchaseCardLayout)
            {
                Fit((RectTransform)noticeConfirm.transform,new Rect(.14f,.80f,.34f,.105f));
                Fit((RectTransform)noticeClose.transform,two?new Rect(.52f,.80f,.34f,.105f):new Rect(.32f,.80f,.36f,.105f));
                Canvas.ForceUpdateCanvases();
                FitNoticeButtonSkins();
            }
            else
            {
                Fit((RectTransform)noticeConfirm.transform,ScreenContent.Rect("shop","checkout-region-2",new Rect(.07f,.77f,.55f,.15f)));
                Fit((RectTransform)noticeClose.transform,two?ScreenContent.Rect("shop","checkout-region-0",new Rect(.66f,.77f,.27f,.15f)):ScreenContent.Rect("shop","checkout-region-1",new Rect(.25f,.77f,.5f,.15f)));
            }
        }
        void FitNoticeButtonSkins()
        {
            if(!purchaseCardLayout)return;
            foreach(var button in new[]{noticeConfirm,noticeClose})
                if(button&&button.image&&button.image.sprite)
                    TvTitleHeaderLayout.FitSkin(button.image,((RectTransform)button.transform).rect.height);
        }
        void LinkNoticeButtons()
        {
            if(!noticeClose)return;
            var other=noticeConfirm&&noticeConfirm.gameObject.activeSelf&&noticeConfirm.interactable?noticeConfirm:noticeClose;
            Link(noticeClose,other,other,noticeClose,noticeClose);
            if(noticeConfirm)Link(noticeConfirm,noticeClose,noticeClose,noticeConfirm,noticeConfirm);
        }
        IEnumerator LoadLiveAccount()
        {
            string owner=Owner;
            if(loadedOwner!=owner){liveCatalog=null;walletBalances=null;account=null;LoadPending();}
            loadedOwner=owner;
            catalogError=null;
            yield return api.Request("GET","/api/economy/account",null,(code,body,error)=>
            {
                if(owner!=Owner)return;
                account=code>=200&&code<300&&(bool?)body?["authenticated"]==true?body?["account"] as JObject:null;
            });
            yield return api.Request("GET","/api/economy/catalog",null,(code,body,error)=>
            {
                if(owner!=Owner)return;
                liveCatalog=null;
                if(code>=200&&code<300&&body!=null){try{liveCatalog=ShopLiveCatalog.Parse(body);}catch{catalogError="Shop catalog is unavailable. Refresh to retry.";}}
                else catalogError="Could not load the Shop. Refresh to retry.";
            });
            walletBalances=null;
            if(owner==Owner&&api.IsWalletAuthenticated)yield return api.Request("GET","/api/economy/wallet-balance",null,(code,body,error)=>
            {if(owner==Owner&&code>=200&&code<300&&(bool?)body?["ok"]==true&&(string)body["walletAddress"]==Wallet)walletBalances=body;});
            if(owner==Owner)
            {
                if(!currencyChosen&&currency!=ShopCurrency.Solana&&!HasAvailablePurchases(currency)&&HasAvailablePurchases(ShopCurrency.Solana))
                    SetCurrency(ShopCurrency.Solana,false);
                RefreshAccount();
            }
        }
        bool HasAvailablePurchases(ShopCurrency value)
        {
            if(liveCatalog==null)return false;
            foreach(var product in ShopCatalog.Products)if(liveCatalog.Available(product.Id,value))return true;
            return false;
        }
        void LoadPending()
        {
            pendingPayment=null;
            if(!api||!api.IsWalletAuthenticated)return;
            try {var value=JObject.Parse(PlayerPrefs.GetString(PendingKey,"{}"));if((string)value["wallet"]==Wallet&&(string)value["api"]==api.BaseUrl
                &&!string.IsNullOrEmpty((string)value["quoteToken"])
                &&((bool?)value["walletSubmitted"]==true||(!string.IsNullOrEmpty((string)value["signature"])&&!string.IsNullOrEmpty((string)value["signedTransaction"]))))pendingPayment=value;}
            catch { /* An invalid receipt cannot authorize a payment. */ }
        }
        void SavePending(){PlayerPrefs.SetString(PendingKey,pendingPayment.ToString(Formatting.None));PlayerPrefs.Save();}
        void ClearPending(){PlayerPrefs.DeleteKey(PendingKey);PlayerPrefs.Save();pendingPayment=null;}
        string LivePrice(string id)
        {
            if(liveCatalog==null)return catalogError==null?"LOADING":"UNAVAILABLE";
            if(id=="season-pass"&&liveCatalog.Owned)return "OWNED";
            if(!liveCatalog.Available(id,currency))return "UNAVAILABLE";
            return DisplayPrice(liveCatalog.Price(id,currency),currency)+" "+PaymentTicker;
        }
        string SeasonCaption()=>liveCatalog==null?"CURRENT SEASON":(liveCatalog.SeasonName??"CURRENT SEASON").Trim().ToUpperInvariant()+
            (liveCatalog.Owned?" • OWNED":" • FULL SEASON");
        void InspectCheckout(int index)
        {
            ResetNoticeActions();LoadPending();checkoutIndex=index;
            var product=ShopCatalog.Products[index];
            if(pendingPayment!=null)
            {
                ShowNotice("CHECK PAYMENT","A previous payment is awaiting confirmation. Check it before buying another item.");
                ConfigureNoticeAction("CHECK PAYMENT");return;
            }
            if(liveCatalog==null){ShowNotice(product.Title,catalogError??"Loading the live Shop. Please try again.");ShowItemArtwork(product.Id);return;}
            string description=product.Category==ShopCategory.SeasonPass?PassDescription():product.Reward;
            if(product.Category==ShopCategory.SeasonPass&&liveCatalog.Owned){ShowNotice("SEASON PASS OWNED",description);ShowItemArtwork(product.Id);return;}
            if(!liveCatalog.Available(product.Id,currency))
            {
                string reason=currency!=ShopCurrency.Solana&&liveCatalog.Available(product.Id,ShopCurrency.Solana)
                    ?PaymentTicker+" purchases are unavailable for this item. Choose SOLANA to buy it.":"This purchase is currently unavailable.";
                ShowNotice(product.Title,description+"\n\n"+reason);ShowItemArtwork(product.Id);return;
            }
            ShowNotice(product.Title,description+"\n\n"+LivePrice(product.Id));
            ShowItemArtwork(product.Id);
            if(api&&api.IsWalletReadyForPurchases)
                checkoutRoutine=StartCoroutine(PrepareQuote());
            else ConfigureNoticeAction("CONNECT WALLET");
        }
        string PassDescription()
        {
            string end="";
            if(DateTimeOffset.TryParse(ShopLiveCatalog.Text(liveCatalog?.Pass?["expiresAt"]),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var expiry))
                end="\nEnds "+expiry.UtcDateTime.ToString("dd MMM yyyy HH:mm 'UTC'",CultureInfo.InvariantCulture)+".";
            return (liveCatalog?.SeasonName??"CURRENT SEASON")+end+"\nIncludes all eligible matches played in this season, including earlier matches. Cycle scores also count without a pass.";
        }
        void ConfigureNoticeAction(string caption)
        {
            noticeConfirm.gameObject.SetActive(true);noticeConfirm.interactable=true;noticeConfirmText.text=caption;
            LayoutNoticeButtons();
            LinkNoticeButtons();Focus(noticeConfirm);
        }
        void ConfirmCheckout()
        {
            if(checkoutBusy)return;
            if(!api||!api.IsWalletAuthenticated){DismissNotice();if(api)api.ConnectWallet();return;}
            if(pendingPayment!=null){checkoutRoutine=StartCoroutine(CheckPending());return;}
            if(!api.IsWalletReadyForPurchases){DismissNotice();api.ConnectWallet();return;}
            if(checkoutQuote!=null){checkoutRoutine=StartCoroutine(SignAndSubmit());return;}
            if(checkoutIndex>=0&&liveCatalog!=null&&liveCatalog.Available(ShopCatalog.Products[checkoutIndex].Id,currency))
                checkoutRoutine=StartCoroutine(PrepareQuote());
        }
        IEnumerator PrepareQuote()
        {
            checkoutQuote=null;checkoutBusy=true;noticeConfirm.interactable=false;
            string owner=Owner;int revision=checkoutRevision;
            JObject result=null;string error=null;
            var product=ShopCatalog.Products[checkoutIndex];
            var payload=new JObject{{"itemId",product.Id},{"currency",PaymentCode},{"walletAddress",Wallet}};
            if(product.Category==ShopCategory.SeasonPass)payload["seasonId"]=liveCatalog.SeasonId;
            noticeBody.text="Preparing the live price and wallet transaction...";
            yield return api.Request("POST","/api/economy/purchase/quote",payload,(code,body,problem)=>
            {if(code>=200&&code<300&&(bool?)body?["ok"]==true)result=body;else error=(string)body?["statusText"]??problem;});
            if(owner!=Owner||revision!=checkoutRevision){FinishCheckout();yield break;}
            bool valid=false;
            string price=null;
            try
            {
                string expected=PaymentCode;
                price=ShopLiveCatalog.FormatAtomic((string)result?["amountAtomic"],currency==ShopCurrency.Solana?9:currency==ShopCurrency.Play?liveCatalog.PlayDecimals:liveCatalog.SkrDecimals);
                var bytes=Convert.FromBase64String((string)result?["transaction"]??"");
                valid=bytes.Length>65&&bytes.Length<=1232&&bytes[0]==1&&(string)result?["currency"]==expected
                    &&(string)result?["network"]==MainMenuApiClient.WalletNetwork&&!string.IsNullOrEmpty((string)result?["quoteToken"])
                    &&QuoteIsCurrent(result)&&(product.Category!=ShopCategory.SeasonPass||(string)result["seasonId"]==liveCatalog.SeasonId);
                if(currency==ShopCurrency.Play)valid&=(string)result?["tokenMint"]=="PLAYs3GSSadH2q2JLS7djp7yzeT75NK78XgrE5YLrfq";
            }
            catch { }
            if(!api.IsWalletReadyForPurchases)
            {
                FinishCheckout();noticeBody.text="Your wallet is locked or disconnected. Connect the wallet used to sign in to continue.";
                ConfigureNoticeAction("CONNECT WALLET");yield break;
            }
            if(!valid){noticeBody.text=error??"The payment quote is unavailable or expired. Please try again.";FinishCheckout();ConfigureNoticeAction("RETRY");yield break;}
            checkoutQuote=result;FinishCheckout();
            noticeBody.text=(product.Category==ShopCategory.SeasonPass?PassDescription():product.Reward)+"\n\n"+price+" "+PaymentTicker+
                " + network fees\nApprove this exact price in your wallet.";
            ConfigureNoticeAction("APPROVE");
        }
        static bool QuoteIsCurrent(JObject quote)=>DateTimeOffset.TryParse(ShopLiveCatalog.Text(quote?["expiresAt"]),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var expires)
            &&expires> DateTimeOffset.UtcNow;
        IEnumerator SignAndSubmit()
        {
            if(!QuoteIsCurrent(checkoutQuote)){checkoutQuote=null;yield return PrepareQuote();yield break;}
            checkoutBusy=true;noticeConfirm.interactable=false;
            var quote=checkoutQuote;string owner=Owner;int revision=checkoutRevision;
            JObject signed=null;
            noticeBody.text="Approve the payment in your wallet. Return to Battle Cities after signing.";
            signer.Sign((string)quote["transaction"],Wallet,ShopLiveCatalog.Text(quote["expiresAt"]),value=>signed=value,"Buy "+ShopCatalog.Products[checkoutIndex].Title,()=>
            {
                pendingPayment=new JObject{{"wallet",Wallet},{"api",api.BaseUrl},{"itemId",ShopCatalog.Products[checkoutIndex].Id},
                    {"quoteToken",quote["quoteToken"]},{"walletSubmitted",true}};
                var purchased=liveCatalog?.PurchaseContents(ShopCatalog.Products[checkoutIndex].Id);
                if(purchased!=null)pendingPayment["contents"]=purchased;
                SavePending();
            });
            while(signed==null&&signer.IsPending&&owner==Owner&&revision==checkoutRevision)yield return null;
            if(owner!=Owner||revision!=checkoutRevision){FinishCheckout();yield break;}
            if((bool?)signed?["ok"]!=true)
            {
                if((string)signed?["errorCode"]=="wallet-not-ready"&&api)api.MarkPurchaseWalletUnavailable();
                if((bool?)pendingPayment?["walletSubmitted"]==true&&(bool?)signed?["definitelyNotSubmitted"]==true)ClearPending();
                if((bool?)pendingPayment?["walletSubmitted"]==true)
                {
                    checkoutQuote=null;noticeBody.text="Checking whether your wallet submitted the payment...";
                    yield return VerifyPending(owner,revision);FinishCheckout();yield break;
                }
                string price=ShopLiveCatalog.FormatAtomic((string)quote["amountAtomic"],currency==ShopCurrency.Solana?9:currency==ShopCurrency.Play?liveCatalog.PlayDecimals:liveCatalog.SkrDecimals);
                // A failed signing attempt must not retry the same transaction:
                // the wallet may have rejected or altered that quote.
                checkoutQuote=null;
                bool ready=api&&api.IsWalletReadyForPurchases;
                noticeBody.text=ready?((string)signed?["error"]??"Wallet signing was cancelled. No payment was submitted.")+"\n\n"+price+" "+PaymentTicker+" + network fees"
                    :((string)signed?["error"]??"Your wallet is locked or disconnected. Reconnect the wallet used to sign in to continue.");
                FinishCheckout();ConfigureNoticeAction(ready?"RETRY":"RECONNECT WALLET");yield break;
            }
            if((bool?)signed["walletSubmitted"]==true)
            {
                pendingPayment["signature"]=signed["signature"];SavePending();checkoutQuote=null;
                yield return VerifyPending(owner,revision);FinishCheckout();yield break;
            }
            // Persist the exact signed bytes before submission. A retry uses the
            // same signature and cannot create another wallet payment.
            pendingPayment=new JObject{{"wallet",Wallet},{"api",api.BaseUrl},{"itemId",ShopCatalog.Products[checkoutIndex].Id},
                {"quoteToken",quote["quoteToken"]},{"signature",signed["signature"]},{"signedTransaction",signed["signedTransaction"]}};
            var contents=liveCatalog?.PurchaseContents(ShopCatalog.Products[checkoutIndex].Id);
            if(contents!=null)pendingPayment["contents"]=contents;
            SavePending();checkoutQuote=null;
            noticeBody.text="Submitting the signed payment...";noticeClose.interactable=false;
            yield return api.Request("POST","/api/economy/purchase/submit",new JObject{{"quoteToken",pendingPayment["quoteToken"]},{"signedTransaction",pendingPayment["signedTransaction"]}},(code,body,error)=>
            {if(owner==Owner)ApplyPaymentResult(body);});
            if(owner==Owner&&revision==checkoutRevision&&pendingPayment!=null)yield return VerifyPending(owner,revision);
            else if(owner==Owner&&revision==checkoutRevision){noticeConfirm.gameObject.SetActive(false);LayoutNoticeButtons();LinkNoticeButtons();yield return LoadLiveAccount();if(api)api.RefreshNow();}
            FinishCheckout();
        }
        IEnumerator CheckPending()
        {
            checkoutBusy=true;noticeConfirm.interactable=false;noticeClose.interactable=false;
            string owner=Owner;int revision=checkoutRevision;
            yield return VerifyPending(owner,revision);
            FinishCheckout();
        }
        IEnumerator VerifyPending(string owner,int revision)
        {
            noticeBody.text="Checking payment confirmation...";
            for(int attempt=0;attempt<4&&pendingPayment!=null&&owner==Owner&&revision==checkoutRevision;attempt++)
            {
                var request=new JObject{{"quoteToken",pendingPayment["quoteToken"]}};
                if((bool?)pendingPayment["walletSubmitted"]==true)request["recover"]=true;
                if(!string.IsNullOrEmpty((string)pendingPayment["signature"]))request["signature"]=pendingPayment["signature"];
                yield return api.Request("POST","/api/economy/purchase/verify",request,(code,body,error)=>
                {if(owner==Owner&&revision==checkoutRevision)ApplyPaymentResult(body);});
                if(pendingPayment!=null&&attempt<3)yield return new WaitForSecondsRealtime(3);
            }
            if(owner!=Owner||revision!=checkoutRevision)yield break;
            if(pendingPayment!=null&&(bool?)pendingPayment["walletSubmitted"]!=true)
            {
                yield return api.Request("POST","/api/economy/purchase/submit",new JObject{{"quoteToken",pendingPayment["quoteToken"]},{"signedTransaction",pendingPayment["signedTransaction"]}},(code,body,error)=>
                {if(owner==Owner&&revision==checkoutRevision)ApplyPaymentResult(body);});
            }
            if(pendingPayment!=null){noticeBody.text="Payment status is still unknown. Check your wallet history, then use CHECK PAYMENT. If it remains pending, contact support before retrying.";ConfigureNoticeAction("CHECK PAYMENT");}
            else {noticeConfirm.gameObject.SetActive(false);LayoutNoticeButtons();LinkNoticeButtons();yield return LoadLiveAccount();if(api)api.RefreshNow();}
        }
        void ApplyPaymentResult(JObject body)
        {
            if((bool?)body?["ok"]==true&&body["account"] is JObject updated)
            {
                string confirmation=PurchaseConfirmation(pendingPayment);
                ShowPurchaseCard(pendingPayment,confirmation);
                account=updated;ClearPending();RefreshAccount();
            }
            else if((bool?)body?["terminal"]==true){ClearPending();noticeBody.text=(string)body["statusText"]??"The payment did not complete.";}
        }
        static string PurchaseConfirmation(JObject receipt)
        {
            string id=ShopLiveCatalog.Text(receipt?["itemId"]);
            ShopProduct product=null;
            foreach(var item in ShopCatalog.Products)if(item.Id==id){product=item;break;}
            if(product==null)return "PURCHASE CONFIRMED\nYour purchase was added to your account.";
            if(product.Category==ShopCategory.SeasonPass)return "PURCHASE CONFIRMED\n1 SEASON PASS";
            var lines=new List<string>();
            var contents=receipt?["contents"] as JObject;
            int fuel=PositiveCount(contents?["fuel"]);
            if(fuel>0)lines.Add(fuel.ToString(CultureInfo.InvariantCulture)+" FUEL");
            if(contents?["inventory"] is JObject inventory)
                foreach(var entry in inventory.Properties())
                {
                    int count=PositiveCount(entry.Value);
                    if(count==0)continue;
                    foreach(var item in ShopCatalog.Products)
                        if(item.InventoryId==entry.Name)
                        {lines.Add(count.ToString(CultureInfo.InvariantCulture)+" "+item.Title);break;}
                }
            if(lines.Count==0)return "PURCHASE CONFIRMED\n"+product.Title;
            return "PURCHASE CONFIRMED\n"+(product.Category==ShopCategory.Packs?product.Title+"\n":"")+string.Join("\n",lines);
        }
        static int PositiveCount(JToken value)
        {
            if(value?.Type!=JTokenType.Integer)return 0;
            return int.TryParse(value.ToString(),NumberStyles.None,CultureInfo.InvariantCulture,out var result)&&result>0?result:0;
        }
        void FinishCheckout(){checkoutBusy=false;checkoutRoutine=null;if(noticeClose)noticeClose.interactable=true;if(noticeConfirm)noticeConfirm.interactable=true;LinkNoticeButtons();}
        void CancelCheckout()
        {
            checkoutRevision++;if(signer)signer.Cancel();if(checkoutRoutine!=null)StopCoroutine(checkoutRoutine);FinishCheckout();ResetNoticeActions();
        }
    }
}
