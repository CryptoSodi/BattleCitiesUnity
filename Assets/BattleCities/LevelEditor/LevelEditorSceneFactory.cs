#if UNITY_EDITOR
using System;
using System.Linq;
using BattleCities.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BattleCities.LevelEditor
{
    public static class LevelEditorSceneFactory
    {
        const string Folder = "Assets/BattleCities/LevelEditor/Art";
        static LevelEditorDocument doc;
        static PreBattleArt art;
        static ArcadeTextStyles styles;

        public static void Create()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before creating the scene.");
            if (System.IO.File.Exists("Assets/BattleCities/Scenes/LevelEditor.unity")) throw new InvalidOperationException("LevelEditor already exists. Open it to preserve your authored layout.");
            var sourceScene = EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/BattleCity.unity", OpenSceneMode.Additive);
            var source = sourceScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleGame>(true)).First();
            var brick = source.BrickModel; var steel = source.SteelModel; var bush = source.BushModel; var eagle = source.EagleModel; var tank = source.TankModels[0];
            EditorSceneManager.CloseScene(sourceScene, true);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            System.IO.Directory.CreateDirectory(Folder); System.IO.Directory.CreateDirectory("Assets/BattleCities/LevelDrafts"); AssetDatabase.Refresh();
            doc = new GameObject("LevelEditor").AddComponent<LevelEditorDocument>();
            doc.MapRoot = new GameObject("MapRoot").transform; doc.MapRoot.SetParent(doc.transform);
            doc.BrickModel = brick; doc.SteelModel = steel; doc.BushModel = bush; doc.EagleModel = eagle; doc.TankModel = tank;
            doc.MaterialKeys = new[] { "brick", "steel", "jungle", "water", "lava", "muddyWater", "ice", "grease", "quicksand", "city", "plaza", "road", "forestPath", "meadow", "player", "enemy", "base", "light" };
            var colors = new[] { new Color(.64f,.27f,.12f), new Color(.53f,.62f,.7f), new Color(.2f,.55f,.22f), new Color(.03f,.53f,.75f), new Color(.84f,.19f,.05f), new Color(.35f,.29f,.12f), new Color(.61f,.85f,.95f), new Color(.18f,.14f,.27f), new Color(.66f,.48f,.19f), new Color(.39f,.42f,.42f), new Color(.47f,.49f,.45f), new Color(.24f,.28f,.29f), new Color(.37f,.3f,.2f), new Color(.25f,.42f,.22f), new Color(.05f,.8f,.76f), new Color(.95f,.24f,.17f), new Color(.75f,.39f,.11f), Color.yellow };
            doc.PreviewMaterials = doc.MaterialKeys.Select((key,i) => Material(key, colors[i])).ToArray();
            doc.PreviewTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(Folder + "/MapPreview.renderTexture");
            if (!doc.PreviewTexture) { doc.PreviewTexture = new RenderTexture(1536,1024,24) { name = "MapPreview", antiAliasing = 2 }; AssetDatabase.CreateAsset(doc.PreviewTexture, Folder + "/MapPreview.renderTexture"); }
            var preview = new GameObject("Preview");
            doc.PreviewCamera = new GameObject("Map Camera", typeof(Camera)).GetComponent<Camera>(); doc.PreviewCamera.transform.SetParent(preview.transform);
            doc.PreviewCamera.orthographic = true; doc.PreviewCamera.nearClipPlane = .1f; doc.PreviewCamera.farClipPlane = 100;
            doc.PreviewCamera.clearFlags = CameraClearFlags.SolidColor; doc.PreviewCamera.backgroundColor = new Color(.075f,.13f,.18f); doc.PreviewCamera.targetTexture = doc.PreviewTexture;
            var light = new GameObject("Editor Sun", typeof(Light)).GetComponent<Light>(); light.transform.SetParent(preview.transform); light.type = LightType.Directional; light.intensity = 1.5f; light.transform.rotation = Quaternion.Euler(60,-30,0);
            RenderSettings.ambientLight = new Color(.55f,.61f,.67f); RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            var screen = new GameObject("Screen Camera", typeof(Camera), typeof(AudioListener)); screen.tag = "MainCamera"; screen.transform.SetParent(preview.transform);
            var camera = screen.GetComponent<Camera>(); camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.06f,.10f); camera.depth = 1;
            var canvas = new GameObject("EditorCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            art = Resources.Load<PreBattleArt>("PreBattleArt"); if (!art) throw new InvalidOperationException("Missing PreBattleArt");
            styles = new ArcadeTextStyles();
            var workspace = Rect("Workspace", canvas.transform); workspace.anchorMin=workspace.anchorMax=new Vector2(.5f,.5f); workspace.pivot=new Vector2(.5f,.5f); workspace.sizeDelta=new Vector2(1552,852);
            // The scene owns every RectTransform. No runtime layout rebuild overwrites author adjustments.
            var header = Panel("Title",workspace,art.tankTitlePanel,Color.white); Place(header,4,4,1544,64); header.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier=4;
            var title = Text("Title",header,"LEVEL EDITOR",28,true); Place(title.rectTransform,30,8,310,48);
            var icon = Rect("TitleIcon",header); var iconImage=icon.gameObject.AddComponent<UnityEngine.UI.Image>();
            var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>("Assets/BattleCities/UI/Settings/ArcadeMenuTheme.asset");
            if(theme&&theme.NavigationIcons.Length>1)iconImage.sprite=theme.NavigationIcons[1];iconImage.preserveAspect=true;iconImage.raycastTarget=false;
            Place(icon,18,12,40,40);Place(title.rectTransform,66,8,270,48);
            string[] actions={"NEW","OPEN","SAVE","SAVE AS","VALIDATE"};
            UnityEngine.Events.UnityAction[] calls={doc.NewLevel,doc.OpenMap,doc.Save,doc.SaveAs,doc.ValidateAction};
            for(int i=0;i<actions.Length;i++) {var button=Button(actions[i],header,actions[i]);Place((RectTransform)button.transform,910+i*124,10,118,44);UnityEventTools.AddPersistentListener(button.onClick,calls[i]);}
            var toolbar = Rect("Toolbar",workspace); Place(toolbar,4,78,1544,46);
            string[] tools={"Select","Paint","Erase"};
            for(int i=0;i<tools.Length;i++){var button=Button(tools[i],toolbar,tools[i].ToUpperInvariant());Place((RectTransform)button.transform,i*112,0,104,44);UnityEventTools.AddStringPersistentListener(button.onClick,doc.SetTool,tools[i]);button.GetComponent<LevelEditorButton>().ToolKey=tools[i];}
            var snap=Button("Snap",toolbar,"SNAP");Place((RectTransform)snap.transform,348,0,96,44);UnityEventTools.AddPersistentListener(snap.onClick,doc.CycleSnap);
            var undo=Button("Undo",toolbar,"UNDO");Place((RectTransform)undo.transform,454,0,96,44);UnityEventTools.AddPersistentListener(undo.onClick,doc.UndoEdit);
            var redo=Button("Redo",toolbar,"REDO");Place((RectTransform)redo.transform,560,0,96,44);UnityEventTools.AddPersistentListener(redo.onClick,doc.RedoEdit);
            var frame=Button("Frame",toolbar,"FIT MAP");Place((RectTransform)frame.transform,666,0,110,44);UnityEventTools.AddPersistentListener(frame.onClick,doc.FrameMap);
            doc.ToolLabel=Text("BrushStatus",toolbar,"SELECT / BRICK / 64 UNITS",22);doc.ToolLabel.color=Color.white;Place(doc.ToolLabel.rectTransform,794,0,740,44);
            var palette=Panel("ElementPalette",workspace,null,new Color(.94f,.91f,.82f));Place(palette,4,136,250,636);
            var paletteTitle=Text("Heading",palette,"ELEMENTS",26);Place(paletteTitle.rectTransform,16,8,220,34);
            var scrollRoot=Rect("ScrollView",palette);Place(scrollRoot,10,50,230,574);var scroll=scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            var viewport=Panel("Viewport",scrollRoot,null,Color.white);Stretch(viewport,0,0,0,0);viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var content=Rect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=5;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=content;
            void Group(string label,string[] keys,LevelElementKind kind)
            {
                var caption=Text(label,content,label,22);caption.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=34;
                foreach(string key in keys){string display=key.Replace("city_","").Replace("forest_","").Replace("_"," ").ToUpperInvariant();var b=Button(key,content,display);b.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=40;string id=kind+":"+key;UnityEventTools.AddStringPersistentListener(b.onClick,doc.ChooseBrush,id);b.GetComponent<LevelEditorButton>().BrushKey=id;}
            }
            Group("TERRAIN",LevelEditorCatalog.Terrain,LevelElementKind.Terrain);
            Group("SPAWNS",new[]{"player"},LevelElementKind.PlayerSpawn);Group("",new[]{"enemy"},LevelElementKind.EnemySpawn);Group("",new[]{"base"},LevelElementKind.Base);
            var mapPanel=Panel("MapPanel",workspace,null,new Color(.04f,.1f,.15f));Place(mapPanel,266,136,960,636);
            var mapView=Rect("MapViewport",mapPanel);Stretch(mapView,0,0,0,0);var raw=mapView.gameObject.AddComponent<UnityEngine.UI.RawImage>();raw.texture=doc.PreviewTexture;doc.Viewport=raw;
            var surface=mapView.gameObject.AddComponent<LevelEditorSurface>();surface.Document=doc;
            var gridRect=Rect("GridAndSelection",mapView);Stretch(gridRect,0,0,0,0);gridRect.gameObject.AddComponent<LevelEditorGrid>().Document=doc;
            var inspector=Panel("PropertiesPanel",workspace,null,new Color(.94f,.91f,.82f));Place(inspector,1238,136,310,636);
            var heading=Text("Heading",inspector,"ELEMENT PROPERTIES",25);Place(heading.rectTransform,18,10,276,40);
            doc.SelectionLabel=Text("Selection",inspector,"SELECT AN ELEMENT",21);Place(doc.SelectionLabel.rectTransform,18,62,276,170);doc.SelectionLabel.alignment=TextAlignmentOptions.TopLeft;
            doc.DamageToggle=Toggle("OverrideDamage",inspector,"OVERRIDE DAMAGE",246);UnityEventTools.AddPersistentListener(doc.DamageToggle.onValueChanged,doc.SetDamageOverride);
            var healthLabel=Text("HealthLabel",inspector,"HIT POINTS",21);Place(healthLabel.rectTransform,18,300,170,40);
            var fieldRoot=Panel("Health",inspector,null,Color.white);Place(fieldRoot,198,300,92,40);var input=fieldRoot.gameObject.AddComponent<TMP_InputField>();var inputText=Text("Text",fieldRoot,"1",23);Stretch(inputText.rectTransform,8,3,-8,-3);input.textViewport=fieldRoot;input.textComponent=inputText;input.contentType=TMP_InputField.ContentType.IntegerNumber;input.text="1";doc.HealthField=input;UnityEventTools.AddPersistentListener(input.onEndEdit,doc.SetHealth);
            doc.InvulnerableToggle=Toggle("Invulnerable",inspector,"INVULNERABLE",352);UnityEventTools.AddPersistentListener(doc.InvulnerableToggle.onValueChanged,doc.SetInvulnerable);
            doc.NormalToggle=Toggle("NormalShots",inspector,"NORMAL SHOTS",398);UnityEventTools.AddPersistentListener(doc.NormalToggle.onValueChanged,doc.SetNormal);
            doc.PowerToggle=Toggle("PowerShots",inspector,"POWER SHOTS + SPLASH",444);UnityEventTools.AddPersistentListener(doc.PowerToggle.onValueChanged,doc.SetPower);
            var note=Text("DamageHelp",inspector,"Terrain HP is per fragment.\nProps have one health pool.\nMore properties: Unity Inspector.",18);Place(note.rectTransform,18,496,276,74);
            var delete=Button("Delete",inspector,"DELETE ELEMENT");Place((RectTransform)delete.transform,18,578,276,42);UnityEventTools.AddPersistentListener(delete.onClick,doc.DeleteSelected);
            var footer=Panel("StatusFooter",workspace,art.statusPanel,Color.white);Place(footer,4,784,1544,64);TvStatusFooterLayout.ApplySkin(footer.GetComponent<UnityEngine.UI.Image>(),art.statusPanel);
            doc.StatusLabel=Text("Status",footer,"READY / Press Play for Canvas controls, or paint in Scene view.",22);Place(doc.StatusLabel.rectTransform,24,8,1120,48);
            var test=Button("TestLevel",footer,"TEST LEVEL");TvStatusFooterLayout.PlaceAction((RectTransform)test.transform);UnityEventTools.AddPersistentListener(test.onClick,doc.TestLevel);
            doc.Import(Resources.Load<TextAsset>("Maps/01").text);doc.RefreshSelection();doc.FrameMap();
            input.targetGraphic=fieldRoot.GetComponent<UnityEngine.UI.Image>();fieldRoot.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
            doc.PreviewCamera.cullingMask=~(1<<5);
            // Save the font materials used by the authored Canvas; transient style materials must not leak into the scene.
            foreach(var label in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                var transient=label.fontSharedMaterial;if(!transient||AssetDatabase.Contains(transient))continue;
                string path=Folder+"/"+(label.enableVertexGradient?"GoldText":"CleanText")+".mat";
                var saved=AssetDatabase.LoadAssetAtPath<Material>(path);if(!saved){saved=new Material(transient){hideFlags=HideFlags.None};AssetDatabase.CreateAsset(saved,path);}label.fontSharedMaterial=saved;
            }
            styles.Dispose();styles=null;
            LevelEditorPaletteAppearance.Apply(doc);
            LevelEditorPropertiesLayout.Apply(doc);
            LevelEditorTreeLayout.Apply(doc);
            LevelEditorBuildingLayout.Apply(doc);
            EditorSceneManager.SaveScene(scene,"Assets/BattleCities/Scenes/LevelEditor.unity");AssetDatabase.SaveAssets();Selection.activeGameObject=doc.gameObject;
            Debug.Log("Level editor created. All UI controls and map elements are saved in the scene hierarchy.",doc);
        }
        static Material Material(string key,Color color)
        {
            string path=Folder+"/"+key+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat)return mat;
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Editor "+key,color=color};mat.SetFloat("_Smoothness",.25f);AssetDatabase.CreateAsset(mat,path);return mat;
        }
        static RectTransform Rect(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
        static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        static void Stretch(RectTransform r,float left,float bottom,float right,float top){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(right,top);}
        static RectTransform Panel(string name,Transform parent,Sprite sprite,Color color){var r=Rect(name,parent);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.type=sprite?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.color=color;image.raycastTarget=false;return r;}
        static TMP_Text Text(string name,Transform parent,string caption,float size,bool gold=false){var r=Rect(name,parent);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.text=caption;text.fontSize=size;text.enableAutoSizing=true;text.fontSizeMin=size-3;text.fontSizeMax=size;text.alignment=TextAlignmentOptions.Left;text.raycastTarget=false;styles.Apply(text,gold?ArcadeTextTreatment.Gold:ArcadeTextTreatment.PrizeAmount);return text;}
        static UnityEngine.UI.Button Button(string name,Transform parent,string caption)
        {
            var r=Panel(name,parent,art.tankCostButton,Color.white);var image=r.GetComponent<UnityEngine.UI.Image>();image.raycastTarget=true;image.pixelsPerUnitMultiplier=4;
            var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.transition=UnityEngine.UI.Selectable.Transition.None;
            var text=Text("Caption",r,caption,23);text.alignment=TextAlignmentOptions.Center;Stretch(text.rectTransform,8,3,-8,-3);styles.ApplyCleanButton(text,false);
            var visual=r.gameObject.AddComponent<LevelEditorButton>();visual.Document=doc;visual.Background=image;visual.Caption=text;visual.Blue=art.tankCostButton;visual.Gold=art.tankCostButtonSelected;return button;
        }
        static UnityEngine.UI.Toggle Toggle(string name,Transform parent,string caption,float y)
        {
            var r=Panel(name,parent,null,Color.clear);r.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;Place(r,18,y,276,36);var bg=Panel("Box",r,null,new Color(.08f,.22f,.36f));Place(bg,0,3,30,30);bg.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var check=Panel("Check",bg,null,new Color(1,.78f,.14f));Stretch(check,6,6,-6,-6);
            var toggle=r.gameObject.AddComponent<UnityEngine.UI.Toggle>();toggle.targetGraphic=bg.GetComponent<UnityEngine.UI.Image>();toggle.graphic=check.GetComponent<UnityEngine.UI.Image>();
            var text=Text("Caption",r,caption,21);Place(text.rectTransform,42,0,234,36);return toggle;
        }
    }
}
#endif
