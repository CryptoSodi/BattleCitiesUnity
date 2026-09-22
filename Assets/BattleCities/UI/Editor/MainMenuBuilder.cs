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
            var background=Img("World backdrop",root,theme.Background);Stretch(background.rectTransform);
            var tint=Img("Backdrop shade",root,null,new Color(0,.14f,.22f,.18f));Stretch(tint.rectTransform);
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
            var frame=Panel("Main Display",content,Art("reference-style-v2/shared/panels/navigation-container"));
            var television=Panel("TV Frame",frame,theme.SilverFrame);
            var televisionViewport=Panel("TV Background Viewport",frame,theme.Rounded);
            var televisionViewportImage=televisionViewport.GetComponent<UnityEngine.UI.Image>();
            televisionViewportImage.type=UnityEngine.UI.Image.Type.Sliced;
            televisionViewportImage.preserveAspect=false;
            televisionViewportImage.raycastTarget=false;
            var televisionMask=televisionViewport.gameObject.AddComponent<UnityEngine.UI.Mask>();
            televisionMask.showMaskGraphic=false;
            var televisionBackground=Img("TV Background",televisionViewport,theme.Battlefield);
            televisionBackground.preserveAspect=false;



            var hero=Panel("Battlefield Hero",frame,null,new Color(1,1,1,0));


            var logo=Img("Battle Cities logo",hero,theme.Logo);logo.preserveAspect=true;
            var start=Img("Start Battle",hero,theme.PlayButton);start.preserveAspect=true;
            var startButton=MakeButton(start.rectTransform);AddFocus(start.rectTransform,startButton);
            var startLabel=Label("Editable label",start.rectTransform,"START",74,new Color32(48,28,3,255),false);Box(startLabel.rectTransform,.34f,.10f,.46f,.72f);
            var hint=Panel("A Select hint",hero,theme.DarkPanel);
            var hintLabel=Label("Label",hint,"A   SELECT",25,Color.white);Stretch(hintLabel.rectTransform);
            var rewards=BuildRewards(frame);
            var nav=Panel("Navigation",content,theme.BlueFrame);
            var tabs=new Button[5];string[] names={"PLAY","QUARTERS","SHOP","RANKING","SOCIALS"};
            for(int i=0;i<5;i++)
            {
                var tile=Panel(names[i],nav,i==0?theme.SelectedPanel:theme.CreamPanel);
                var icon=Img("Icon",tile,theme.NavigationIcons[i]);icon.preserveAspect=true;Box(icon.rectTransform,.12f,.04f,.76f,.68f);
                var label=Label("Label",tile,names[i],29,Navy,false);Box(label.rectTransform,.03f,.72f,.94f,.22f);
                tabs[i]=MakeButton(tile);AddFocus(tile,tabs[i]);
            }
            var board=BuildLeaderboard(content,out var retry,out var boardMessage,out var boardDetail);
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
            view.Configure(theme,safe,content,header,frame,hero,rewards,nav,board,how,controls,logo.rectTransform,start.rectTransform,hint,modal,
                startButton,settings,retry,close,tabs,player,score,hi,dialogTitle,body,boardMessage,boardDetail,actions,module);
            UnityEventTools.AddPersistentListener(startButton.onClick,view.StartBattle);
            UnityEventTools.AddPersistentListener(settings.onClick,view.OpenSettings);
            UnityEventTools.AddPersistentListener(retry.onClick,view.RetryLeaderboard);
            UnityEventTools.AddPersistentListener(close.onClick,view.Back);
            UnityEventTools.AddPersistentListener(tabs[0].onClick,view.PlayTab);
            UnityEventTools.AddPersistentListener(tabs[1].onClick,view.OpenQuarters);
            UnityEventTools.AddPersistentListener(tabs[2].onClick,view.OpenShop);
            UnityEventTools.AddPersistentListener(tabs[3].onClick,view.OpenRanking);
            UnityEventTools.AddPersistentListener(tabs[4].onClick,view.OpenSocials);
            // Reusable component prefabs are clean templates; scene-specific events stay in the scene.
            ReferenceHeaderArt.Import();
            ReferenceNavigationArt.ApplyTo(nav);
            ReferenceInstructionArt.ApplyTo(controls,how);
            ReferenceStartArt.ApplyTo(start.rectTransform,theme);
            ReferenceHeaderArt.ApplyHeader(header,theme);
            ReferenceRewardArt.ApplyTo(rewards);
            ExportTemplate(tabs[1].gameObject,"Shared/NavigationTile");
            ExportTemplate(start.gameObject,"Shared/PlayButton");
            ExportTemplate(header.GetChild(1).gameObject,"Shared/StatusCard");
            ExportTemplate(rewards.Find("Garden/Reward 1").gameObject,"Shared/RewardPodium");
            ExportTemplate(how.gameObject,"MainMenu/HowItWorks");
            ExportTemplate(board.gameObject,"MainMenu/LeaderboardPanel");
            ExportTemplate(frame.gameObject,"MainMenu/MainDisplay");
            ExportTemplate(hero.gameObject,"MainMenu/BattlefieldHero");
            view.RefreshLayout();
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
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
            t.Battlefield=Art("reference-style-v2/main-menu/backgrounds/tv-arena");t.PsgBattlefield=Art("reference-style-v2/main-menu/backgrounds/psg1/tv-arena-psg1");t.Background=Art("shared/backgrounds/soft-battlefield");t.RewardGarden=Art("main-menu/backgrounds/reward-garden");
            t.Logo=Art("main-menu/branding/battle-cities-logo");t.PlayButton=Art("shared/buttons/play-gold");
            t.BlueFrame=Art("shared/panels/blue-frame");t.CreamPanel=Art("shared/panels/nav-cream");t.GoldPanel=Art("shared/panels/gold-panel");
            t.SelectedPanel=Art("shared/panels/nav-gold");t.DarkPanel=Art("shared/panels/dark-inset");t.SilverFrame=Art("shared/panels/silver-frame");t.FocusRing=Art("shared/panels/focus-ring");t.Rounded=Art("shared/panels/round-white");
            t.NavigationIcons=new[]{"play","quarters","shop","ranking","socials"}.Select(s=>Art("reference-style-v2/shared/navigation/"+s)).ToArray();
            t.SettingsIcon=Art("shared/icons/settings");t.ScoreIcon=Art("shared/icons/score-coin");t.HighScoreIcon=Art("shared/icons/high-score-shield");t.TrophyIcon=Art("shared/icons/trophy");
            t.RewardIcons=new[]{"chest-silver-v2","chest-gold-v2","chest-bronze-v2","chest-cyan-v2"}.Select(s=>Art("shared/icons/"+s)).ToArray();
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
                imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.spritePixelsPerUnit=100;
                imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.isReadable=false;imp.filterMode=FilterMode.Bilinear;imp.wrapMode=TextureWrapMode.Clamp;
                imp.maxTextureSize=2048;imp.textureCompression=TextureImporterCompression.Uncompressed;
                var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;imp.SetTextureSettings(settings);
                imp.spriteBorder=p.Contains("/panels/")?new Vector4(32,32,32,32):Vector4.zero;
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
                var submit=map.AddAction("Submit",InputActionType.Button);submit.AddBinding("<Keyboard>/enter");submit.AddBinding("<Keyboard>/space");submit.AddBinding(psg?"<Gamepad>/buttonEast":"<Gamepad>/buttonSouth");
                var cancel=map.AddAction("Cancel",InputActionType.Button);cancel.AddBinding("<Keyboard>/escape");cancel.AddBinding(psg?"<Gamepad>/buttonSouth":"<Gamepad>/buttonEast");
                var point=map.AddAction("Point",InputActionType.PassThrough);point.expectedControlType="Vector2";point.AddBinding("<Mouse>/position");point.AddBinding("<Touchscreen>/touch*/position");point.AddBinding("<Pen>/position");
                var click=map.AddAction("Click",InputActionType.PassThrough);click.expectedControlType="Button";click.AddBinding("<Mouse>/leftButton");click.AddBinding("<Touchscreen>/touch*/press");click.AddBinding("<Pen>/tip");
            }
            AssetDatabase.CreateAsset(asset,Root+"Settings/MainMenuInput.asset");return asset;
        }
        private static RectTransform BuildRewards(Transform parent)
        {
            var root=Panel("Rewards",parent,null,new Color(1,1,1,0));
            var headerBar=Panel("Header Bar",root,theme.DarkPanel);Box(headerBar,0,0,1,.16f);
            var trophy=Img("Trophy",headerBar,theme.TrophyIcon);trophy.preserveAspect=true;Box(trophy.rectTransform,.025f,.04f,.07f,.92f);
            var title=Label("Title",headerBar,"TOP 10 EVERY 30 MINUTES",34,Gold);title.alignment=TextAnchor.MiddleLeft;Box(title.rectTransform,.105f,.02f,.87f,.96f);

            var garden=MaskPanel("Garden",root,0);Box(garden,0,.16f,1,.84f);
            var backdrop=Img("Backdrop",garden,theme.RewardGarden);Stretch(backdrop.rectTransform);
            string[] ranks={"2ND","1ST","3RD","4TH–10TH"};string[] values={"750 $BATC","1,000 $BATC","500 $BATC","250 $BATC"};
            Color[] colors={new Color(.35f,.57f,.82f),new Color(1,.74f,.1f),new Color(.9f,.38f,.13f),new Color(.08f,.66f,1)};
            for(int i=0;i<4;i++)
            {
                var item=Rect("Reward "+i,garden);Box(item,.09f+i*.205f,.025f,.205f,.94f);
                var chest=Img("Chest",item,theme.RewardIcons[i]);chest.preserveAspect=false;Box(chest.rectTransform,i==1?0:.05f,i==1?0:.08f,i==1?1:.90f,i==1?.73f:.65f);
                var pedestal=Panel("Podium",item,theme.CreamPanel,colors[i]);Box(pedestal,.02f,.69f,.96f,.30f);
                var rank=Label("Rank",pedestal,ranks[i],28,Color.white);Box(rank.rectTransform,.03f,.04f,.94f,.44f);
                var amount=Label("Reward",pedestal,values[i],26,i==1?Navy:Color.white);Box(amount.rectTransform,.03f,.47f,.94f,.43f);
            }
            return root;
        }
        private static RectTransform BuildLeaderboard(Transform parent,out Button retry,out Text message,out Text detail)
        {
            var root=Panel("Leaderboard",parent,Art("reference-style-v2/shared/panels/navigation-container"));
            var heading=Panel("Heading",root,theme.DarkPanel);Box(heading,.04f,.025f,.92f,.10f);
            var trophy=Img("Trophy",heading,theme.TrophyIcon);trophy.preserveAspect=true;Box(trophy.rectTransform,.015f,.08f,.19f,.84f);
            var title=Label("Title",heading,"REWARDS LEADERBOARD",27,Gold);Box(title.rectTransform,.21f,.02f,.77f,.57f);
            var subtitle=Label("Availability",heading,"LIVE BOARD UNAVAILABLE",18,Color.white);Box(subtitle.rectTransform,.21f,.58f,.77f,.32f);
            var round=Panel("Round status",root,theme.GoldPanel);Box(round,.05f,.14f,.9f,.105f);
            var roundText=Label("Label",round,"ROUND STATUS\nROUND UNAVAILABLE",27,Navy,false);Box(roundText.rectTransform,.04f,.08f,.92f,.84f);
            var columns=Label("Columns",root,"#      PLAYER                SCORE       REWARD",17,Color.white);Box(columns.rectTransform,.07f,.255f,.86f,.035f);
            var list=Panel("Scores",root,theme.CreamPanel);Box(list,.05f,.3f,.9f,.56f);
            var bigTrophy=Img("Empty state trophy",list,theme.TrophyIcon);bigTrophy.preserveAspect=true;Box(bigTrophy.rectTransform,.31f,.20f,.38f,.27f);
            message=Label("Message",list,"LIVE SCORES UNAVAILABLE",26,Navy,false);Box(message.rectTransform,.04f,.52f,.92f,.09f);
            detail=Label("Detail",list,"Leaderboard service not connected.",21,Navy,false);detail.font=theme.BodyFont;Box(detail.rectTransform,.05f,.61f,.9f,.07f);
            var retryRect=Panel("Retry",list,theme.BlueFrame);Box(retryRect,.28f,.72f,.44f,.11f);
            var retryText=Label("Label",retryRect,"RETRY",26,Color.white);Stretch(retryText.rectTransform);retry=MakeButton(retryRect);AddFocus(retryRect,retry);
            var footer=Panel("Footer",root,theme.DarkPanel);Box(footer,.04f,.88f,.92f,.095f);
            var footerText=Label("Label",footer,"BATC REWARDS\nPlay. Earn. Climb the leaderboard.",24,Gold);Stretch(footerText.rectTransform);
            return root;
        }
        private static RectTransform BuildHowItWorks(Transform parent)
        {
            var root=Panel("How It Works",parent,Art("reference-style-v2/shared/panels/navigation-container"));
            var paper=Panel("Paper",root,theme.CreamPanel);Box(paper,.012f,.10f,.976f,.80f);
            var label=Panel("Heading",root,theme.GoldPanel);Box(label,.025f,.015f,.24f,.27f);
            var title=Label("Title",label,"HOW IT WORKS",28,Navy,false);Stretch(title.rectTransform);
            string[] headings={"PLAY","REACH TOP 10","EARN BATC"};
            string[] bodies={"Play battles\nand earn points","Reach the top 10\nbefore the round closes","Eligible rewards go\nto your linked wallet"};
            Sprite[] icons={theme.NavigationIcons[0],theme.NavigationIcons[3],theme.RewardIcons[1]};
            for(int i=0;i<3;i++)
            {
                var step=Rect("Step "+(i+1),paper);Box(step,.02f+i*.325f,.20f,.32f,.75f);
                var icon=Img("Icon",step,icons[i]);icon.preserveAspect=true;Box(icon.rectTransform,0,.08f,.27f,.78f);
                var heading=Label("Title",step,headings[i],25,Navy,false);heading.alignment=TextAnchor.MiddleLeft;Box(heading.rectTransform,.30f,0,.69f,.34f);
                var body=Label("Body",step,bodies[i],22,Navy,false);body.font=theme.BodyFont;body.alignment=TextAnchor.UpperLeft;Box(body.rectTransform,.30f,.36f,.69f,.61f);
            }
            return root;
        }
        private static RectTransform Rect(string name,Transform parent)
        {var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);return (RectTransform)g.transform;}
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
        {var ring=Img("Focus outline",rect,theme.FocusRing);ring.type=Image.Type.Sliced;Stretch(ring.rectTransform,-4);ring.enabled=false;rect.gameObject.AddComponent<MenuButtonVisual>().Configure(ring,rect);}
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
