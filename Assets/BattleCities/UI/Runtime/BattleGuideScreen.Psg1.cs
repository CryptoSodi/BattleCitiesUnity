using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BattleCities.UI
{
    public sealed partial class BattleGuideScreen
    {
        public const string Psg1ScreenId="battle-guide-psg1",Psg1PreferenceKey="battlecities.psg1.controls.dismissed.v1";
        static Dictionary<string,string> psgCopy;
        public static Dictionary<string,string> Psg1Copy=>psgCopy??(psgCopy=CreatePsg1Copy());
        static Dictionary<string,string> CreatePsg1Copy()=>new Dictionary<string,string>(Copy){
            ["move-body"]="LEFT STICK / D-PAD\nMove up, down, left and right.",
            ["aim-body"]="RIGHT STICK\nIndependent turret aim.",
            ["fire-body"]="TAP + RELEASE\nFire a normal shot.",
            ["charge-body"]="HOLD 1 SEC + RELEASE\nPower shot adds +2 power.",
            ["special-title"]="SPECIAL ABILITIES",["special-body"]="Tap and release to deploy your tank's special.",
            ["switch-body"]="Hold 1 second to switch. Only available with multiple special abilities.",
            ["powers-title"]="EQUIPPED POWER-UPS",["powers-body"]="A / B / X / Y consume equipped slots 1 / 2 / 3 / 4.",
            ["step-1-title"]="1  FOLLOW MOVEMENT",["step-1-body"]="Without aiming input, the turret follows movement.",
            ["step-2-title"]="2  RELEASE TO SHOOT",["step-2-body"]="Release R1 to fire in your selected aim direction.",
            ["step-3-title"]="3  FOLLOW AGAIN",["step-3-body"]="Your shot keeps its aim. The turret follows movement again after firing.",
            ["deploy-title"]="DEPLOY SPECIAL",["single-special"]="Current tanks each have one special.",
            ["menu-hint"]="D-PAD: SCROLL / SELECT    A: CONFIRM    B: BACK"
        };
        public static readonly string[] Psg1ArtKeys={"stick","dpad","shoulder","face","device","tap","charge","normal-shell","power-shell"};
        Psg1BattleGuideArt psgArt;
        TMP_Text psgMenuHint;
        Sprite PsgArt(string key,Sprite fallback)=>ScreenContent.Sprite(Psg1ScreenId,key,fallback);
        public void KeepControllerFocus()
        { if(IsOpen)Psg1UiNavigation.KeepFocus(transform,proceed); }
        void UpdatePsg1Scrolling()
        {
            var pad=Gamepad.current;if(pad==null)return;
            float axis=pad.dpad.y.ReadValue();
            if(Mathf.Abs(axis)<.35f)axis=pad.leftStick.y.ReadValue();
            ScrollPsg1(axis,Time.unscaledDeltaTime);
        }
        void ScrollPsg1(float axis,float dt)
        {
            if(Mathf.Abs(axis)<.35f)return;
            var scroll=viewport.parent.GetComponent<ScrollRect>();scroll.StopMovement();
            float range=Mathf.Max(0,content.rect.height-viewport.rect.height);
            content.anchoredPosition=new Vector2(0,Mathf.Clamp(content.anchoredPosition.y-axis*480*dt,0,range));
        }

        void BuildPsg1Cards()
        {
            var move=Card("Move","move-title");
            Illustration(move,"stick",psgArt?psgArt.stick:null,new Rect(.04f,.22f,.43f,.42f));
            Illustration(move,"dpad",psgArt?psgArt.dpad:null,new Rect(.55f,.22f,.40f,.42f));
            var or=Label("Or",move,"OR",24);Area(or.rectTransform,new Rect(.45f,.36f,.13f,.13f));or.alignment=TextAlignmentOptions.Center;
            Body(move,"move-body",.67f);
            var aim=Card("Aim","aim-title");
            Illustration(aim,"stick",psgArt?psgArt.stick:null,new Rect(.27f,.23f,.46f,.42f));
            string[] arrows={"↑","←","↓","→"};
            Rect[] areas={new Rect(.40f,.16f,.2f,.14f),new Rect(.13f,.37f,.2f,.14f),new Rect(.40f,.58f,.2f,.14f),new Rect(.67f,.37f,.2f,.14f)};
            for(int i=0;i<4;i++){var arrow=Label("Aim direction "+i,aim,arrows[i],30);arrow.color=new Color32(0,172,237,255);Area(arrow.rectTransform,areas[i]);arrow.alignment=TextAlignmentOptions.Center;}
            Body(aim,"aim-body",.72f);
            PsgShotCard("Fire","fire-title","fire-body","tap",art?art.tap:null,"normal-shell",art?art.normalShell:null);
            PsgShotCard("Power shot","charge-title","charge-body","charge",art?art.charge:null,"power-shell",art?art.powerShell:null);
            turretHeading=Label("Turret heading",content,Text("turret-title"),32).rectTransform;
            for(int i=0;i<3;i++)
            {
                var card=Card("Step "+(i+1),"step-"+(i+1)+"-title");
                var modelBounds=(RectTransform)new GameObject("Tank bounds",typeof(RectTransform)).transform;
                modelBounds.SetParent(card,false);Area(modelBounds,new Rect(.08f,.20f,.84f,.43f));
                var shadowObject=new GameObject("Ground shadow",typeof(RectTransform),typeof(CanvasRenderer),typeof(TankGroundShadow));
                shadowObject.transform.SetParent(modelBounds,false);Area((RectTransform)shadowObject.transform,new Rect(.23f,.66f,.54f,.22f));
                shadowObject.GetComponent<TankGroundShadow>().color=new Color32(35,25,13,112);shadowObject.GetComponent<TankGroundShadow>().raycastTarget=false;
                var picture=(RectTransform)new GameObject("Tank",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter)).transform;
                picture.SetParent(modelBounds,false);Fill(picture);picture.GetComponent<RawImage>().raycastTarget=false;
                var fitter=picture.GetComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fitter.aspectRatio=1.6f;
                tanks.Add(picture.gameObject.AddComponent<BattleGuideTankPreview>());
                var arrow=Label("Movement arrow",card,"→",36);arrow.color=new Color32(0,185,237,255);Area(arrow.rectTransform,new Rect(.76f,.35f,.19f,.22f));
                Body(card,"step-"+(i+1)+"-body",.65f);
            }
            var special=Card("Special","special-title");
            var deploy=Panel("Deploy",special,null);Area(deploy,new Rect(.02f,.25f,.47f,.69f));
            Key(deploy,"L1",new Rect(.02f,.08f,.29f,.42f));
            var deployTitle=Label("Deploy title",deploy,Text("deploy-title"),26);Area(deployTitle.rectTransform,new Rect(.35f,.01f,.63f,.25f));
            var deployBody=Label("Deploy explanation",deploy,Text("special-body"),26);Area(deployBody.rectTransform,new Rect(.35f,.28f,.63f,.68f));
            var change=Panel("Switch",special,null);Area(change,new Rect(.51f,.25f,.47f,.69f));
            Key(change,"L1",new Rect(.02f,.08f,.29f,.42f));
            var switchTitle=Label("Switch title",change,Text("switch-title"),26);Area(switchTitle.rectTransform,new Rect(.35f,.01f,.63f,.25f));
            var switchBody=Label("Switch availability",change,Text("switch-body"),24);Area(switchBody.rectTransform,new Rect(.35f,.28f,.63f,.68f));
            var powers=Card("Powerups","powers-title");
            var shop=RemoteGameArt.Load<ShopArt>("ShopArt");string[] examplePowers={"shield","freeze","speed","upgrade"};
            for(int i=0;i<4;i++)
            {
                string button=new[]{"A","B","X","Y"}[i];
                Key(powers,button,new Rect(.03f+i*.24f,.25f,.10f,.40f));
                Sprite icon=null;
                for(int j=0;j<ShopCatalog.Products.Length;j++)if(ShopCatalog.Products[j].InventoryId==examplePowers[i]&&shop&&j<shop.products.Length)icon=shop.products[j];
                Illustration(powers,"Power example "+i,icon,new Rect(.14f+i*.24f,.25f,.10f,.40f));
            }
            Body(powers,"powers-body",.72f);
            psgMenuHint=Label("Guide controls",footer,Text("menu-hint"),22);
            hideAgain.gameObject.AddComponent<BattleGuideCardFocus>().Configure(hideAgain.transform.Find("Box").GetComponent<Image>());
            foreach(var card in cards)
            {
                foreach(var label in card.GetComponentsInChildren<TMP_Text>())
                { if(label.name=="Description"){label.fontSize=label.fontSizeMax=28;label.fontSizeMin=24;} }
            }
        }
        void PsgShotCard(string name,string heading,string body,string handKey,Sprite hand,string shellKey,Sprite shell)
        {
            var card=Card(name,heading);Key(card,"R1",new Rect(.18f,.21f,.64f,.24f));
            Illustration(card,handKey,hand,new Rect(.10f,.45f,.26f,.25f));
            Illustration(card,shellKey,shell,new Rect(.43f,.46f,.47f,.23f));Body(card,body,.72f);
        }
        void ConfigurePsg1Navigation()
        {
            foreach(var item in new Selectable[]{hideAgain,proceed})
            {
                var other=item==hideAgain?(Selectable)proceed:hideAgain;
                item.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=other,selectOnRight=other,selectOnUp=item,selectOnDown=item};
            }
        }
        void LayoutPsg1(float width,float height)
        {
            float gap=8;int columns=width>=900?4:2;
            float topHeight=width>=1150?Mathf.Clamp(height*.29f,210,255):320,topWidth=(width-gap*(columns-1))/columns;
            for(int i=0;i<4;i++)Place(cards[i],i%columns*(topWidth+gap),i/columns*(topHeight+gap),topWidth,topHeight);
            float y=Mathf.Ceil(4f/columns)*(topHeight+gap);
            float specialHeight=width>=900?156:300;
            Place(cards[7],0,y,width,specialHeight);y+=specialHeight+gap;
            if(width<900)
            {
                Place((RectTransform)cards[7].Find("Deploy"),12,42,width-24,116);
                Place((RectTransform)cards[7].Find("Switch"),12,166,width-24,122);
            }
            float powerHeight=width>=900?164:180;
            Place(cards[8],0,y,width,powerHeight);y+=powerHeight+gap;
            // Keep each face button and its example icon paired on one line.
            var powers=cards[8];float pairWidth=(width-40)/4f;
            float size=Mathf.Min(64,pairWidth*.40f);
            for(int i=0;i<4;i++)
            {
                string button=new[]{"A","B","X","Y"}[i];float x=20+i*pairWidth;
                Place((RectTransform)powers.Find("Key "+button),x,40,size,size);
                Place((RectTransform)powers.Find("Power example "+i),x+size+10,40,size,size);
            }
            Place(turretHeading,6,y,width-12,36);y+=40;
            int steps=width>=1000?3:1;float stepWidth=(width-gap*(steps-1))/steps,stepHeight=width>=1150?200:240;
            for(int i=0;i<3;i++)Place(cards[4+i],i%steps*(stepWidth+gap),y+i/steps*(stepHeight+gap),stepWidth,stepHeight);
            y+=Mathf.Ceil(3f/steps)*(stepHeight+gap);
            foreach(var card in cards)Place((RectTransform)card.Find("Heading"),12,5,card.rect.width-24,32);
            content.sizeDelta=new Vector2(0,y);content.anchoredPosition=new Vector2(0,Mathf.Clamp(content.anchoredPosition.y,0,Mathf.Max(0,y-height)));
            float footerSpace=footer.rect.width*TvStatusFooterLayout.ActionArea.x-36;
            float optWidth=Mathf.Min(330,footerSpace*.44f),lineHeight=footer.rect.height-16;
            Place((RectTransform)hideAgain.transform,18,8,optWidth,lineHeight);
            float checkSize=Mathf.Min(32,optWidth*.15f);
            Place((RectTransform)hideAgain.transform.Find("Box"),0,(lineHeight-checkSize)*.5f,checkSize,checkSize);
            var checkMark=hideAgain.transform.Find("Box/Check").GetComponent<TMP_Text>();
            checkMark.fontSize=checkMark.fontSizeMax=Mathf.Min(22,checkSize*.65f);checkMark.fontSizeMin=12;
            var optLabel=hideAgain.transform.Find("Caption").GetComponent<TMP_Text>();
            Place(optLabel.rectTransform,checkSize+12,0,optWidth-checkSize-12,lineHeight);
            optLabel.fontSizeMin=16;optLabel.textWrappingMode=TextWrappingModes.NoWrap;
            Place(psgMenuHint.rectTransform,18+optWidth+14,8,footerSpace-optWidth-14,lineHeight);
            psgMenuHint.alignment=TextAlignmentOptions.MidlineLeft;psgMenuHint.textWrappingMode=TextWrappingModes.NoWrap;psgMenuHint.fontSizeMin=12;
            ConfigurePsg1Navigation();
        }
    }
    public sealed class BattleGuideCardFocus : MonoBehaviour,ISelectHandler,IDeselectHandler
    {
        CardSelectionHighlight highlight;
        public void Configure(Image image){highlight=CardSelectionHighlight.Ensure(image);highlight.SetState(false,false);}
        public void OnSelect(BaseEventData data){if(highlight)highlight.SetFocused(true);Psg1UiNavigation.Reveal(GetComponent<Selectable>());}
        public void OnDeselect(BaseEventData data){if(highlight)highlight.SetFocused(false);}
        void OnDisable(){if(highlight)highlight.SetFocused(false);}
    }
}
