using System;
using System.IO;
using System.Linq;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class MainMenuBuilder
    {
        public const string Root="Assets/BattleCities/UI/";
        public const string ScenePath="Assets/BattleCities/Scenes/MainMenu.unity";
        private static MenuTheme theme;
        private static readonly Color Navy=new Color32(6,29,54,255), Gold=new Color32(255,224,62,255);

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void ImportRewardHeaderIconsAfterReload()
        {
            EditorApplication.delayCall-=EnsureRewardHeaderIcons;
            EditorApplication.delayCall+=EnsureRewardHeaderIcons;
        }

        private static void EnsureRewardHeaderIcons()
        {
            EditorApplication.delayCall-=EnsureRewardHeaderIcons;
            const string timerPath=Root+"Art/shared/icons/timer.png";
            var importer=AssetImporter.GetAtPath(timerPath) as TextureImporter;
            if(importer==null)return;
            bool needsImport=importer.textureType!=TextureImporterType.Sprite
                || importer.spriteImportMode!=SpriteImportMode.Single
                || importer.mipmapEnabled
                || !importer.alphaIsTransparency;
            if(needsImport)
            {
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=100;
                importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false;
                importer.isReadable=false;
                importer.filterMode=FilterMode.Bilinear;
                importer.wrapMode=TextureWrapMode.Clamp;
                importer.maxTextureSize=2048;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                var settings=new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                var android=importer.GetPlatformTextureSettings("Android");
                android.overridden=true;
                android.maxTextureSize=2048;
                android.format=TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
            }

            var timer=AssetDatabase.LoadAssetAtPath<Sprite>(timerPath);
            var menuTheme=AssetDatabase.LoadAssetAtPath<MenuTheme>(Root+"Settings/ArcadeMenuTheme.asset");
            if(!timer||!menuTheme)return;
            if(menuTheme.TimerIcon!=timer)
            {
                menuTheme.TimerIcon=timer;
                EditorUtility.SetDirty(menuTheme);
                AssetDatabase.SaveAssets();
            }
            foreach(var view in UnityEngine.Resources.FindObjectsOfTypeAll<MainMenuScene>())
                if(view&&view.gameObject.scene.IsValid())view.RefreshLayout();
        }

        [MenuItem("Battle Cities/UI/Create Main Menu Scene")]
        public static void Create()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before creating the main menu.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save your open scene edits before creating MainMenu.");
            if(File.Exists(ScenePath))throw new InvalidOperationException("MainMenu already exists. Open it with Battle Cities/UI/Open Main Menu.");
            Directory.CreateDirectory(Root+"Prefabs/Shared");Directory.CreateDirectory(Root+"Prefabs/MainMenu");Directory.CreateDirectory(Root+"Settings");
            ImportArt();
            theme=CreateTheme();
            var actions=CreateInput();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cameraObject=new GameObject("Menu Camera",typeof(Camera),typeof(AudioListener));
            var camera=cameraObject.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.17f,.25f);camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.tag="MainCamera";
            var canvasObject=new GameObject("Main Menu",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(MainMenuScene));
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            var view=canvasObject.GetComponent<MainMenuScene>();
            var root=(RectTransform)canvasObject.transform;
            var background=Img("World backdrop",root,theme.Battlefield);Stretch(background.rectTransform);
            var safe=Rect("Safe Area",root);Stretch(safe);
            var content=Rect("Content",safe);
            var header=Panel("Status Bar",content,null);
            Text player=null,score=null,hi=null;Button settings=null;
            for(int i=0;i<3;i++)
            {
                var tile=Panel(i==0?"Player":i==1?"Score":"High Score",header,theme.BlueFrame);Box(tile,i/3f+.005f,0,1/3f-.01f,1);
                var badge=Panel("Icon tile",tile,theme.CreamPanel);Box(badge,.035f,.10f,.24f,.78f);
                var icon=Img("Icon",badge,i==0?theme.SettingsIcon:i==1?theme.ScoreIcon:theme.HighScoreIcon);Box(icon.rectTransform,.05f,.04f,.90f,.90f);icon.preserveAspect=true;
                var inset=Panel("Readout",tile,theme.DarkPanel);Box(inset,.295f,.13f,.65f,.74f);
                var title=Label("Label",inset,i==0?"COMMANDER":i==1?"SCORE":"HI-SCORE",i==0?29:25,Color.white);Box(title.rectTransform,.03f,.04f,.94f,.44f);
                var value=Label("Value",inset,i==0?"LVL 1":"000000",i==0?22:38,Gold);Box(value.rectTransform,.03f,.45f,.94f,.5f);
                if(i==0){player=title;settings=MakeButton(tile);AddFocus(tile,settings);}
                else if(i==1)score=value;else hi=value;
            }
            var frame=Panel("Main Display",content,null,new Color(1,1,1,0));
            var hero=Panel("Battlefield Hero",frame,null,new Color(1,1,1,0));


            var logo=Img("Battle Cities logo",hero,theme.Logo);logo.preserveAspect=true;
            var start=Img("Start Battle",hero,theme.PlayButton);start.preserveAspect=true;
            var startButton=MakeButton(start.rectTransform);AddFocus(start.rectTransform,startButton);
            var startLabel=Label("Editable label",start.rectTransform,"START",74,new Color32(48,28,3,255),false);Box(startLabel.rectTransform,.34f,.10f,.46f,.72f);
            var nav=Panel("Navigation",content,Art("reference-style-v2/shared/panels/button-leaderboard-container"));
            var tabs=new Button[5];string[] names={"PLAY","SHOP","RANKING","QUARTERS","SOCIALS"};
            int[] iconIndices={0,2,3,1,4};
            for(int i=0;i<5;i++)
            {
                var tile=Panel(names[i],nav,i==0?theme.SelectedPanel:theme.CreamPanel);
                var icon=Img("Icon",tile,theme.NavigationIcons[iconIndices[i]]);icon.preserveAspect=true;Box(icon.rectTransform,.12f,.04f,.76f,.68f);
                var label=Label("Label",tile,names[i],29,Navy,false);Box(label.rectTransform,.03f,.72f,.94f,.22f);
                tabs[i]=MakeButton(tile);AddFocus(tile,tabs[i]);
            }
            var board=BuildLeaderboard(content,out var boardMessage,out var boardDetail);
            var how=BuildHowItWorks(content);
            var controls=Panel("PSG1 Controls",content,theme.DarkPanel);
            var controlText=Label("Hints",controls,"D-PAD  NAVIGATE          A  SELECT          B  BACK",26,Color.white);Stretch(controlText.rectTransform);
            var modal=Panel("Modal",content,null,new Color(0,.035f,.07f,.78f));Stretch(modal);
            modal.GetComponent<Image>().raycastTarget=true;
            var dialog=Panel("Dialog",modal,theme.BlueFrame);
            var dialogTitle=Label("Title",dialog,"CONTROLS",38,Gold);Box(dialogTitle.rectTransform,.05f,.06f,.90f,.12f);
            var bodyPanel=Panel("Body panel",dialog,theme.CreamPanel);Box(bodyPanel,.04f,.22f,.92f,.56f);
            var body=Label("Body",bodyPanel,"",26,Navy,false);Box(body.rectTransform,.04f,.03f,.92f,.94f);body.font=theme.BodyFont;
            var closeRect=Panel("Back",dialog,theme.GoldPanel);Box(closeRect,.30f,.82f,.4f,.13f);
            var closeText=Label("Label",closeRect,"BACK",29,Navy,false);Stretch(closeText.rectTransform);
            var close=MakeButton(closeRect);AddFocus(closeRect,close);
            modal.gameObject.SetActive(false);
            var eventObject=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var module=eventObject.GetComponent<InputSystemUIInputModule>();
            var eventSystem=eventObject.GetComponent<EventSystem>();eventSystem.firstSelectedGameObject=start.gameObject;
            view.Configure(theme,safe,content,header,frame,hero,nav,board,how,controls,logo.rectTransform,start.rectTransform,modal,
                startButton,settings,close,tabs,player,score,hi,dialogTitle,body,boardMessage,boardDetail,actions,module);
            UnityEventTools.AddPersistentListener(startButton.onClick,view.StartBattle);
            UnityEventTools.AddPersistentListener(settings.onClick,view.OpenSettings);
            UnityEventTools.AddPersistentListener(close.onClick,view.Back);
            UnityEventTools.AddPersistentListener(tabs[0].onClick,view.PlayTab);
            UnityEventTools.AddPersistentListener(tabs[1].onClick,view.OpenShop);
            UnityEventTools.AddPersistentListener(tabs[2].onClick,view.OpenRanking);
            UnityEventTools.AddPersistentListener(tabs[3].onClick,view.OpenQuarters);
            UnityEventTools.AddPersistentListener(tabs[4].onClick,view.OpenSocials);
            // Reusable component prefabs are clean templates; scene-specific events stay in the scene.
            ReferenceHeaderArt.Import();
            ReferenceNavigationArt.ApplyTo(nav);
            ReferenceInstructionArt.ApplyTo(controls,how);
            ReferenceStartArt.ApplyTo(start.rectTransform,theme);
            ReferenceHeaderArt.ApplyHeader(header,theme);
            ExportTemplate(tabs[1].gameObject,"Shared/NavigationTile");
            ExportTemplate(start.gameObject,"Shared/PlayButton");
            ExportTemplate(header.GetChild(1).gameObject,"Shared/StatusCard");
            ExportTemplate(how.gameObject,"MainMenu/HowItWorks");
            ExportTemplate(board.gameObject,"MainMenu/LeaderboardPanel");
            ExportTemplate(frame.gameObject,"MainMenu/MainDisplay");
            ExportTemplate(hero.gameObject,"MainMenu/BattlefieldHero");
            view.RefreshLayout();
            EditorSceneManager.SaveScene(scene,ScenePath);
            const string loginPath="Assets/BattleCities/Scenes/Login.unity";
            var ordered=new System.Collections.Generic.List<EditorBuildSettingsScene>();
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(loginPath))ordered.Add(new EditorBuildSettingsScene(loginPath,true));
            ordered.Add(new EditorBuildSettingsScene(ScenePath,true));
            ordered.AddRange(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath&&s.path!=loginPath));
            EditorBuildSettings.scenes=ordered.ToArray();
            AssetDatabase.SaveAssets();Selection.activeGameObject=canvasObject;
            Debug.Log("[MainMenu] Created scene, theme, seven reusable UI prefabs, input maps, and platform layouts.");
        }
        [MenuItem("Battle Cities/UI/Open Main Menu")]
        public static void Open()
        {if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
        private static MenuTheme CreateTheme()
        {
            var t=ScriptableObject.CreateInstance<MenuTheme>();
            t.HeadingFont=AssetDatabase.LoadAssetAtPath<Font>(Root+"Art/shared/fonts/BarlowCondensed-Bold.ttf");
            t.BodyFont=AssetDatabase.LoadAssetAtPath<Font>(Root+"Art/shared/fonts/BarlowCondensed-SemiBold.ttf");
            t.Battlefield=Art("main-menu/backgrounds/arena-web");t.PsgBattlefield=Art("main-menu/backgrounds/arena-psg1");
            t.AndroidLandscapeBattlefield=Art("main-menu/backgrounds/arena-android-landscape");t.AndroidPortraitBattlefield=Art("main-menu/backgrounds/arena-android-portrait");
            t.Background=Art("shared/backgrounds/soft-battlefield");t.RewardGarden=Art("main-menu/backgrounds/reward-garden");
            t.Logo=Art("main-menu/branding/battle-cities-logo");t.PlayButton=Art("shared/buttons/play-gold");
            t.BlueFrame=Art("shared/panels/blue-frame");t.CreamPanel=Art("shared/panels/nav-cream");t.GoldPanel=Art("shared/panels/gold-panel");
            t.SelectedPanel=Art("shared/panels/nav-gold");t.DarkPanel=Art("shared/panels/dark-inset");t.SilverFrame=Art("shared/panels/silver-frame");t.FocusRing=Art("shared/panels/focus-ring");t.Rounded=Art("shared/panels/round-white");
            t.NavigationIcons=new[]{"play","quarters","shop","ranking","socials"}.Select(s=>Art("reference-style-v2/shared/navigation/"+s)).ToArray();
            t.NavigationFocus=Art("reference-style-v2/shared/buttons/focus-blue");
            t.RankingGamingIcon=Art("reference-style-v2/shared/icons/ranking/gaming");
            t.RankingTradingIcon=Art("reference-style-v2/shared/icons/ranking/trading");
            t.SettingsIcon=Art("shared/icons/settings");t.ScoreIcon=Art("shared/icons/score-coin");t.HighScoreIcon=Art("shared/icons/high-score-shield");t.TrophyIcon=Art("shared/icons/trophy");t.TimerIcon=Art("shared/icons/timer");
            t.RewardIcons=new[]{"chest-silver-v2","chest-gold-v2","chest-bronze-v2","chest-cyan-v2"}.Select(s=>Art("shared/icons/"+s)).ToArray();
            t.PrizeCrates=new[]{"gold","orange","blue","black"}.Select(s=>Art("shared/icons/prize-crate-"+s)).ToArray();
            t.StepBadges=Enumerable.Range(1,4).Select(i=>Art("reference-style-v2/shared/icons/steps/step-"+i)).ToArray();
            AssetDatabase.CreateAsset(t,Root+"Settings/ArcadeMenuTheme.asset");return t;
        }
        private static Sprite Art(string path)
        {var s=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Art/"+path+".png");if(!s)throw new InvalidOperationException("Missing sprite "+path);return s;}
        private static void ImportArt()
        {
            AssetDatabase.Refresh();
            foreach(var path in Directory.GetFiles(Root+"Art","*.png",SearchOption.AllDirectories))
            {
                string p=path.Replace('\\','/');var imp=AssetImporter.GetAtPath(p) as TextureImporter;if(imp==null)continue;
                imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.spritePixelsPerUnit=(p.EndsWith("/shared/panels/blue-frame.png")||p.EndsWith("/shared/panels/navigation-container.png")||p.EndsWith("/shared/panels/button-leaderboard-container.png"))?700:100;
                bool rankingTabIcon=p.Contains("/shared/icons/ranking/");
                imp.alphaIsTransparency=true;imp.mipmapEnabled=rankingTabIcon;imp.isReadable=false;imp.filterMode=rankingTabIcon?FilterMode.Trilinear:FilterMode.Bilinear;imp.wrapMode=TextureWrapMode.Clamp;
                imp.maxTextureSize=2048;imp.textureCompression=TextureImporterCompression.Uncompressed;
                var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;imp.SetTextureSettings(settings);
                imp.spriteBorder=(p.EndsWith("/shared/panels/blue-frame.png")||p.EndsWith("/shared/panels/navigation-container.png")||p.EndsWith("/shared/panels/button-leaderboard-container.png"))?new Vector4(160,160,160,160):p.EndsWith("/shared/header/high-score-center.png")?new Vector4(160,0,160,0):p.Contains("/panels/")?new Vector4(32,32,32,32):Vector4.zero;
                var android=imp.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=2048;android.format=TextureImporterFormat.ASTC_6x6;imp.SetPlatformTextureSettings(android);
                imp.SaveAndReimport();
            }
        }
        private static InputActionAsset CreateInput()
        {
            var asset=ScriptableObject.CreateInstance<InputActionAsset>();
            foreach(bool psg in new[]{false,true})
            {
                var map=new InputActionMap(psg?"PSG1":"UI");asset.AddActionMap(map);
                var nav=map.AddAction("Navigate",InputActionType.PassThrough);nav.expectedControlType="Vector2";
                nav.AddBinding("<Gamepad>/dpad");nav.AddBinding("<Gamepad>/leftStick");
                nav.AddCompositeBinding("2DVector").With("Up","<Keyboard>/upArrow").With("Down","<Keyboard>/downArrow").With("Left","<Keyboard>/leftArrow").With("Right","<Keyboard>/rightArrow");
                nav.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Down","<Keyboard>/s").With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
                nav.AddCompositeBinding("2DVector").With("Up","<Keyboard>/numpad8").With("Down","<Keyboard>/numpad2").With("Left","<Keyboard>/numpad4").With("Right","<Keyboard>/numpad6");
                var submit=map.AddAction("Submit",InputActionType.Button);submit.AddBinding("<Keyboard>/enter");submit.AddBinding("<Keyboard>/space");submit.AddBinding(psg?"<Gamepad>/buttonEast":"<Gamepad>/buttonSouth");
                submit.AddBinding("<Keyboard>/numpadEnter");
                var cancel=map.AddAction("Cancel",InputActionType.Button);cancel.AddBinding("<Keyboard>/escape");cancel.AddBinding(psg?"<Gamepad>/buttonSouth":"<Gamepad>/buttonEast");
                var point=map.AddAction("Point",InputActionType.PassThrough);point.expectedControlType="Vector2";point.AddBinding("<Mouse>/position");point.AddBinding("<Touchscreen>/touch*/position");point.AddBinding("<Pen>/position");
                var click=map.AddAction("Click",InputActionType.PassThrough);click.expectedControlType="Button";click.AddBinding("<Mouse>/leftButton");click.AddBinding("<Touchscreen>/touch*/press");click.AddBinding("<Pen>/tip");
                var scroll=map.AddAction("ScrollWheel",InputActionType.PassThrough);scroll.expectedControlType="Vector2";scroll.AddBinding("<Mouse>/scroll");
            }
            AssetDatabase.CreateAsset(asset,Root+"Settings/MainMenuInput.asset");return asset;
        }
        private static RectTransform BuildLeaderboard(Transform parent,out Text message,out Text detail)
        {
            var root=Panel("Leaderboard",parent,Art("reference-style-v2/shared/panels/navigation-container"));
            var heading=Panel("Heading",root,theme.DarkPanel);Box(heading,.04f,.025f,.92f,.10f);
            var trophy=Img("Trophy",heading,theme.TrophyIcon);trophy.preserveAspect=true;
            trophy.rectTransform.anchorMin=trophy.rectTransform.anchorMax=new Vector2(0,.5f);
            trophy.rectTransform.pivot=new Vector2(0,.5f);trophy.rectTransform.anchoredPosition=new Vector2(10,0);
            trophy.rectTransform.sizeDelta=new Vector2(70,72);
            var title=Label("Title",heading,"PLAYER RANKINGS",32,Gold);
            title.alignment=TextAnchor.MiddleLeft;title.resizeTextForBestFit=false;title.horizontalOverflow=HorizontalWrapMode.Overflow;
            title.rectTransform.anchorMin=new Vector2(0,1);title.rectTransform.anchorMax=new Vector2(1,1);
            title.rectTransform.pivot=new Vector2(0,1);title.rectTransform.offsetMin=new Vector2(100,-51);title.rectTransform.offsetMax=new Vector2(-10,-10);
            var subtitle=Label("Availability",heading,"LOADING RANKINGS",21,Color.white);
            subtitle.alignment=TextAnchor.MiddleLeft;subtitle.resizeTextForBestFit=false;subtitle.horizontalOverflow=HorizontalWrapMode.Overflow;
            subtitle.rectTransform.anchorMin=new Vector2(0,1);subtitle.rectTransform.anchorMax=new Vector2(1,1);
            subtitle.rectTransform.pivot=new Vector2(0,1);subtitle.rectTransform.offsetMin=new Vector2(100,-84);subtitle.rectTransform.offsetMax=new Vector2(-10,-52);
            var round=Panel("Round status",root,theme.GoldPanel);Box(round,.05f,.14f,.9f,.105f);
            var roundWatch=Img("Watch",round,theme.TimerIcon);roundWatch.preserveAspect=true;Box(roundWatch.rectTransform,.035f,.20f,.145f,.60f);
            var roundHeading=Label("Heading",round,"TOP 10 EVERY 30 MINUTES",20,Navy,false);roundHeading.alignment=TextAnchor.MiddleLeft;Box(roundHeading.rectTransform,.22f,.10f,.75f,.28f);
            var roundText=Label("Label",round,"ROUND UNAVAILABLE",32,Navy,false);roundText.alignment=TextAnchor.MiddleLeft;Box(roundText.rectTransform,.22f,.40f,.75f,.47f);
            var columns=Panel("Columns",root,theme.DarkPanel);Box(columns,.05f,.245f,.9f,.085f);
            ConfigureLeaderboardColumns(columns,theme);
            var list=Panel("Scores",root,theme.CreamPanel);Box(list,.05f,.3f,.9f,.56f);
            var bigTrophy=Img("Empty state trophy",list,theme.TrophyIcon);bigTrophy.preserveAspect=true;Box(bigTrophy.rectTransform,.31f,.20f,.38f,.27f);
            message=Label("Message",list,"LOADING RANKINGS",26,Navy,false);Box(message.rectTransform,.04f,.52f,.92f,.09f);
            detail=Label("Detail",list,"Fetching live player standings.",21,Navy,false);detail.font=theme.BodyFont;Box(detail.rectTransform,.05f,.61f,.9f,.07f);
            var footer=Panel("Footer",root,theme.DarkPanel);Box(footer,.04f,.88f,.92f,.095f);
            var footerText=Label("Label",footer,"SEASON STANDINGS",25,new Color(1f,.878f,.43f));
            footerText.resizeTextForBestFit=false;footerText.alignment=TextAnchor.MiddleLeft;
            footerText.rectTransform.anchorMin=new Vector2(0,1);footerText.rectTransform.anchorMax=new Vector2(1,1);
            footerText.rectTransform.pivot=new Vector2(0,1);
            footerText.rectTransform.offsetMin=new Vector2(110,-43);footerText.rectTransform.offsetMax=new Vector2(-10,-10);
            var rewardCoin=CreateRewardCoin("Reward Coin",footer);
            rewardCoin.anchorMin=rewardCoin.anchorMax=new Vector2(0,.5f);
            rewardCoin.pivot=new Vector2(0,.5f);rewardCoin.anchoredPosition=new Vector2(14,0);
            rewardCoin.sizeDelta=new Vector2(78,78);
            var footerSubtitle=Label("Subtitle",footer,"Loading round status...",21,new Color(.96f,.97f,1f));
            footerSubtitle.font=theme.BodyFont;footerSubtitle.resizeTextForBestFit=false;footerSubtitle.alignment=TextAnchor.MiddleLeft;
            footerSubtitle.rectTransform.anchorMin=Vector2.zero;footerSubtitle.rectTransform.anchorMax=new Vector2(1,0);
            footerSubtitle.rectTransform.pivot=Vector2.zero;
            footerSubtitle.rectTransform.offsetMin=new Vector2(110,10);footerSubtitle.rectTransform.offsetMax=new Vector2(-10,43);
            var footerSubtitleShadow=footerSubtitle.GetComponent<Shadow>();
            footerSubtitleShadow.effectColor=new Color(0,0,0,.5f);footerSubtitleShadow.effectDistance=new Vector2(1,-1);
            return root;
        }
        private static RectTransform BuildHowItWorks(Transform parent)
        {
            var root=Panel("How It Works",parent,Art("reference-style-v2/shared/panels/navigation-container"));
            var paper=Panel("Paper",root,theme.CreamPanel);Box(paper,.012f,.10f,.976f,.80f);
            var label=Panel("Heading",root,theme.GoldPanel);Box(label,.025f,.015f,.30f,.36f);
            label.GetComponent<Image>().pixelsPerUnitMultiplier=5;
            var title=Label("Title",label,"HOW IT WORKS ?",36,Navy,false);title.resizeTextMinSize=20;Stretch(title.rectTransform);
            label.anchorMin=label.anchorMax=new Vector2(.025f,.985f);
            label.pivot=new Vector2(0,1);label.anchoredPosition=Vector2.zero;
            label.sizeDelta=new Vector2(title.preferredWidth+24,45);
            string[] headings={"PLAY","REACH TOP 10","EARN BATC"};
            string[] bodies={"Play battles\nand earn points","Reach the top 10\nbefore the round closes","Eligible rewards go\nto your linked wallet"};
            Sprite[] icons={theme.NavigationIcons[0],theme.NavigationIcons[3],theme.PrizeCrates[0]};
            for(int i=0;i<3;i++)
            {
                var step=Rect("Step "+(i+1),paper);Box(step,.02f+i*.325f,.20f,.32f,.75f);
                var icon=Img("Icon",step,icons[i]);icon.preserveAspect=true;Box(icon.rectTransform,0,.08f,.27f,.78f);
                var heading=Label("Title",step,headings[i],25,Navy,false);heading.alignment=TextAnchor.MiddleLeft;Box(heading.rectTransform,.30f,0,.69f,.34f);
                var body=Label("Body",step,bodies[i],22,Navy,false);body.font=theme.BodyFont;body.alignment=TextAnchor.UpperLeft;Box(body.rectTransform,.30f,.36f,.69f,.61f);
            }
            return root;
        }
        private static RectTransform CreateRewardCoin(string name,Transform parent)
        {
            var coin=Art("shared/icons/reward-star-coin");
            if(!coin)throw new InvalidOperationException("The reward coin sprite is missing.");
            var image=Img(name,parent,coin);
            image.type=Image.Type.Simple;image.preserveAspect=true;
            return image.rectTransform;
        }
        private static RectTransform Rect(string name,Transform parent)
        {var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);return (RectTransform)g.transform;}
        public static void ConfigureLeaderboardColumns(RectTransform columns,MenuTheme skin)
        {
            var oldText=columns.GetComponent<Text>();
            if(oldText)oldText.enabled=false;
            var background=columns.GetComponent<Image>();
            if(!background)
            {
                var existing=columns.Find("Background");
                background=existing?existing.GetComponent<Image>():null;
                if(!background)background=Rect("Background",columns).gameObject.AddComponent<Image>();
                Stretch(background.rectTransform);background.transform.SetAsFirstSibling();
            }
            background.sprite=skin.DarkPanel;background.type=Image.Type.Sliced;
            background.pixelsPerUnitMultiplier=2;background.raycastTarget=false;
            Box(columns,.05f,.245f,.9f,.085f);
            string[] names={"Rank","Player","Score","Reward"};
            string[] titles={"#","PLAYER","SCORE","MATCHES"};
            float[] left={.025f,.15f,.53f,.77f};
            float[] widths={.10f,.36f,.22f,.21f};
            for(int i=0;i<names.Length;i++)
            {
                var child=columns.Find(names[i]);
                var text=child?child.GetComponent<Text>():null;
                if(!text)
                {
                    var rect=Rect(names[i],columns);
                    text=rect.gameObject.AddComponent<Text>();
                    text.color=Color.white;
                    var shadow=rect.gameObject.AddComponent<Shadow>();
                    shadow.effectColor=new Color(0,0,0,.6f);shadow.effectDistance=new Vector2(1,-1);
                }
                text.text=titles[i];text.font=skin.HeadingFont;text.fontSize=20;
                text.resizeTextForBestFit=true;text.resizeTextMinSize=14;text.resizeTextMaxSize=20;
                text.alignment=i==1?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter;
                text.raycastTarget=false;Box(text.rectTransform,left[i],.05f,widths[i],.57f);
            }
            var scores=columns.parent.Find("Scores");
            if(scores)columns.SetSiblingIndex(scores.GetSiblingIndex());
        }
        private static Image Img(string name,Transform parent,Sprite sprite,Color? color=null)
        {var rt=Rect(name,parent);var image=rt.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color??Color.white;image.raycastTarget=false;return image;}
        private static RectTransform Panel(string name,Transform parent,Sprite sprite,Color? color=null)
        {if(!sprite&&!color.HasValue)return Rect(name,parent);var image=Img(name,parent,sprite,color);if(sprite)image.type=Image.Type.Sliced;return image.rectTransform;}
        private static RectTransform MaskPanel(string name,Transform parent,float inset)
        {var image=Img(name,parent,theme.Rounded);image.type=Image.Type.Sliced;Stretch(image.rectTransform,inset);image.gameObject.AddComponent<Mask>().showMaskGraphic=false;return image.rectTransform;}
        private static Text Label(string name,Transform parent,string value,int size,Color color,bool shadow=true)
        {
            var rt=Rect(name,parent);var text=rt.gameObject.AddComponent<Text>();text.font=theme.HeadingFont;text.fontSize=size;text.color=color;text.text=value;text.alignment=TextAnchor.MiddleCenter;
            text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=Mathf.RoundToInt(size*.65f);text.resizeTextMaxSize=size;
            if(shadow){var s=rt.gameObject.AddComponent<Shadow>();s.effectColor=new Color(0,.025f,.05f,.7f);s.effectDistance=new Vector2(1,-2);}
            return text;
        }
        private static Button MakeButton(RectTransform rect)
        {var image=rect.GetComponent<Image>();image.raycastTarget=true;var b=rect.gameObject.AddComponent<Button>();b.targetGraphic=image;var colors=b.colors;colors.highlightedColor=new Color(1,1,.94f);colors.pressedColor=new Color(.8f,.87f,.91f);colors.selectedColor=Color.white;colors.fadeDuration=.08f;b.colors=colors;return b;}
        private static void AddFocus(RectTransform rect,Button button)
        {rect.gameObject.AddComponent<MenuButtonVisual>().Configure(null,rect);}
        public static void Box(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=new Vector2(x,1-y-height);rect.anchorMax=new Vector2(x+width,1-y);rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;}
        public static void Stretch(RectTransform rect,float inset=0)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*inset;rect.offsetMax=-Vector2.one*inset;}
        private static void ExportTemplate(GameObject source,string path)
        {
            var copy=UnityEngine.Object.Instantiate(source);copy.name=source.name;
            foreach(var button in copy.GetComponentsInChildren<Button>(true))
            {button.onClick=new Button.ButtonClickedEvent();button.navigation=new Navigation{mode=Navigation.Mode.Automatic};}
            PrefabUtility.SaveAsPrefabAsset(copy,Root+"Prefabs/"+path+".prefab");UnityEngine.Object.DestroyImmediate(copy);
        }
    }
}
