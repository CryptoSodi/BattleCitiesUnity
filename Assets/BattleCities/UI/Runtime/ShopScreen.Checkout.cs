using System;
using System.Collections;
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
        CheckoutWalletSigner signer;
        Coroutine checkoutRoutine;
        string catalogError,loadedOwner;
        int checkoutIndex=-1,checkoutRevision;
        bool checkoutBusy,currencyChosen;
        string Owner=>api?.LastPlayer?.id+"|"+api?.LastPlayer?.walletAddress+"|"+api?.BaseUrl;
        string Wallet=>api?.LastPlayer?.walletAddress;
        string PendingKey
        {
            get {using(var hash=SHA256.Create())return "battlecities.shop.payment."+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Owner))).Replace("-","").ToLowerInvariant();}
        }
        void BuildCheckout()
        {
            Fit(noticeCard,new Rect(.12f,.14f,.76f,.72f));
            Fit(noticeBody.rectTransform,new Rect(.07f,.23f,.86f,.50f));
            signer=GetComponent<CheckoutWalletSigner>();if(!signer)signer=gameObject.AddComponent<CheckoutWalletSigner>();
            noticeConfirm=MakeButton("Purchase",noticeCard,"GET QUOTE",ConfirmCheckout);
            noticeConfirmText=noticeConfirm.GetComponentInChildren<TMP_Text>();
            Fit((RectTransform)noticeConfirm.transform,new Rect(.07f,.77f,.55f,.15f));
            connect.onClick.RemoveAllListeners();connect.onClick.AddListener(()=>
            {
                if(checkoutBusy)return;
                if(api&&api.IsWalletAuthenticated){if(accountRequest!=null)StopCoroutine(accountRequest);accountRequest=StartCoroutine(LoadAccount());}
                else if(api)api.ConnectWallet();
            });
            ResetNoticeActions();
        }
        void ResetNoticeActions()
        {
            if(noticeConfirm)noticeConfirm.gameObject.SetActive(false);
            if(noticeClose){noticeClose.interactable=true;Fit((RectTransform)noticeClose.transform,new Rect(.25f,.77f,.5f,.15f));}
            checkoutQuote=null;checkoutIndex=-1;
            LinkNoticeButtons();
        }
        void LinkNoticeButtons()
        {
            if(!noticeClose)return;
            var other=noticeConfirm&&noticeConfirm.gameObject.activeSelf&&noticeConfirm.interactable?noticeConfirm:noticeClose;
            Link(noticeClose,other,other,other,other);
            if(noticeConfirm)Link(noticeConfirm,noticeClose,noticeClose,noticeClose,noticeClose);
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
                if(!currencyChosen&&currency==ShopCurrency.Skr&&!HasAvailablePurchases(ShopCurrency.Skr)&&HasAvailablePurchases(ShopCurrency.Solana))
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
                &&!string.IsNullOrEmpty((string)value["signature"])&&!string.IsNullOrEmpty((string)value["signedTransaction"]))pendingPayment=value;}
            catch { /* An invalid receipt cannot authorize a payment. */ }
        }
        void SavePending(){PlayerPrefs.SetString(PendingKey,pendingPayment.ToString(Formatting.None));PlayerPrefs.Save();}
        void ClearPending(){PlayerPrefs.DeleteKey(PendingKey);PlayerPrefs.Save();pendingPayment=null;}
        string LivePrice(string id)
        {
            if(liveCatalog==null)return catalogError==null?"LOADING":"UNAVAILABLE";
            if(id=="season-pass"&&liveCatalog.Owned)return "OWNED";
            if(!liveCatalog.Available(id,currency))return "UNAVAILABLE";
            return liveCatalog.Price(id,currency)+(currency==ShopCurrency.Solana?" SOL":" SKR");
        }
        string SeasonCaption()=>liveCatalog==null?"CURRENT SEASON":(liveCatalog.SeasonName??"CURRENT SEASON").ToUpperInvariant()+
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
            if(liveCatalog==null){ShowNotice(product.Title,catalogError??"Loading the live Shop. Please try again.");return;}
            string description=product.Category==ShopCategory.SeasonPass?PassDescription():product.Reward;
            if(product.Category==ShopCategory.SeasonPass&&liveCatalog.Owned){ShowNotice("SEASON PASS OWNED",description);return;}
            if(!liveCatalog.Available(product.Id,currency))
            {
                string reason=currency==ShopCurrency.Skr&&liveCatalog.Available(product.Id,ShopCurrency.Solana)
                    ?"SKR purchases are unavailable for this item. Choose SOLANA to buy it.":"This purchase is currently unavailable.";
                ShowNotice(product.Title,description+"\n\n"+reason);return;
            }
            ShowNotice(product.Title,description+"\n\n"+LivePrice(product.Id));
            ConfigureNoticeAction(api&&api.IsWalletAuthenticated?"GET QUOTE":"CONNECT WALLET");
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
            Fit((RectTransform)noticeClose.transform,new Rect(.66f,.77f,.27f,.15f));
            LinkNoticeButtons();Focus(noticeConfirm);
        }
        void ConfirmCheckout()
        {
            if(checkoutBusy)return;
            if(!api||!api.IsWalletAuthenticated){DismissNotice();if(api)api.ConnectWallet();return;}
            if(pendingPayment!=null){checkoutRoutine=StartCoroutine(CheckPending());return;}
            if(checkoutQuote!=null){checkoutRoutine=StartCoroutine(SignAndSubmit());return;}
            if(checkoutIndex>=0&&liveCatalog!=null&&liveCatalog.Available(ShopCatalog.Products[checkoutIndex].Id,currency))
                checkoutRoutine=StartCoroutine(PrepareQuote());
        }
        IEnumerator PrepareQuote()
        {
            checkoutBusy=true;noticeConfirm.interactable=false;
            string owner=Owner;int revision=checkoutRevision;
            JObject result=null;string error=null;
            var product=ShopCatalog.Products[checkoutIndex];
            var payload=new JObject{{"itemId",product.Id},{"currency",currency==ShopCurrency.Solana?"sol":"skr"},{"walletAddress",Wallet}};
            if(product.Category==ShopCategory.SeasonPass)payload["seasonId"]=liveCatalog.SeasonId;
            noticeBody.text="Preparing the live price and wallet transaction...";
            yield return api.Request("POST","/api/economy/purchase/quote",payload,(code,body,problem)=>
            {if(code>=200&&code<300&&(bool?)body?["ok"]==true)result=body;else error=(string)body?["statusText"]??problem;});
            if(owner!=Owner||revision!=checkoutRevision){FinishCheckout();yield break;}
            bool valid=false;
            string price=null;
            try
            {
                string expected=currency==ShopCurrency.Solana?"sol":"skr";
                price=ShopLiveCatalog.FormatAtomic((string)result?["amountAtomic"],currency==ShopCurrency.Solana?9:liveCatalog.SkrDecimals);
                var bytes=Convert.FromBase64String((string)result?["transaction"]??"");
                valid=bytes.Length>65&&bytes.Length<=1232&&bytes[0]==1&&(string)result?["currency"]==expected
                    &&(string)result?["network"]=="mainnet-beta"&&!string.IsNullOrEmpty((string)result?["quoteToken"])
                    &&QuoteIsCurrent(result)&&(product.Category!=ShopCategory.SeasonPass||(string)result["seasonId"]==liveCatalog.SeasonId);
            }
            catch { }
            if(!valid){noticeBody.text=error??"The payment quote is unavailable or expired. Refresh the Shop and try again.";FinishCheckout();noticeConfirm.gameObject.SetActive(false);LinkNoticeButtons();yield break;}
            checkoutQuote=result;FinishCheckout();
            noticeBody.text=(product.Category==ShopCategory.SeasonPass?PassDescription():product.Reward)+"\n\n"+price+" "+(currency==ShopCurrency.Solana?"SOL":"SKR")+
                " + network fees\nApprove this exact price in your wallet.";
            ConfigureNoticeAction("APPROVE "+price+" "+(currency==ShopCurrency.Solana?"SOL":"SKR"));
        }
        static bool QuoteIsCurrent(JObject quote)=>DateTimeOffset.TryParse(ShopLiveCatalog.Text(quote?["expiresAt"]),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var expires)
            &&expires> DateTimeOffset.UtcNow;
        IEnumerator SignAndSubmit()
        {
            if(!QuoteIsCurrent(checkoutQuote)){checkoutQuote=null;noticeBody.text="This quote expired. Request a fresh price.";ConfigureNoticeAction("GET QUOTE");yield break;}
            checkoutBusy=true;noticeConfirm.interactable=false;
            var quote=checkoutQuote;string owner=Owner;int revision=checkoutRevision;
            JObject signed=null;
            noticeBody.text="Approve the payment in your wallet. Return to Battle Cities after signing.";
            signer.Sign((string)quote["transaction"],Wallet,ShopLiveCatalog.Text(quote["expiresAt"]),value=>signed=value);
            while(signed==null&&signer.IsPending&&owner==Owner&&revision==checkoutRevision)yield return null;
            if(owner!=Owner||revision!=checkoutRevision){FinishCheckout();yield break;}
            if((bool?)signed?["ok"]!=true)
            {
                noticeBody.text=(string)signed?["error"]??"Wallet signing was cancelled. No payment was submitted.";
                FinishCheckout();ConfigureNoticeAction("APPROVE PAYMENT");yield break;
            }
            // Persist the exact signed bytes before submission. A retry uses the
            // same signature and cannot create another wallet payment.
            pendingPayment=new JObject{{"wallet",Wallet},{"api",api.BaseUrl},{"itemId",ShopCatalog.Products[checkoutIndex].Id},
                {"quoteToken",quote["quoteToken"]},{"signature",signed["signature"]},{"signedTransaction",signed["signedTransaction"]}};
            SavePending();checkoutQuote=null;
            noticeBody.text="Submitting the signed payment...";noticeClose.interactable=false;
            yield return api.Request("POST","/api/economy/purchase/submit",new JObject{{"quoteToken",pendingPayment["quoteToken"]},{"signedTransaction",pendingPayment["signedTransaction"]}},(code,body,error)=>
            {if(owner==Owner)ApplyPaymentResult(body);});
            if(owner==Owner&&revision==checkoutRevision&&pendingPayment!=null)yield return VerifyPending(owner,revision);
            else if(owner==Owner&&revision==checkoutRevision){noticeConfirm.gameObject.SetActive(false);yield return LoadLiveAccount();if(api)api.RefreshNow();}
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
                yield return api.Request("POST","/api/economy/purchase/verify",new JObject{{"quoteToken",pendingPayment["quoteToken"]},{"signature",pendingPayment["signature"]}},(code,body,error)=>
                {if(owner==Owner&&revision==checkoutRevision)ApplyPaymentResult(body);});
                if(pendingPayment!=null&&attempt<3)yield return new WaitForSecondsRealtime(3);
            }
            if(owner!=Owner||revision!=checkoutRevision)yield break;
            if(pendingPayment!=null)
            {
                yield return api.Request("POST","/api/economy/purchase/submit",new JObject{{"quoteToken",pendingPayment["quoteToken"]},{"signedTransaction",pendingPayment["signedTransaction"]}},(code,body,error)=>
                {if(owner==Owner&&revision==checkoutRevision)ApplyPaymentResult(body);});
            }
            if(pendingPayment!=null){noticeBody.text="Payment confirmation is pending. CHECK PAYMENT retries this receipt without making another payment.";ConfigureNoticeAction("CHECK PAYMENT");}
            else {noticeConfirm.gameObject.SetActive(false);yield return LoadLiveAccount();if(api)api.RefreshNow();}
        }
        void ApplyPaymentResult(JObject body)
        {
            if((bool?)body?["ok"]==true&&body["account"] is JObject updated)
            {
                account=updated;ClearPending();noticeBody.text="PURCHASE CONFIRMED\nYour inventory and season-pass ownership are updated.";RefreshAccount();
            }
            else if((bool?)body?["terminal"]==true){ClearPending();noticeBody.text=(string)body["statusText"]??"The payment did not complete.";}
        }
        void FinishCheckout(){checkoutBusy=false;checkoutRoutine=null;if(noticeClose)noticeClose.interactable=true;if(noticeConfirm)noticeConfirm.interactable=true;LinkNoticeButtons();}
        void CancelCheckout()
        {
            checkoutRevision++;if(signer)signer.Cancel();if(checkoutRoutine!=null)StopCoroutine(checkoutRoutine);FinishCheckout();ResetNoticeActions();
        }
    }
}
