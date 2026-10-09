using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Screen = UnityEngine.Device.Screen;

namespace BattleCities.UI
{
    public sealed class BattleResultsScreen : MonoBehaviour
    {
        sealed class Row
        {
            public RectTransform Root, Portrait, Medal, You;
            public TMP_Text Rank, Name, Kills, Bonus, Points;
            public TMP_Text[] Tiers = new TMP_Text[4];
        }
        readonly ArcadeTextStyles styles = new ArcadeTextStyles();
        BattleResultsArt art;
        BattleResultsData data;
        Canvas canvas; CanvasGroup gate;
        RectTransform root, header, sidebar, paper, summaryHeading, table, tableHeading, columns, viewport, content, footer, mvp;
        TMP_Text title, playerCount, outcome, highScore, enemies, duration, summaryCount, mvpName, teamTotal, status;
        Image outcomeIcon, mvpPortrait;
        Button share, next;
        Row[] rows;
        Psg1UiInput controller;
        Action continueAction, backAction;
        Vector2 lastSize;
        Rect lastSafe;
        float openedAt, messageUntil;
        bool busy, inputReady;
        bool? shareCaptionNavy, nextCaptionNavy;
        static readonly Color Navy = new Color32(6,29,54,255), Line = new Color32(62,108,130,86);
        static readonly float[] Edges = { 0, .09f, .43f, .51f, .59f, .67f, .75f, .85f, 1 };

        public void Show(BattleResultsData report, Action onContinue, Action onBack)
        {
            data = report; continueAction = onContinue; backAction = onBack;
            gameObject.SetActive(true);
            if (!canvas) Build();
            busy = false; inputReady = false; next.interactable = true; openedAt = Time.unscaledTime; gate.interactable = false;
            Render(); Layout();
            if (!Application.isPlaying) { gate.interactable = true; return; }
            if (!EventSystem.current)
            {
                var events = new GameObject("Results EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            if (RuntimePlatformInfo.IsPsg1) controller = new Psg1UiInput();
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(next.gameObject);
        }

        void Build()
        {
            art = Resources.Load<BattleResultsArt>("BattleResultsArt");
            if (!art || !art.theme || !art.panels) throw new InvalidOperationException("BattleResultsArt is not configured.");
            var go = new GameObject("Results Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            go.transform.SetParent(transform, false); canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 600;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 1;
            gate = go.GetComponent<CanvasGroup>();
            var backdrop = Panel("Opaque background", go.transform, null); Stretch(backdrop); backdrop.GetComponent<Image>().color = new Color32(2,24,61,255); backdrop.GetComponent<Image>().raycastTarget = true;
            root = Panel("Full TV frame", go.transform, art.theme.BlueFrame); root.GetComponent<Image>().pixelsPerUnitMultiplier = 3;
            header = Panel("Title", root, art.panels.tankTitlePanel); header.GetComponent<Image>().pixelsPerUnitMultiplier = 4;
            Icon("Medal", header, art.medal);
            title = Label("Title text", header, "", 38, TextAlignmentOptions.MidlineLeft, true);
            share = Control("Share results", header, "SHARE RESULTS", Share);
            Icon("Icon", share.transform, art.share);
            sidebar = Panel("Match report", root, art.theme.BlueFrame); sidebar.GetComponent<Image>().pixelsPerUnitMultiplier = 3;
            paper = Panel("Paper", sidebar, art.theme.CreamPanel); paper.GetComponent<Image>().pixelsPerUnitMultiplier = 2;
            summaryHeading = Panel("Heading", paper, art.panels.statusPanel); summaryHeading.GetComponent<Image>().pixelsPerUnitMultiplier = 3;
            Icon("Clipboard", summaryHeading, art.report); Label("Label", summaryHeading, "MATCH REPORT", 29, TextAlignmentOptions.MidlineLeft);
            var result = Panel("Outcome", paper, null); outcomeIcon = Icon("Icon", result, art.defeat); outcome = Label("Text", result, "", 39, TextAlignmentOptions.MidlineLeft);
            highScore = Metric("Hi score", paper, art.theme.TrophyIcon, "HI-SCORE");
            enemies = Metric("Enemies", paper, art.enemy, "ENEMIES");
            duration = Metric("Battle time", paper, art.theme.TimerIcon, "BATTLE TIME");
            summaryCount = Metric("Players", paper, art.players, "PLAYERS");
            mvp = Panel("MVP", paper, art.panels.tankTitlePanel); mvp.GetComponent<Image>().pixelsPerUnitMultiplier = 4;
            Icon("Medal", mvp, art.medal); mvpPortrait = Icon("Portrait", mvp, art.portraits[0]);
            mvpName = Label("Name", mvp, "", 25, TextAlignmentOptions.MidlineLeft); mvpName.color = Color.white;
            table = Panel("Player results", root, art.theme.CreamPanel); table.GetComponent<Image>().pixelsPerUnitMultiplier = 2;
            tableHeading = Panel("Heading", table, null);
            Label("Title", tableHeading, "PLAYER RESULTS", 35, TextAlignmentOptions.MidlineLeft);
            playerCount = Label("Count", tableHeading, "", 28, TextAlignmentOptions.MidlineRight);
            columns = Panel("Columns", table, null);
            string[] captions = { "RANK", "PLAYER", "I", "II", "III", "IV", "BONUS", "POINTS" };
            for (int i=0;i<8;i++)
            {
                var cell = Panel("Column " + i, columns, null);
                Label("Caption", cell, captions[i], 25);
                if (i>=2 && i<=5) Icon("Tank", cell, art.tanks[i-2]);
            }
            Rule(columns, "Top"); Rule(columns, "Bottom");
            var scroll = Panel("Rows", table, null).gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            viewport = Panel("Viewport", scroll.transform, null); Stretch(viewport); viewport.GetComponent<Image>().color=Color.white; viewport.GetComponent<Image>().raycastTarget=true;
            var mask = viewport.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            content = Panel("Content", viewport, null); content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=content;
            rows = new Row[4];
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i]=new Row(); row.Root=Panel("Player row " + i, content, null); row.Root.gameObject.AddComponent<LayoutElement>();
                row.Rank=Label("Rank",row.Root,"",60);row.Medal=Icon("Rank medal",row.Root,art.medal).rectTransform;
                row.Portrait=Icon("Portrait",row.Root,art.portraits[i]).rectTransform;
                row.Name=Label("Player name",row.Root,"",30,TextAlignmentOptions.MidlineLeft);row.Name.overflowMode=TextOverflowModes.Ellipsis;
                row.Kills=Label("Kills",row.Root,"",22,TextAlignmentOptions.MidlineLeft);
                row.You=Panel("You badge",row.Root,art.theme.Rounded);row.You.GetComponent<Image>().color=new Color32(0,143,231,255);
                Label("Label",row.You,"YOU",18).color=Color.white;
                for(int t=0;t<4;t++)row.Tiers[t]=Label("Tier " + t,row.Root,"",32);
                row.Bonus=Label("Bonus",row.Root,"",30);row.Points=Label("Points",row.Root,"",38);
                Rule(row.Root,"Bottom");
                for(int line=1;line<8;line++)Rule(row.Root,"Column divider " + line);
            }
            footer = Panel("Footer",root,art.panels.statusPanel); TvStatusFooterLayout.ApplySkin(footer.GetComponent<Image>(),art.panels.statusPanel);
            Icon("Shells",footer,art.shells); Label("Team label",footer,"TEAM TOTAL",26,TextAlignmentOptions.MidlineLeft);
            teamTotal=Label("Team total",footer,"",48);status=Label("Status",footer,"MATCH COMPLETE",25);
            Rule(footer,"Divider left");Rule(footer,"Divider right");
            next=Control("Continue",footer,"CONTINUE",()=>{if(!busy)continueAction?.Invoke();});
        }

        void Render()
        {
            title.text="STAGE " + data.Stage + " RESULTS";
            playerCount.text=data.Players.Length + (data.Players.Length==1?" PLAYER":" PLAYERS");
            outcome.text=data.Outcome;outcome.color=data.Won?new Color32(23,115,39,255):new Color32(191,28,25,255);
            outcomeIcon.sprite=data.Won?art.victory:art.defeat;
            highScore.text=data.HighScore.ToString("N0");enemies.text=data.Pvp?data.TeamKills.ToString("N0"):data.Defeated+" / "+data.TotalEnemies;
            enemies.transform.parent.Find("Caption").GetComponent<TMP_Text>().text=data.Pvp?"ELIMINATIONS":"ENEMIES";
            duration.text=data.TimeLabel;summaryCount.text=playerCount.text;
            var best=data.Players.FirstOrDefault();
            mvpName.text="MVP\n"+(best!=null&&(best.Stats.Kills>0||best.Stats.Points>0)?best.Name:"—");
            mvpPortrait.sprite=art.portraits[best?.Slot??0];teamTotal.text=data.TeamKills.ToString("N0");status.text="MATCH COMPLETE";
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];row.Root.gameObject.SetActive(i<data.Players.Length);if(i>=data.Players.Length)continue;
                var player=data.Players[i];
                row.Root.GetComponent<Image>().color=player.Local?new Color32(255,237,158,170):i%2==0?new Color32(244,231,198,110):new Color32(255,252,241,110);
                row.Rank.text=player.Rank.ToString();row.Rank.color=i==0?new Color32(206,147,10,255):i==1?new Color32(88,119,135,255):i==2?new Color32(179,81,34,255):Navy;
                row.Medal.gameObject.SetActive(i==0);row.Portrait.GetComponent<Image>().sprite=art.portraits[player.Slot%4];
                row.Name.text=player.Name;row.Kills.text=player.Stats.Kills+(player.Stats.Kills==1?" KILL":" KILLS");row.You.gameObject.SetActive(player.Local);
                for(int tier=0;tier<4;tier++)row.Tiers[tier].text=player.Stats.ForTier(tier).ToString();
                row.Bonus.text=player.Stats.Bonus==0?"—":player.Stats.Bonus.ToString("N0");row.Points.text=player.Stats.Points.ToString("N0");
            }
        }

        void Layout()
        {
            float sw=Mathf.Max(1,Screen.width),sh=Mathf.Max(1,Screen.height);var safe=Screen.safeArea;
            root.anchorMin=new Vector2(safe.xMin/sw,safe.yMin/sh);root.anchorMax=new Vector2(safe.xMax/sw,safe.yMax/sh);root.offsetMin=root.offsetMax=Vector2.zero;
            Canvas.ForceUpdateCanvases();float w=root.rect.width,h=root.rect.height;
            var top=new TvTitleHeaderLayout(root);top.PlaceTitle(header,(RectTransform)header.Find("Medal"),title.rectTransform,true);
            Place(title.rectTransform,90,4,w*.62f-90,top.Height-8);
            Place((RectTransform)share.transform,header.rect.width*.75f,6,header.rect.width*.24f,top.Height-12);
            FitButtonSkin(share);
            Fit((RectTransform)share.transform.Find("Icon"),.045f,.18f,.15f,.64f);Fit((RectTransform)share.transform.Find("Caption"),.22f,.06f,.73f,.88f);
            var bottom=new TvStatusFooterLayout(root);bottom.Place(footer);TvStatusFooterLayout.PlaceAction((RectTransform)next.transform);
            FitButtonSkin(next);
            float bodyTop=top.Height+14,bodyHeight=h-bodyTop-bottom.Height-14,leftWidth=w*.255f;
            Place(sidebar,10,bodyTop,leftWidth-15,bodyHeight);Place(paper,7,7,sidebar.rect.width-14,bodyHeight-14);
            Place(table,leftWidth+4,bodyTop,w-leftWidth-14,bodyHeight);
            float pw=paper.rect.width,ph=paper.rect.height,headingHeight=54,mvpHeight=Mathf.Clamp(ph*.135f,68,95);
            Place(summaryHeading,0,0,pw,headingHeight);Place((RectTransform)summaryHeading.Find("Clipboard"),12,7,34,40);Place((RectTransform)summaryHeading.Find("Label"),54,4,pw-64,46);
            float area=ph-headingHeight-mvpHeight,unit=area/5.25f;
            var outcomeRoot=(RectTransform)outcome.transform.parent;Place(outcomeRoot,0,headingHeight,pw,unit*1.25f);
            Place(outcomeIcon.rectTransform,12,8,pw*.32f,unit*1.25f-16);Place(outcome.rectTransform,pw*.38f,5,pw*.59f,unit*1.25f-10);
            TMP_Text[] metrics={highScore,enemies,duration,summaryCount};
            for(int i=0;i<metrics.Length;i++)
            {
                var p=(RectTransform)metrics[i].transform.parent;Place(p,0,headingHeight+unit*(1.25f+i),pw,unit);
                Place((RectTransform)p.Find("Icon"),12,8,pw*.27f,unit-16);
                Place((RectTransform)p.Find("Caption"),pw*.37f,4,pw*.60f,unit*.30f);
                Place(metrics[i].rectTransform,pw*.37f,unit*.31f,pw*.60f,unit*.65f);
                Place((RectTransform)p.Find("Rule"),6,0,pw-12,1);
            }
            summaryCount.transform.parent.Find("Caption").gameObject.SetActive(false);
            Place(summaryCount.rectTransform,pw*.37f,8,pw*.60f,unit-16);
            Place(mvp,0,ph-mvpHeight,pw,mvpHeight);Place((RectTransform)mvp.Find("Medal"),10,10,pw*.16f,mvpHeight-20);
            Place(mvpPortrait.rectTransform,pw*.22f,9,mvpHeight-18,mvpHeight-18);Place(mvpName.rectTransform,pw*.22f+mvpHeight-10,8,pw*.78f-mvpHeight,mvpHeight-16);
            float tw=table.rect.width,th=table.rect.height,inner=tw-16;
            Place(tableHeading,8,8,inner,50);Place((RectTransform)tableHeading.Find("Title"),18,0,inner*.66f,50);Place(playerCount.rectTransform,inner*.69f,0,inner*.29f,50);
            Place(columns,8,58,inner,84);Place((RectTransform)columns.Find("Top"),0,0,inner,1);Place((RectTransform)columns.Find("Bottom"),0,83,inner,1.5f);
            for(int i=0;i<8;i++)
            {
                var cell=(RectTransform)columns.Find("Column "+i);Place(cell,inner*Edges[i],0,inner*(Edges[i+1]-Edges[i]),84);
                var caption=(RectTransform)cell.Find("Caption");if(i>=2&&i<=5){Place((RectTransform)cell.Find("Tank"),cell.rect.width*.2f,5,cell.rect.width*.6f,49);Place(caption,0,53,cell.rect.width,28);}else Place(caption,3,5,cell.rect.width-6,74);
            }
            Place((RectTransform)viewport.parent,8,142,inner,th-150);Canvas.ForceUpdateCanvases();
            float rh=Mathf.Max(108,(th-150)/4);
            foreach(var row in rows)
            {
                row.Root.GetComponent<LayoutElement>().preferredHeight=rh;
                float icon=Mathf.Min(rh*.78f,inner*.095f),px=inner*Edges[1]+8,nameX=px+icon+12,nameW=inner*Edges[2]-nameX-8;
                Place(row.Rank.rectTransform,inner*.035f,0,inner*.050f,rh);Place(row.Medal,inner*.006f,rh*.30f,inner*.030f,rh*.40f);
                Place(row.Portrait,px,(rh-icon)*.5f,icon,icon);Place(row.Name.rectTransform,nameX,rh*.14f,nameW,rh*.31f);
                Place(row.You,nameX,rh*.46f,48,23);Stretch((RectTransform)row.You.Find("Label"));
                Place(row.Kills.rectTransform,nameX,row.You.gameObject.activeSelf?rh*.70f:rh*.52f,nameW,rh*.22f);
                for(int t=0;t<4;t++)Place(row.Tiers[t].rectTransform,inner*Edges[t+2],0,inner*(Edges[t+3]-Edges[t+2]),rh);
                Place(row.Bonus.rectTransform,inner*Edges[6],0,inner*(Edges[7]-Edges[6]),rh);Place(row.Points.rectTransform,inner*Edges[7],0,inner*(1-Edges[7]),rh);
                Place((RectTransform)row.Root.Find("Bottom"),0,rh-1,inner,1);
                for(int line=1;line<8;line++)Place((RectTransform)row.Root.Find("Column divider "+line),inner*Edges[line],0,1,rh);
            }
            float fw=footer.rect.width,fh=footer.rect.height;
            Place((RectTransform)footer.Find("Shells"),fw*.035f,5,fh*.9f,fh-10);Place((RectTransform)footer.Find("Team label"),fw*.125f,4,fw*.14f,fh-8);
            Place(teamTotal.rectTransform,fw*.265f,0,fw*.08f,fh);Place(status.rectTransform,fw*.39f,5,fw*.34f,fh-10);
            Place((RectTransform)footer.Find("Divider left"),fw*.365f,fh*.18f,1,fh*.64f);Place((RectTransform)footer.Find("Divider right"),fw*.735f,fh*.18f,1,fh*.64f);
            Canvas.ForceUpdateCanvases();lastSize=new Vector2(Screen.width,Screen.height);lastSafe=safe;
        }

        void Update()
        {
            if(!canvas)return;
            RefreshButtonCaption(share,ref shareCaptionNavy);RefreshButtonCaption(next,ref nextCaptionNavy);
            if(lastSize!=new Vector2(Screen.width,Screen.height)||lastSafe!=Screen.safeArea)Layout();
            gate.interactable=Time.unscaledTime-openedAt>.25f;
            if(messageUntil>0&&Time.unscaledTime>messageUntil){messageUntil=0;if(!busy)status.text="MATCH COMPLETE";}
            if(!gate.interactable)return;
            if(!inputReady){inputReady=true;Psg1UiNavigation.Rows(new Selectable[] { share },new Selectable[] { next });}
            if(RuntimePlatformInfo.IsPsg1){Psg1UiNavigation.KeepFocus(root,next);if(controller!=null&&controller.CancelPressed)backAction?.Invoke();}
            else if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)backAction?.Invoke();
        }
        public void SetStatus(string message,bool blockContinue=false){status.text=message;busy=blockContinue;next.interactable=!busy;messageUntil=0;}
        public void RefreshLayout()=>Layout();
        public void Hide(){controller?.Dispose();controller=null;gameObject.SetActive(false);}
        void Share(){BattleResultsShare.Share(gameObject.name,data.ShareText);}
        public void OnResultsShared(string message){if(!busy){status.text=message;messageUntil=Time.unscaledTime+4;}}
        void OnDisable(){controller?.Dispose();controller=null;}
        void OnDestroy(){controller?.Dispose();styles.Dispose();}

        TMP_Text Metric(string name,Transform parent,Sprite sprite,string caption)
        {
            var row=Panel(name,parent,null);Icon("Icon",row,sprite);Label("Caption",row,caption,23,TextAlignmentOptions.MidlineLeft);Rule(row,"Rule");return Label("Value",row,"",43,TextAlignmentOptions.MidlineLeft);
        }
        RectTransform Panel(string name,Transform parent,Sprite skin)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.sprite=skin;image.type=skin?Image.Type.Sliced:Image.Type.Simple;image.color=skin?Color.white:Color.clear;image.raycastTarget=false;return (RectTransform)go.transform;
        }
        Image Icon(string name,Transform parent,Sprite sprite){var r=Panel(name,parent,null);var i=r.GetComponent<Image>();i.sprite=sprite;i.type=Image.Type.Simple;i.preserveAspect=true;i.color=Color.white;return i;}
        void Rule(Transform parent,string name){Panel(name,parent,null).GetComponent<Image>().color=Line;}
        TMP_Text Label(string name,Transform parent,string text,float size,TextAlignmentOptions align=TextAlignmentOptions.Center,bool gold=false)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var label=go.GetComponent<TMP_Text>();
            label.text=text;label.richText=false;label.fontSize=label.fontSizeMax=size;label.fontSizeMin=size*.66f;label.enableAutoSizing=true;label.alignment=align;label.textWrappingMode=TextWrappingModes.NoWrap;label.raycastTarget=false;
            styles.Apply(label,gold?ArcadeTextTreatment.Gold:ArcadeTextTreatment.PrizeAmount);return label;
        }
        Button Control(string name,Transform parent,string text,Action action)
        {
            var rect=Panel(name,parent,art.panels.tankCostButton);var image=rect.GetComponent<Image>();image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.SpriteSwap;
            button.spriteState=new SpriteState{highlightedSprite=art.panels.tankCostButtonSelected,pressedSprite=art.panels.tankCostButtonSelected,selectedSprite=art.panels.tankCostButtonSelected,disabledSprite=art.panels.tankCostButtonLocked};
            button.onClick.AddListener(()=>{if(gate.interactable)action();});
            var label=Label("Caption",rect,text,30);Fit(label.rectTransform,.05f,.05f,.90f,.90f);styles.ApplyCleanButton(label,false);
            return button;
        }
        static void FitButtonSkin(Button button)
        {
            var image=button.image;image.type=Image.Type.Sliced;image.preserveAspect=false;
            image.pixelsPerUnitMultiplier=image.sprite.rect.width/(Mathf.Max(1,((RectTransform)button.transform).rect.width)*Mathf.Max(.01f,image.pixelsPerUnit));
        }
        void RefreshButtonCaption(Button button,ref bool? previous)
        {
            var skin=button.image.overrideSprite?button.image.overrideSprite:button.image.sprite;
            bool navy=skin==art.panels.tankCostButtonSelected;if(previous==navy)return;previous=navy;
            styles.ApplyCleanButton(button.GetComponentInChildren<TMP_Text>(),navy);
        }
        static void Place(RectTransform r,float x,float y,float w,float h)=>MainMenuScene.Place(r,x,y,w,h);
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        static void Fit(RectTransform r,float x,float y,float w,float h){r.anchorMin=new Vector2(x,1-y-h);r.anchorMax=new Vector2(x+w,1-y);r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
