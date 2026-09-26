using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>TV-contained tank selection and server-confirmed deployment/loadout flow.</summary>
    public sealed class PreBattleScreen : MonoBehaviour
    {
        static readonly string[] Names={"VANGUARD","STRIKER","TWIN FANG","SIEGEBREAKER"};
        static readonly string[] Roles={"BALANCED CHASSIS","HIGH VELOCITY","RAPID FIRE","HEAVY SHELLS"};
        static readonly string[] Items={"shield","base-defence","freeze","extra-life","upgrade","zoom-out","wipeout","speed"};
        static readonly string[] ItemNames={"SHIELD","BASE DEFENCE","FREEZE","EXTRA LIFE","UPGRADE","ZOOM OUT","WIPEOUT","SPEED"};
        static readonly string[] Slots={"active-one","active-two","active-three","active-four"};
        MenuTheme theme; MainMenuApiClient api; Action launch; Button home;
        RectTransform root,tankPage,loadoutPage;
        TMP_FontAsset font;
        TMP_Text title,status,fuel,continueLabel,wallet;
        readonly List<Button> selectable=new List<Button>();
        readonly Button[] tankButtons=new Button[4],slotButtons=new Button[4];
        readonly TMP_Text[] slotNames=new TMP_Text[4],slotEmpty=new TMP_Text[4],inventoryLabels=new TMP_Text[8];
        readonly RawImage[] slotIcons=new RawImage[4];
        Button back,refresh,proceed;
        PreBattleArt art;
        JObject account,pendingFuel,loadout=new JObject();
        int selected,paidTier=-1;
        bool busy,inLoadout;
        public bool IsOpen=>root&&root.gameObject.activeSelf;
        public void Configure(RectTransform frame,MenuTheme skin,MainMenuApiClient client,Action onLaunch,Button returnTo)
        {
            theme=skin;api=client;launch=onLaunch;home=returnTo;
            art=Resources.Load<PreBattleArt>("PreBattleArt");
            font=TMP_FontAsset.CreateFontAsset(theme.HeadingFont);
            root=Panel("Pre-battle screens",frame,theme.CreamPanel,new Rect(.025f,.035f,.95f,.93f));
            root.GetComponent<Image>().raycastTarget=true;
            root.gameObject.AddComponent<RectMask2D>();
            var heading=Panel("Heading",root,theme.BlueFrame,new Rect(.01f,.01f,.98f,.095f));
            title=Label("Title",heading,"SELECT TANK",new Rect(.025f,0,.64f,1),34,theme.Gold);
            back=Button("Back",heading,"< BACK",new Rect(.76f,.08f,.23f,.84f),Back);
            back.image.sprite=theme.BlueFrame;back.GetComponentInChildren<TMP_Text>().color=Color.white;
            var balance=Panel("Fuel summary",root,theme.DarkPanel,new Rect(.015f,.12f,.97f,.095f));
            fuel=Label("Fuel",balance,"FUEL AVAILABLE  --",new Rect(.02f,0,.70f,1),27,theme.Gold);
            refresh=Button("Refresh",balance,"REFRESH",new Rect(.77f,.08f,.22f,.84f),()=>{if(!busy)StartCoroutine(Refresh());});
            refresh.image.sprite=theme.BlueFrame;refresh.GetComponentInChildren<TMP_Text>().color=Color.white;
            tankPage=Panel("Tank roster",root,null,new Rect(.015f,.23f,.97f,.61f));
            for(int i=0;i<4;i++)
            {
                int index=i;
                var b=Button(Names[i],tankPage,"",new Rect(i*.25f+.006f,0,.238f,1),()=>SelectTank(index));
                tankButtons[i]=b;
                Label("Name",b.transform,Names[i],new Rect(.025f,.025f,.95f,.10f),26,theme.Navy);
                var picture=Raw("Tank model",b.transform,new Rect(.04f,.14f,.92f,.46f));
                picture.texture=art&&art.tanks!=null&&i<art.tanks.Length?art.tanks[i]:null;
                picture.color=picture.texture?Color.white:Color.clear;
                Label("Role",b.transform,Roles[i],new Rect(.04f,.60f,.92f,.08f),19,theme.Navy);
                Label("Specifications",b.transform,"VELOCITY  "+(i==0?"STANDARD":"HIGH")+"\nFIRE RATE  "+(i<2?"STANDARD":"RAPID")+"\nCHASSIS  "+(i+1),new Rect(.06f,.69f,.88f,.17f),18,theme.Navy);
                Label("Tank tier",b.transform,"TIER "+(i+1),new Rect(.06f,.88f,.88f,.09f),27,theme.Navy);
            }
            loadoutPage=Panel("Loadout",root,null,new Rect(.015f,.23f,.97f,.61f));
            var inventory=Panel("Wallet and inventory",loadoutPage,theme.DarkPanel,new Rect(0,0,.25f,1));
            wallet=Label("Wallet",inventory,"WALLET\nCONNECT TO LOAD INVENTORY",new Rect(.04f,.02f,.92f,.25f),23,Color.white);
            for(int i=0;i<Items.Length;i++)
            {
                float x=(i%2)*.5f,y=.30f+(i/2)*.17f;
                var image=Raw(Items[i],inventory,new Rect(x+.10f,y,.30f,.10f));SetIcon(image,Items[i]);
                inventoryLabels[i]=Label("Count",inventory,"0",new Rect(x+.02f,y+.10f,.46f,.055f),19,theme.Gold);
            }
            for(int i=0;i<4;i++)
            {
                int index=i;
                var b=Button("Slot "+(i+1),loadoutPage,"",new Rect(.27f+(i%2)*.365f,(i/2)*.51f,.35f,.49f),()=>CycleSlot(index));
                slotButtons[i]=b;
                Label("Slot heading",b.transform,"SLOT 0"+(i+1),new Rect(.05f,.03f,.90f,.12f),24,theme.Navy);
                slotIcons[i]=Raw("Equipped item",b.transform,new Rect(.30f,.19f,.40f,.43f));
                slotEmpty[i]=Label("Empty slot",b.transform,"+",new Rect(.30f,.19f,.40f,.43f),72,new Color(.25f,.38f,.48f));
                slotNames[i]=Label("Item name",b.transform,"EMPTY",new Rect(.03f,.65f,.94f,.13f),25,theme.Navy);
                Label("Change",b.transform,"CHANGE",new Rect(.08f,.81f,.84f,.14f),24,new Color(.03f,.32f,.65f));
            }
            status=Label("Status",root,"CHOOSE YOUR CHASSIS",new Rect(.02f,.85f,.96f,.05f),19,theme.Navy);
            proceed=Button("Continue",root,"CONTINUE",new Rect(.29f,.915f,.42f,.075f),Continue);
            continueLabel=proceed.GetComponentInChildren<TMP_Text>();
            proceed.image.sprite=theme.GoldPanel;
            root.gameObject.SetActive(false);
        }
        public void Open()
        {
            root.gameObject.SetActive(true);root.SetAsLastSibling();
            ShowPage(false);SelectTank(selected);
            status.text="SELECT A TANK, THEN CONTINUE TO BATTLE";
            Focus(tankButtons[selected]);
        }
        public void Back()
        {
            if(busy)return;
            if(inLoadout){ShowPage(false);status.text="FUEL PAID — CONTINUE RETURNS TO YOUR LOADOUT";Focus(proceed);return;}
            root.gameObject.SetActive(false);Focus(home);
        }
        void ShowPage(bool loadoutView)
        {
            inLoadout=loadoutView;tankPage.gameObject.SetActive(!inLoadout);loadoutPage.gameObject.SetActive(inLoadout);
            title.text=inLoadout?"BATTLE LOADOUT":"SELECT TANK";continueLabel.text=inLoadout?"START BATTLE":"CONTINUE";
            UpdateView();Navigation();
        }
        void SelectTank(int index)
        {
            if(busy||pendingFuel!=null||paidTier>=0)return;
            selected=index;UpdateView();Focus(tankButtons[index]);
        }
        void UpdateView()
        {
            fuel.text="SELECTED TANK   "+Names[selected]+"     /     READY TO DEPLOY";
            for(int i=0;i<4;i++)
            {
                tankButtons[i].image.sprite=i==selected?theme.GoldPanel:theme.CreamPanel;
                tankButtons[i].interactable=!busy&&paidTier<0&&pendingFuel==null;
                string item=(string)loadout[Slots[i]];int at=Array.IndexOf(Items,item);
                slotNames[i].text=at>=0?ItemNames[at]:"EMPTY";SetIcon(slotIcons[i],item);
                slotEmpty[i].gameObject.SetActive(at<0);
                slotButtons[i].interactable=!busy;
            }
            wallet.text="WALLET\n"+(account==null?"NOT CONNECTED":"CONNECTED")+"\nBATC  "+Number(account?["tokenBalance"])+"\nSOL  "+Number(account?["solBalance"]);
            for(int i=0;i<8;i++)inventoryLabels[i].text=Number(account?["inventory"]?[Items[i]]);
            back.interactable=!busy;refresh.interactable=!busy&&pendingFuel==null;
            proceed.interactable=true;
            Navigation();
        }
        static string Number(JToken value)=>value==null?"0":value.ToString();
        IEnumerator Refresh()
        {
            busy=true;status.text="LOADING SHOP INVENTORY...";UpdateView();
            yield return api.Request("GET","/api/economy/account",null,(code,body,error)=>
            {
                if(code>=200&&code<300&&(bool?)body?["authenticated"]==true&&body["account"] is JObject data)
                { account=data;loadout=(JObject)(data["loadout"]?.DeepClone()??new JObject());status.text="SELECT A TANK, THEN CONTINUE"; }
                else {account=null;status.text=code==401||(bool?)body?["authenticated"]==false?"CONNECT YOUR WALLET TO USE SHOP FUEL":"SHOP UNAVAILABLE — SELECT REFRESH TO RETRY";}
            });
            busy=false;UpdateView();
            Focus(inLoadout?slotButtons[0]:paidTier>=0?proceed:tankButtons[selected]);
        }
        void Continue()
        {
            // Direct play must not depend on wallet authentication or shop availability.
            // MainMenuScene owns the scene-loading guard, including repeated clicks.
            StopAllCoroutines();busy=false;
            BattlePreparation.Set(selected,api.BaseUrl);
            status.text="DEPLOYING...";
            launch?.Invoke();
        }
        IEnumerator ConfirmFuel()
        {
            busy=true;status.text="CONFIRMING FUEL...";UpdateView();
            // Preserve the exact balance target across retries: a timed-out PUT must not deduct again.
            if(pendingFuel==null)
            {
                yield return api.Request("GET","/api/economy/account",null,(code,body,error)=>
                {account=code>=200&&code<300&&(bool?)body?["authenticated"]==true?body["account"] as JObject:null;});
                int available=(int?)account?["fuelBalance"]??0;
                if(account==null||available<selected+1)
                {busy=false;status.text=account==null?"SHOP UNAVAILABLE — REFRESH AND RETRY":"NEED "+(selected+1)+" FUEL — VISIT THE SHOP";UpdateView();yield break;}
                pendingFuel=new JObject{{"fuelBalance",available-selected-1},{"loadout",account["loadout"]?.DeepClone()??new JObject()}};
            }
            bool success=false;
            yield return api.Request("PUT","/api/economy/account",new JObject{{"account",pendingFuel.DeepClone()}},(code,body,error)=>
            {
                if(code>=200&&code<300&&(bool?)body?["authenticated"]==true&&body["account"] is JObject data&&
                    (int?)data["fuelBalance"]==(int?)pendingFuel["fuelBalance"])
                {account=data;loadout=(JObject)(data["loadout"]?.DeepClone()??new JObject());paidTier=selected;pendingFuel=null;success=true;}
                else status.text="FUEL NOT CONFIRMED — CONTINUE RETRIES THE SAME REQUEST";
            });
            busy=false;
            if(success){ShowPage(true);status.text="FUEL CONFIRMED — CHOOSE UP TO FOUR OWNED POWERS";Focus(slotButtons[0]);}
            UpdateView();
        }
        void CycleSlot(int index)
        {
            if(busy||account==null)return;
            var choices=new List<string>{null};
            foreach(var item in Items)
            {
                // Extra lives are inventory, not an API-supported active power-up.
                if(item=="extra-life")continue;
                bool used=false;for(int j=0;j<4;j++)if(j!=index&&(string)loadout[Slots[j]]==item)used=true;
                if(!used&&((int?)account["inventory"]?[item]??0)>0)choices.Add(item);
            }
            int next=(choices.IndexOf((string)loadout[Slots[index]])+1)%choices.Count;
            if(choices[next]==null)loadout.Remove(Slots[index]);else loadout[Slots[index]]=choices[next];
            status.text=choices.Count==1?"NO UNEQUIPPED ITEMS OWNED — EMPTY LOADOUT IS ALLOWED":"LOADOUT UPDATED";UpdateView();
        }
        IEnumerator SaveLoadoutAndLaunch()
        {
            if(paidTier<0)yield break;
            busy=true;status.text="SAVING LOADOUT...";UpdateView();bool saved=false;
            yield return api.Request("PUT","/api/economy/account",new JObject{{"account",new JObject{{"loadout",loadout.DeepClone()}}}},(code,body,error)=>
            {
                if(code>=200&&code<300&&(bool?)body?["authenticated"]==true&&body["account"] is JObject data)
                {account=data;saved=JToken.DeepEquals(loadout,data["loadout"]);}
            });
            busy=false;
            if(saved){BattlePreparation.Set(paidTier,api.BaseUrl);status.text="DEPLOYING...";launch?.Invoke();}
            else status.text="LOADOUT NOT SAVED — RETRY START BATTLE";
            UpdateView();
        }
        void Navigation()
        {
            selectable.Clear();selectable.Add(back);selectable.Add(refresh);
            var cards=inLoadout?slotButtons:tankButtons;
            bool editable=inLoadout||(paidTier<0&&pendingFuel==null);
            if(editable)selectable.AddRange(cards);selectable.Add(proceed);
            for(int i=0;i<selectable.Count;i++)
            {
                var nav=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit};
                nav.selectOnUp=selectable[(i+selectable.Count-1)%selectable.Count];nav.selectOnDown=selectable[(i+1)%selectable.Count];
                nav.selectOnLeft=nav.selectOnUp;nav.selectOnRight=nav.selectOnDown;selectable[i].navigation=nav;
            }
            for(int i=0;editable&&i<4;i++)
            {
                var n=cards[i].navigation;n.selectOnUp=inLoadout&&i>=2?cards[i-2]:back;n.selectOnDown=inLoadout&&i<2?cards[i+2]:proceed;
                n.selectOnLeft=cards[(i+3)%4];n.selectOnRight=cards[(i+1)%4];cards[i].navigation=n;
            }
        }
        static void Focus(Button button){if(EventSystem.current&&button)EventSystem.current.SetSelectedGameObject(button.gameObject);}
        RectTransform Panel(string name,Transform parent,Sprite sprite,Rect area)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;Fit(rect,area);var image=go.GetComponent<UnityEngine.UI.Image>();image.sprite=sprite;
            image.type=UnityEngine.UI.Image.Type.Sliced;image.color=sprite?Color.white:Color.clear;image.raycastTarget=false;return rect;
        }
        TMP_Text Label(string name,Transform parent,string text,Rect area,float size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);Fit((RectTransform)go.transform,area);
            var label=go.GetComponent<TextMeshProUGUI>();label.font=font;label.text=text;label.color=color;label.fontSize=size;label.enableAutoSizing=true;
            label.fontSizeMin=10;label.fontSizeMax=size;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;return label;
        }
        Button Button(string name,Transform parent,string caption,Rect area,UnityEngine.Events.UnityAction click)
        {
            var rect=Panel(name,parent,theme.CreamPanel,area);var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();
            button.image.raycastTarget=true;var colors=button.colors;colors.highlightedColor=new Color(.80f,.94f,1);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(1,.65f,.1f);button.colors=colors;
            button.onClick.AddListener(click);if(!string.IsNullOrEmpty(caption))Label("Label",rect,caption,new Rect(.03f,.04f,.94f,.92f),30,theme.Navy);return button;
        }
        RawImage Raw(string name,Transform parent,Rect area)
        {
            var holder=new GameObject(name+" bounds",typeof(RectTransform));holder.layer=parent.gameObject.layer;holder.transform.SetParent(parent,false);Fit((RectTransform)holder.transform,area);
            var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));go.layer=parent.gameObject.layer;go.transform.SetParent(holder.transform,false);Fit((RectTransform)go.transform,new Rect(0,0,1,1));
            var aspect=go.AddComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=1;
            var image=go.GetComponent<RawImage>();image.raycastTarget=false;return image;
        }
        void SetIcon(RawImage image,string item)
        {
            string type=item=="base-defence"?"defence":item=="zoom-out"?"zoomout":item=="extra-life"?"life":item;
            image.texture=art?art.powerups:null;
            if(image.texture&&BattleHud.TryPowerupUv(type,out var uv)){image.uvRect=uv;image.color=Color.white;}
            else image.color=Color.clear;
        }
        static void Fit(RectTransform rect,Rect a)
        {rect.anchorMin=new Vector2(a.x,1-a.y-a.height);rect.anchorMax=new Vector2(a.x+a.width,1-a.y);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        void OnDestroy(){if(root)Destroy(root.gameObject);if(font)Destroy(font);}
    }
}
