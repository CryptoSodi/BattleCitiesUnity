using System;
using System.Collections;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        RectTransform homeFuelIndicator;
        Text homeFuelText;
        Image homeFuelTrack, homeFuelFill;
        string homeFuelOwner;
        bool homeFuelKnown, homeFuelLoading;
        int homeFuelBalance, homeFuelCap, homeFuelIntervalSeconds, homeFuelLastSecond = -1;
        double homeFuelNextAt, homeFuelNextRequest;

        void LayoutFuelRefill()
        {
            if(!howItWorks||!content||!mainFrame)return;
            if(!homeFuelIndicator)
            {
                var existing=content.Find("Fuel refill indicator") as RectTransform;
                var go=existing?existing.gameObject:new GameObject("Fuel refill indicator",typeof(RectTransform),typeof(Image));
                go.layer=content.gameObject.layer;homeFuelIndicator=(RectTransform)go.transform;homeFuelIndicator.SetParent(content,false);
                var icon=EnsurePanelImage(homeFuelIndicator,"Fuel icon");
                var art=RemoteGameArt.Load<PreBattleArt>("PreBattleArt");
                icon.sprite=art?art.fuelCan:null;icon.preserveAspect=true;icon.type=Image.Type.Simple;
                icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=new Vector2(0f,.5f);
                icon.rectTransform.pivot=new Vector2(0f,.5f);icon.rectTransform.anchoredPosition=new Vector2(4f,0f);icon.rectTransform.sizeDelta=new Vector2(36f,36f);
                homeFuelText=PrizeText(homeFuelIndicator,"Label","FUEL —",26);
                homeFuelText.alignment=TextAnchor.MiddleLeft;
                homeFuelText.resizeTextMinSize=18;homeFuelText.raycastTarget=false;
                homeFuelText.rectTransform.anchorMin=Vector2.zero;homeFuelText.rectTransform.anchorMax=Vector2.one;
                homeFuelText.rectTransform.offsetMin=new Vector2(42f,8f);homeFuelText.rectTransform.offsetMax=new Vector2(-8f,0f);
                homeFuelTrack=EnsurePanelImage(homeFuelIndicator,"Fuel progress");
                var trackRect=homeFuelTrack.rectTransform;
                trackRect.anchorMin=new Vector2(0f,0f);trackRect.anchorMax=new Vector2(1f,0f);
                trackRect.offsetMin=new Vector2(42f,3f);trackRect.offsetMax=new Vector2(-8f,7f);
                homeFuelFill=EnsurePanelImage(trackRect,"Fill");
                homeFuelFill.rectTransform.anchorMin=Vector2.zero;homeFuelFill.rectTransform.anchorMax=new Vector2(0f,1f);
                homeFuelFill.rectTransform.offsetMin=homeFuelFill.rectTransform.offsetMax=Vector2.zero;
                homeFuelTrack.gameObject.SetActive(false);
            }
            var heading=howItWorks.Find("Heading") as RectTransform;
            var headingSkin=heading?heading.GetComponent<Image>():null;
            var background=homeFuelIndicator.GetComponent<Image>();
            background.sprite=headingSkin&&headingSkin.sprite?headingSkin.sprite:(theme?theme.Rounded:null);
            background.type=headingSkin?headingSkin.type:Image.Type.Sliced;
            background.color=headingSkin?headingSkin.color:new Color32(255,210,49,255);
            background.pixelsPerUnitMultiplier=headingSkin?headingSkin.pixelsPerUnitMultiplier:1f;
            background.material=headingSkin?headingSkin.material:null;background.raycastTarget=false;
            homeFuelTrack.sprite=homeFuelFill.sprite=theme?theme.Rounded:null;
            homeFuelTrack.type=homeFuelFill.type=Image.Type.Sliced;
            homeFuelTrack.color=new Color32(6,29,54,255);homeFuelFill.color=new Color32(0,183,255,255);
            ApplyPrizeAmountTextStyle(homeFuelText);
            var frameCorners=new Vector3[4];mainFrame.GetWorldCorners(frameCorners);
            var frameMin=content.InverseTransformPoint(frameCorners[0]);var frameMax=content.InverseTransformPoint(frameCorners[2]);
            var infoCorners=new Vector3[4];howItWorks.GetWorldCorners(infoCorners);
            var infoLeft=content.InverseTransformPoint(infoCorners[1]);var infoRight=content.InverseTransformPoint(infoCorners[2]);
            bool infoVisible=howItWorks.gameObject.activeSelf;
            float width=Mathf.Min(225f,frameMax.x-frameMin.x-32f);
            float right=frameMax.x-16f,bottom=frameMin.y+42f,height=38f;
            if(infoVisible&&heading)
            {
                var headingCorners=new Vector3[4];heading.GetWorldCorners(headingCorners);
                var headingMin=content.InverseTransformPoint(headingCorners[0]);var headingMax=content.InverseTransformPoint(headingCorners[2]);
                right=infoRight.x-Mathf.Max(0f,headingMin.x-infoLeft.x);
                width=Mathf.Min(width,Mathf.Max(150f,right-headingMax.x-12f));
                bottom=headingMin.y;height=headingMax.y-headingMin.y;
            }
            Place(homeFuelIndicator,right-width-content.rect.xMin,content.rect.yMax-(bottom+height),width,height);
            homeFuelIndicator.localScale=Vector3.one;homeFuelIndicator.SetAsLastSibling();
        }

        void ResetFuelRefill()
        {
            homeFuelOwner=null;homeFuelKnown=false;homeFuelLoading=false;homeFuelNextRequest=0;homeFuelLastSecond=-1;
            if(homeFuelText)homeFuelText.text="FUEL —";
            if(homeFuelTrack)homeFuelTrack.gameObject.SetActive(false);
        }

        void SetFuelProgress(float progress)
        {
            if(!homeFuelTrack||!homeFuelFill)return;
            progress=Mathf.Clamp01(progress);homeFuelTrack.gameObject.SetActive(true);
            homeFuelFill.rectTransform.anchorMax=new Vector2(progress,1f);homeFuelFill.enabled=progress>0f;
            FitProgressCorners(homeFuelTrack);FitProgressCorners(homeFuelFill);
        }

        void UpdateFuelRefill()
        {
            if(!homeFuelText||!apiClient)return;
            var owner=apiClient.IsAuthenticated?apiClient.LastPlayer?.id:null;
            if(string.IsNullOrEmpty(owner)){if(homeFuelOwner!=null)ResetFuelRefill();return;}
            if(homeFuelOwner!=owner){ResetFuelRefill();homeFuelOwner=owner;}
            double now=Time.realtimeSinceStartupAsDouble;
            if(!homeFuelLoading&&now>=homeFuelNextRequest){homeFuelLoading=true;homeFuelNextRequest=now+30;StartCoroutine(LoadFuelRefill(owner));}
            if(!homeFuelKnown)return;
            int seconds=Mathf.Max(0,(int)Math.Ceiling(homeFuelNextAt-now));
            if(seconds==homeFuelLastSecond)return;
            homeFuelLastSecond=seconds;
            homeFuelText.text=homeFuelBalance>=homeFuelCap
                ?"TANKS FULL"
                :string.Format(CultureInfo.InvariantCulture,"+1 FUEL IN {0:00}:{1:00}",seconds/60,seconds%60);
            SetFuelProgress(homeFuelBalance>=homeFuelCap?1f:1f-(float)((homeFuelNextAt-now)/homeFuelIntervalSeconds));
        }

        IEnumerator LoadFuelRefill(string owner)
        {
            JObject body=null;string error=null;
            yield return apiClient.Request("GET","/api/economy/account",null,(code,json,err)=>{if(code>=200&&code<300)body=json;error=err;});
            if(homeFuelOwner!=owner||!apiClient.IsAuthenticated||apiClient.LastPlayer?.id!=owner)yield break;
            homeFuelLoading=false;
            var account=body?["account"] as JObject;var refill=account?["fuelRefill"] as JObject;
            if(error!=null||(string)account?["playerId"]!=owner||refill==null||
               !DateTimeOffset.TryParse((string)refill["serverTime"],CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var serverTime)||
               !DateTimeOffset.TryParse((string)refill["nextAt"],CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var nextAt)||
               account["fuelBalance"]?.Type!=JTokenType.Integer||refill["cap"]?.Type!=JTokenType.Integer||
               refill["intervalSeconds"]?.Type!=JTokenType.Integer||(long)refill["intervalSeconds"]<=0||(long)refill["intervalSeconds"]>86400)
            {homeFuelKnown=false;homeFuelText.text="FUEL UNAVAILABLE";if(homeFuelTrack)homeFuelTrack.gameObject.SetActive(false);yield break;}
            homeFuelBalance=Math.Max(0,(int)account["fuelBalance"]);homeFuelCap=Math.Max(1,(int)refill["cap"]);
            homeFuelIntervalSeconds=(int)refill["intervalSeconds"];
            double now=Time.realtimeSinceStartupAsDouble;
            double remaining=Math.Max(0,(nextAt-serverTime).TotalSeconds);
            homeFuelNextAt=now+remaining;homeFuelKnown=true;homeFuelLastSecond=-1;
            homeFuelNextRequest=now+(homeFuelBalance>=homeFuelCap?30:Math.Min(30,Math.Max(2,remaining+.5)));
        }
    }
}
