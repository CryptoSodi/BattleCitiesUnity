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
        TMP_Text title,status,fuel,fuelAmount,deploymentHeading,deploymentCost,continueLabel,wallet;
        readonly List<Button> selectable=new List<Button>();
        readonly Button[] tankButtons=new Button[4],slotButtons=new Button[4];
        readonly TMP_Text[] slotNames=new TMP_Text[4],slotEmpty=new TMP_Text[4],inventoryLabels=new TMP_Text[8];
        readonly RawImage[] slotIcons=new RawImage[4];
        Button back,proceed;
        PreBattleArt art;
        JObject account,pendingFuel,loadout=new JObject();
        int selected,paidTier=-1;
        bool busy,inLoadout,ownsFont;
        public bool IsOpen=>root&&root.gameObject.activeSelf;
        public bool IsConfigured=>root&&tankButtons[0]&&launch!=null;
        public void Configure(RectTransform frame,MenuTheme skin,MainMenuApiClient client,Action onLaunch,Button returnTo)
        {
            theme=skin;api=client;launch=onLaunch;home=returnTo;
            art=Resources.Load<PreBattleArt>("PreBattleArt");
            root=frame.Find("Pre-battle screens") as RectTransform;
            if(root)
            {
                BindExistingView();
                ApplyScreenSurface();
                ShowPage(false);
                return;
            }
            font=TMP_FontAsset.CreateFontAsset(theme.HeadingFont);
            ownsFont=true;
            // Keep the original cream panel's sliced silhouette as the rounded screen mask.
            root=Panel("Pre-battle screens",frame,theme.CreamPanel,new Rect(.025f,.035f,.95f,.93f));
            root.GetComponent<Image>().raycastTarget=true;
            root.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
            var titlePanel=Panel("Title plate",root,art&&art.tankTitlePanel?art.tankTitlePanel:theme.BlueFrame,new Rect(.02176f,.0176f,.7203f,.0798f));
            titlePanel.GetComponent<Image>().pixelsPerUnitMultiplier=4;
            var mascotSprite=theme.NavigationIcons!=null&&theme.NavigationIcons.Length>0?theme.NavigationIcons[0]:null;
            var mascot=Panel("Mascot",titlePanel,mascotSprite,new Rect(.30f,.08f,.10f,.84f));
            var mascotImage=mascot.GetComponent<Image>();mascotImage.type=Image.Type.Simple;mascotImage.preserveAspect=true;
            title=Label("Title",titlePanel,"SELECT TANK",new Rect(.42f,0,.52f,1),34,theme.Gold);
            title.alignment=TextAlignmentOptions.MidlineLeft;
            back=Button("Back",root,"< BACK",new Rect(.7548f,.0176f,.2254f,.0798f),Back);
            var backLabel=back.GetComponentInChildren<TMP_Text>();
            backLabel.color=Color.white;
            if(art&&art.backButton)
            {
                back.image.sprite=art.backButton;
                back.image.type=Image.Type.Simple;
                back.image.preserveAspect=true;
                backLabel.gameObject.SetActive(false);
                var backColors=back.colors;
                backColors.normalColor=Color.white;
                backColors.highlightedColor=new Color(.90f,.97f,1f);
                backColors.selectedColor=backColors.highlightedColor;
                backColors.pressedColor=new Color(.75f,.87f,.98f);
                back.colors=backColors;
            }
            else back.image.sprite=theme.BlueFrame;
            var balance=Panel("Fuel summary",root,art&&art.statusPanel?art.statusPanel:theme.DarkPanel,new Rect(.015f,.103f,.97f,.089f));
            fuel=Label("Fuel",balance,"FUEL AVAILABLE",new Rect(.105f,.10f,.27f,.80f),25,theme.Navy);
            ApplyStatusPanel();
            tankPage=Panel("Tank roster",root,null,new Rect(.015f,.207f,.97f,.633f));
            for(int i=0;i<4;i++)
            {
                int index=i;
                var b=Button(Names[i],tankPage,"",new Rect(i*.25f+.006f,0,.238f,1),()=>SelectTank(index));
                tankButtons[i]=b;
                Label("Name",b.transform,Names[i],new Rect(.025f,.04f,.95f,.10f),26,theme.Navy);
                var picture=Raw("Tank model",b.transform,new Rect(.035f,.11f,.93f,.40f));
                picture.texture=art&&art.tanks!=null&&i<art.tanks.Length?art.tanks[i]:null;
                picture.color=picture.texture?Color.white:Color.clear;
                Label("Role",b.transform,Roles[i],new Rect(.04f,.485f,.92f,.085f),19,theme.Navy);
                Label("Specifications",b.transform,"",new Rect(.06f,.58f,.88f,.19f),18,theme.Navy);
            }
            ApplyRosterPanel();
            loadoutPage=Panel("Loadout",root,null,new Rect(.015f,.207f,.97f,.633f));
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
            status=Label("Status",root,"CHOOSE YOUR CHASSIS",new Rect(.035f,.88f,.58f,.105f),19,new Color32(245,235,207,255));
            status.alignment=TextAlignmentOptions.MidlineLeft;
            proceed=Button("Continue",root,"CONTINUE",new Rect(.68f,.88f,.29f,.105f),Continue);
            continueLabel=proceed.GetComponentInChildren<TMP_Text>();
            ApplyContinueArt();
            root.gameObject.SetActive(false);
        }
        // Scene previews and editor reloads retain GameObjects, but not runtime UnityEvent listeners.
        void BindExistingView()
        {
            var heading=root.Find("Heading");
            if(heading)
            {
                heading.Find("Title plate").SetParent(root,false);
                heading.Find("Back").SetParent(root,false);
                if(Application.isPlaying)Destroy(heading.gameObject);else DestroyImmediate(heading.gameObject);
            }
            title=root.Find("Title plate/Title").GetComponent<TMP_Text>();font=title.font;
            status=root.Find("Status").GetComponent<TMP_Text>();
            fuel=root.Find("Fuel summary/Fuel").GetComponent<TMP_Text>();
            back=root.Find("Back").GetComponent<Button>();
            proceed=root.Find("Continue").GetComponent<Button>();
            continueLabel=proceed.GetComponentInChildren<TMP_Text>(true);
            tankPage=(RectTransform)root.Find("Tank roster");
            loadoutPage=(RectTransform)root.Find("Loadout");
            var inventory=loadoutPage.Find("Wallet and inventory");
            wallet=inventory.Find("Wallet").GetComponent<TMP_Text>();
            int count=0;
            foreach(var label in inventory.GetComponentsInChildren<TMP_Text>(true))
                if(label.name=="Count")inventoryLabels[count++]=label;
            BindClick(back,Back);BindClick(proceed,Continue);
            for(int i=0;i<4;i++)
            {
                int index=i;
                tankButtons[i]=tankPage.Find(Names[i]).GetComponent<Button>();
                slotButtons[i]=loadoutPage.Find("Slot "+(i+1)).GetComponent<Button>();
                var slot=slotButtons[i].transform;
                slotNames[i]=slot.Find("Item name").GetComponent<TMP_Text>();
                slotEmpty[i]=slot.Find("Empty slot").GetComponent<TMP_Text>();
                slotIcons[i]=slot.Find("Equipped item bounds/Equipped item").GetComponent<RawImage>();
                BindClick(tankButtons[i],()=>SelectTank(index));
                BindClick(slotButtons[i],()=>CycleSlot(index));
            }
        }
        static void BindClick(Button button,UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
        void ApplyScreenSurface()
        {
            Fit(root,new Rect(.025f,.035f,.95f,.93f));
            var surface=root.GetComponent<Image>();
            surface.sprite=theme.CreamPanel;surface.type=Image.Type.Sliced;surface.color=Color.white;
            surface.raycastTarget=true;
            var rectangularMask=root.GetComponent<RectMask2D>();
            if(rectangularMask)
            {
                rectangularMask.enabled=false;
                if(Application.isPlaying)Destroy(rectangularMask);else DestroyImmediate(rectangularMask);
            }
            var mask=root.GetComponent<UnityEngine.UI.Mask>();
            if(!mask)mask=root.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic=false;
            var backdrop=root.Find("Blue backdrop") as RectTransform;
            if(backdrop)
            {
                if(Application.isPlaying)Destroy(backdrop.gameObject);
                else DestroyImmediate(backdrop.gameObject);
            }
            Fit((RectTransform)title.transform.parent,new Rect(.02176f,.0176f,.7203f,.0798f));
            Fit((RectTransform)back.transform,new Rect(.7548f,.0176f,.2254f,.0798f));
            Fit(loadoutPage,new Rect(.015f,.207f,.97f,.633f));
            status.color=new Color32(245,235,207,255);
            ApplyRosterPanel();
            ApplyStatusPanel();
        }
        void ApplyStatusPanel()
        {
            var balance=(RectTransform)fuel.transform.parent;
            Fit(balance,new Rect(.015f,.103f,.97f,.089f));
            var image=balance.GetComponent<Image>();
            image.sprite=art&&art.statusPanel?art.statusPanel:theme.DarkPanel;
            image.type=Image.Type.Sliced;
            image.pixelsPerUnitMultiplier=art&&art.statusPanel?2f:1f;
            image.color=Color.white;
            image.raycastTarget=false;
            var oldRefresh=balance.Find("Refresh");
            if(oldRefresh)
            {
                if(Application.isPlaying)Destroy(oldRefresh.gameObject);
                else DestroyImmediate(oldRefresh.gameObject);
            }
            var icon=balance.Find("Fuel can") as RectTransform;
            if(!icon)icon=Panel("Fuel can",balance,null,new Rect(.018f,0,.09f,1));
            Fit(icon,new Rect(.018f,0,.09f,1));
            var iconImage=icon.GetComponent<Image>();
            iconImage.sprite=art?art.fuelCan:null;
            iconImage.type=Image.Type.Simple;
            iconImage.preserveAspect=true;
            iconImage.color=iconImage.sprite?Color.white:Color.clear;
            iconImage.raycastTarget=false;
            Fit((RectTransform)fuel.transform,new Rect(.105f,.10f,.27f,.80f));
            fuel.text="FUEL AVAILABLE";
            fuel.color=theme.Navy;
            fuel.alignment=TextAlignmentOptions.MidlineLeft;
            fuel.fontSize=25;
            fuel.fontSizeMax=25;
            fuelAmount=SummaryLabel(balance,"Fuel amount",new Rect(.265f,.08f,.06f,.84f),"0",38,theme.Gold,TextAlignmentOptions.Center);
            deploymentHeading=SummaryLabel(balance,"Deployment heading",new Rect(.64f,.08f,.34f,.39f),"DEPLOYMENT COST",21,theme.Navy,TextAlignmentOptions.MidlineRight);
            deploymentCost=SummaryLabel(balance,"Deployment cost",new Rect(.56f,.45f,.42f,.48f),"VANGUARD / 1 FUEL",28,new Color32(192,108,4,255),TextAlignmentOptions.MidlineRight);
            fuelAmount.outlineColor=new Color32(75,45,7,255);
            fuelAmount.outlineWidth=.12f;
            deploymentCost.outlineColor=new Color32(76,46,8,255);
            deploymentCost.outlineWidth=.08f;
        }
        TMP_Text SummaryLabel(Transform parent,string name,Rect area,string value,float size,Color color,TextAlignmentOptions alignment)
        {
            var child=parent.Find(name);
            var label=child?child.GetComponent<TMP_Text>():Label(name,parent,value,area,size,color);
            Fit((RectTransform)label.transform,area);
            label.text=value;
            label.color=color;
            label.alignment=alignment;
            label.fontSize=size;
            label.fontSizeMax=size;
            return label;
        }
        void ApplyRosterPanel()
        {
            // Fill the space between the fuel bar and the Continue footer.
            var rosterAspect=tankPage.GetComponent<AspectRatioFitter>();
            if(rosterAspect)
            {
                rosterAspect.aspectMode=AspectRatioFitter.AspectMode.None;
                rosterAspect.enabled=false;
            }
            Fit(tankPage,new Rect(.015f,.196f,.97f,.674f));
            var panel=tankPage.GetComponent<Image>();
            panel.sprite=theme.CreamPanel;
            panel.type=Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier=2f;
            panel.color=Color.white;
            panel.raycastTarget=false;
            // Keep the cards compact at the top of the expanded white panel.
            for(int i=0;i<tankButtons.Length;i++)
            {
                var card=(RectTransform)tankButtons[i].transform;
                card.anchorMin=new Vector2(.009f+i*.24625f,1f);
                card.anchorMax=new Vector2(.25225f+i*.24625f,1f);
                card.pivot=new Vector2(.5f,1f);
                card.anchoredPosition=new Vector2(0,-8f);
                card.sizeDelta=new Vector2(0,1);
                var cardAspect=card.GetComponent<AspectRatioFitter>();
                if(!cardAspect)cardAspect=card.gameObject.AddComponent<AspectRatioFitter>();
                cardAspect.aspectMode=AspectRatioFitter.AspectMode.WidthControlsHeight;
                cardAspect.aspectRatio=.24325f*2.4f/.94f;
                var name=tankButtons[i].transform.Find("Name") as RectTransform;
                if(name)Fit(name,new Rect(.025f,.04f,.95f,.10f));
                ApplyTankPicture(tankButtons[i],i);
                ApplyCostBadge(tankButtons[i],i);
            }
        }
        void ApplyTankPicture(Button card,int index)
        {
            var bounds=card.transform.Find("Tank model bounds");
            var picture=(bounds?bounds.Find("Tank model"):card.transform.Find("Tank model")) as RectTransform;
            if(!picture)return;
            var image=picture.GetComponent<RawImage>();
            var texture=art&&art.tanks!=null&&index<art.tanks.Length?art.tanks[index]:null;
            image.texture=texture;
            image.color=texture?Color.white:Color.clear;
            image.raycastTarget=false;
            if(!texture)return;
            var aspect=picture.GetComponent<AspectRatioFitter>();
            if(!aspect)aspect=picture.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectRatio=(float)texture.width/texture.height;
            if(bounds)
            {
                Fit((RectTransform)bounds,new Rect(.035f,.11f,.93f,.40f));
                // Let the illustration tuck behind the title without covering its lettering.
                bounds.SetAsFirstSibling();
                aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
                return;
            }
            // Keep the supplied transparent illustrations at their original proportions.
            picture.SetAsFirstSibling();
            picture.anchorMin=new Vector2(.035f,.69f);
            picture.anchorMax=new Vector2(.965f,.69f);
            picture.pivot=new Vector2(.5f,.5f);
            picture.anchoredPosition=Vector2.zero;
            picture.sizeDelta=new Vector2(0,1);
            aspect.aspectMode=AspectRatioFitter.AspectMode.WidthControlsHeight;
        }
        void ApplyCostBadge(Button card,int index)
        {
            var role=card.transform.Find("Role") as RectTransform;
            if(role)Fit(role,new Rect(.04f,.485f,.92f,.085f));
            ApplySpecifications(card,index);
            var oldTier=card.transform.Find("Tank tier");
            if(oldTier)
            {
                if(Application.isPlaying)Destroy(oldTier.gameObject);
                else DestroyImmediate(oldTier.gameObject);
            }
            var badge=card.transform.Find("Cost badge") as RectTransform;
            if(!badge)badge=Panel("Cost badge",card.transform,null,new Rect(.025f,.80f,.95f,.17f));
            Fit(badge,new Rect(.025f,.80f,.95f,.17f));
            var badgeImage=badge.GetComponent<Image>();
            badgeImage.sprite=art&&art.tankCostButton?art.tankCostButton:theme.BlueFrame;
            badgeImage.type=Image.Type.Sliced;
            badgeImage.pixelsPerUnitMultiplier=2f;
            badgeImage.color=Color.white;
            badgeImage.raycastTarget=false;
            var can=badge.Find("Fuel can") as RectTransform;
            if(!can)can=Panel("Fuel can",badge,null,new Rect(.13f,.14f,.28f,.72f));
            Fit(can,new Rect(.13f,.14f,.28f,.72f));
            var canImage=can.GetComponent<Image>();
            canImage.sprite=art?(art.buttonFuelCan?art.buttonFuelCan:art.fuelCan):null;
            canImage.type=Image.Type.Simple;
            canImage.preserveAspect=true;
            canImage.color=canImage.sprite?Color.white:Color.clear;
            canImage.raycastTarget=false;
            var child=badge.Find("Cost text");
            var caption=child?child.GetComponent<TMP_Text>():Label("Cost text",badge,"",new Rect(.44f,.045f,.52f,.91f),27,Color.white);
            Fit((RectTransform)caption.transform,new Rect(.44f,.045f,.52f,.91f));
            caption.text=(index+1)+" FUEL";
            caption.color=Color.white;
            caption.fontStyle=FontStyles.Bold;
            caption.alignment=TextAlignmentOptions.MidlineLeft;
            caption.fontSize=27;
            caption.fontSizeMax=27;
            caption.raycastTarget=false;
        }
        void ApplySpecifications(Button card,int index)
        {
            var specifications=card.transform.Find("Specifications") as RectTransform;
            if(!specifications)return;
            Fit(specifications,new Rect(.06f,.58f,.88f,.19f));
            var legacy=specifications.GetComponent<TMP_Text>();
            if(legacy)legacy.text="";
            string[] headings={"ROUNDS","VELOCITY","WALL DAMAGE"};
            string[] values={index<2?"SINGLE":"TWIN",index==0?"STANDARD":"HIGH",index==3?"HEAVY":"STANDARD"};
            for(int row=0;row<3;row++)
            {
                var stripe=specifications.Find("Specification row "+row) as RectTransform;
                var area=new Rect(0,row/3f,1,1f/3f);
                if(!stripe)stripe=Panel("Specification row "+row,specifications,null,area);
                Fit(stripe,area);
                var background=stripe.GetComponent<Image>();
                background.type=Image.Type.Simple;
                background.color=row==1?new Color32(255,250,238,70):new Color32(238,220,184,130);
                background.raycastTarget=false;
                var heading=stripe.Find("Heading");
                var left=heading?heading.GetComponent<TMP_Text>():Label("Heading",stripe,"",new Rect(.04f,0,.54f,1),15,theme.Navy);
                Fit((RectTransform)left.transform,new Rect(.04f,0,.54f,1));
                left.text=headings[row];
                left.color=theme.Navy;
                left.fontStyle=FontStyles.Bold;
                left.alignment=TextAlignmentOptions.MidlineLeft;
                left.fontSize=15;
                left.fontSizeMax=15;
                var value=stripe.Find("Value");
                var right=value?value.GetComponent<TMP_Text>():Label("Value",stripe,"",new Rect(.62f,0,.34f,1),15,new Color32(154,87,15,255));
                Fit((RectTransform)right.transform,new Rect(.62f,0,.34f,1));
                right.text=values[row];
                right.color=new Color32(154,87,15,255);
                right.fontStyle=FontStyles.Bold;
                right.alignment=TextAlignmentOptions.MidlineRight;
                right.fontSize=15;
                right.fontSizeMax=15;
            }
        }
        public void Open()
        {
            ApplyRosterPanel();
            GetComponent<MainMenuScene>()?.SetHeroVisible(false);
            root.gameObject.SetActive(true);root.SetAsLastSibling();
            GetComponent<MainMenuScene>()?.SetTankSelectorBackdrop(true);
            ShowPage(false);SelectTank(selected);
            status.text="SELECT A TANK, THEN CONTINUE TO BATTLE";
            Focus(tankButtons[selected]);
            if(Application.isPlaying&&api)StartCoroutine(LoadAccount());
        }
        public void Back()
        {
            if(busy)return;
            if(inLoadout){ShowPage(false);status.text="FUEL PAID — CONTINUE RETURNS TO YOUR LOADOUT";Focus(proceed);return;}
            root.gameObject.SetActive(false);
            GetComponent<MainMenuScene>()?.SetHeroVisible(true);
            GetComponent<MainMenuScene>()?.SetTankSelectorBackdrop(false);
            Focus(home);
        }
        void ShowPage(bool loadoutView)
        {
            inLoadout=loadoutView;tankPage.gameObject.SetActive(!inLoadout);loadoutPage.gameObject.SetActive(inLoadout);
            title.text=inLoadout?"BATTLE LOADOUT":"SELECT TANK";
            ApplyContinueArt();
            UpdateView();Navigation();
        }
        void ApplyContinueArt()
        {
            bool useSuppliedSprites=!inLoadout&&art&&art.continueActive&&art.continueInactive;
            continueLabel.text=inLoadout?"START BATTLE":"CONTINUE";
            continueLabel.gameObject.SetActive(!useSuppliedSprites);
            proceed.image.overrideSprite=null;
            proceed.image.color=Color.white;
            if(useSuppliedSprites)
            {
                proceed.image.sprite=art.continueInactive;
                proceed.image.type=Image.Type.Simple;
                proceed.image.preserveAspect=true;
                proceed.spriteState=new SpriteState
                {
                    highlightedSprite=art.continueActive,
                    pressedSprite=art.continueActive,
                    selectedSprite=art.continueActive,
                    disabledSprite=art.continueInactive
                };
                proceed.transition=Selectable.Transition.SpriteSwap;
            }
            else
            {
                proceed.transition=Selectable.Transition.ColorTint;
                proceed.image.sprite=theme.GoldPanel;
                proceed.image.type=Image.Type.Sliced;
                proceed.image.preserveAspect=false;
            }
        }
        void SelectTank(int index)
        {
            if(busy||pendingFuel!=null||paidTier>=0)return;
            selected=index;UpdateView();Focus(tankButtons[index]);
        }
        void UpdateView()
        {
            fuelAmount.text=Number(account?["fuelBalance"]);
            deploymentCost.text=Names[selected]+" / "+(selected+1)+" FUEL";
            for(int i=0;i<4;i++)
            {
                bool available=!busy&&paidTier<0&&pendingFuel==null;
                var card=tankButtons[i];
                card.interactable=available;
                bool supplied=art&&art.tankCardSelected&&art.tankCardAvailable&&art.tankCardUnavailable;
                card.image.sprite=supplied?(available?(i==selected?art.tankCardSelected:art.tankCardAvailable):art.tankCardUnavailable):
                    (i==selected?theme.GoldPanel:theme.CreamPanel);
                card.image.type=Image.Type.Sliced;
                card.image.pixelsPerUnitMultiplier=supplied?4f:1f;
                card.image.color=Color.white;
                if(supplied)
                {
                    var colors=card.colors;
                    colors.normalColor=Color.white;
                    colors.highlightedColor=new Color(1f,.98f,.86f);
                    colors.selectedColor=colors.highlightedColor;
                    colors.pressedColor=new Color(.92f,.92f,.92f);
                    colors.disabledColor=Color.white;
                    card.colors=colors;
                }
                string item=(string)loadout[Slots[i]];int at=Array.IndexOf(Items,item);
                slotNames[i].text=at>=0?ItemNames[at]:"EMPTY";SetIcon(slotIcons[i],item);
                slotEmpty[i].gameObject.SetActive(at<0);
                slotButtons[i].interactable=!busy;
            }
            wallet.text="WALLET\n"+(account==null?"NOT CONNECTED":"CONNECTED")+"\nBATC  "+Number(account?["tokenBalance"])+"\nSOL  "+Number(account?["solBalance"]);
            for(int i=0;i<8;i++)inventoryLabels[i].text=Number(account?["inventory"]?[Items[i]]);
            back.interactable=!busy;
            proceed.interactable=true;
            Navigation();
        }
        static string Number(JToken value)=>value==null?"0":value.ToString();
        IEnumerator LoadAccount()
        {
            yield return api.Request("GET","/api/economy/account",null,(code,body,error)=>
            {
                if(code>=200&&code<300&&(bool?)body?["authenticated"]==true&&body["account"] is JObject data)
                {account=data;loadout=(JObject)(data["loadout"]?.DeepClone()??new JObject());}
                else account=null;
                UpdateView();
            });
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
                {busy=false;status.text=account==null?"SHOP UNAVAILABLE — TRY AGAIN LATER":"NEED "+(selected+1)+" FUEL — VISIT THE SHOP";UpdateView();yield break;}
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
            selectable.Clear();selectable.Add(back);
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
        void OnDestroy(){if(root)Destroy(root.gameObject);if(ownsFont&&font)Destroy(font);}
    }
}
