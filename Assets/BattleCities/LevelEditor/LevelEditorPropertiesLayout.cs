#if UNITY_EDITOR
using System;
using System.Linq;
using BattleCities.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BattleCities.LevelEditor
{
    public static class LevelEditorPropertiesLayout
    {
        [MenuItem("Battle Cities/Level Editor/Update map properties layout")]
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before updating the authored layout.");
            var doc=UnityEngine.Object.FindAnyObjectByType<LevelEditorDocument>();
            if(!doc)throw new InvalidOperationException("Open the LevelEditor scene first.");
            Apply(doc);EditorSceneManager.SaveScene(doc.gameObject.scene);
        }
        public static void Apply(LevelEditorDocument doc)
        {
            string before=doc.ToJson();
            var sourceScene=EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/BattleCity.unity",OpenSceneMode.Additive);
            try
            {
                var source=sourceScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BattleGame>(true)).First();
                doc.BrickSectionData=source.BrickSectionData;doc.GroundTextures=source.GroundTextures;
            }
            finally{EditorSceneManager.CloseScene(sourceScene,true);}
            var canvas=doc.gameObject.scene.GetRootGameObjects().First(g=>g.name=="EditorCanvas");
            var panel=(RectTransform)canvas.transform.Find("Workspace/PropertiesPanel");
            var mapTitle=Label(panel,"MapSettings","MAP SIZE (LARGE TILES)",23);Place(mapTitle.rectTransform,18,10,274,32);
            Place(Label(panel,"MapWidthLabel","WIDTH",19).rectTransform,18,45,82,24);
            Place(Label(panel,"MapHeightLabel","HEIGHT",19).rectTransform,112,45,82,24);
            doc.WidthField=Field(panel,"MapWidth",doc.WidthTiles.ToString(),18,72);
            doc.HeightField=Field(panel,"MapHeight",doc.HeightTiles.ToString(),112,72);
            var resize=Button(doc,panel,"ApplySize","APPLY",208,72,84,36);Bind(resize,doc.ApplyMapSize);
            Place(Label(panel,"TileSizeHeading","PAINT TILE SIZE",20).rectTransform,18,117,274,28);
            int[] sizes={64,32,16};string[] names={"LARGE","SMALL","FINE"};
            for(int i=0;i<3;i++)
            {
                var b=Button(doc,panel,"TileSize"+sizes[i],names[i],18+i*94,148,86,36);Clear(b);
                UnityEventTools.AddIntPersistentListener(b.onClick,doc.SetTileSize,sizes[i]);b.GetComponent<LevelEditorButton>().SizeKey=sizes[i];
            }
            doc.TileSizeHelp=Label(panel,"TileSizeHelp","SMALL: 2 x 2 per large tile",18);Place(doc.TileSizeHelp.rectTransform,18,190,274,24);
            doc.PropertyHeading=panel.Find("Heading").GetComponent<TMP_Text>();Place(doc.PropertyHeading.rectTransform,18,226,274,32);
            Place(doc.SelectionLabel.rectTransform,18,268,274,54);doc.SelectionLabel.fontSizeMax=20;doc.SelectionLabel.fontSizeMin=18;
            var damage=Rect(panel,"DamageProperties");Place(damage,18,330,274,216);doc.DamagePanel=damage.gameObject;
            Move(doc.DamageToggle.transform,damage,0,0,274,36);
            var healthLabel=panel.Find("HealthLabel")??damage.Find("HealthLabel");Move(healthLabel,damage,0,44,170,34);
            Move(doc.HealthField.transform,damage,182,44,92,34);
            Move(doc.InvulnerableToggle.transform,damage,0,94,274,36);
            Move(doc.NormalToggle.transform,damage,0,137,274,36);
            Move(doc.PowerToggle.transform,damage,0,180,274,36);
            var help=panel.Find("DamageHelp").GetComponent<TMP_Text>();doc.DamageHelp=help;doc.DeleteControl=panel.Find("Delete").gameObject;help.text="Select a placed wall to edit its damage.";help.fontSizeMax=17;help.fontSizeMin=16;Place(help.rectTransform,18,548,274,28);
            var surface=Rect(panel,"SurfaceProperties");Place(surface,18,330,274,206);doc.SurfacePanel=surface.gameObject;
            Place(Label(surface,"TypeHeading","SURFACE TYPE",21).rectTransform,0,0,274,28);
            for(int i=0;i<LevelEditorCatalog.SurfaceTypes.Length;i++)
            {
                string key=LevelEditorCatalog.SurfaceTypes[i];var button=Button(doc,surface,key,key.ToUpperInvariant(),i%2*142,36+i/2*48,132,40);
                Clear(button);UnityEventTools.AddStringPersistentListener(button.onClick,doc.SetSurfaceType,key);button.GetComponent<LevelEditorButton>().SurfaceKey=key;
            }
            var surfaceNote=Label(surface,"Help","Changes the selected patch, or the brush\nwhen chosen from the palette.",18);Place(surfaceNote.rectTransform,0,144,274,56);
            foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
            var preview=doc.GamePreview?doc.GamePreview:doc.gameObject.GetComponent<LevelEditorGamePreview>();
            if(!preview)preview=Undo.AddComponent<LevelEditorGamePreview>(doc.gameObject);doc.GamePreview=preview;preview.Document=doc;
            foreach(var e in doc.Elements)if(e.Visual){UnityEngine.Object.DestroyImmediate(e.Visual.gameObject);e.Visual=null;}
            doc.Snap=32;doc.SurfaceBrush="water";doc.BrushProperties=false;
            doc.PreviewCamera.orthographic=true;doc.PreviewCamera.transform.rotation=Quaternion.Euler(90,0,0);doc.FrameMap();
            var sun=doc.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).FirstOrDefault(l=>l.name=="Editor Sun");
            if(sun){sun.transform.rotation=Quaternion.Euler(55,-35,0);sun.color=new Color(1,.94f,.82f);sun.intensity=1.2f;sun.shadows=LightShadows.Soft;sun.shadowBias=.035f;sun.shadowNormalBias=.16f;sun.shadowStrength=.85f;}
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.75f,.85f);RenderSettings.ambientEquatorColor=new Color(.32f,.4f,.46f);RenderSettings.ambientGroundColor=new Color(.22f,.19f,.14f);
            doc.PreviewCamera.allowHDR=true;var cameraData=doc.PreviewCamera.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=true;cameraData.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            var look=doc.PreviewCamera.transform.parent.Find("BattleLook");if(!look){var go=new GameObject("BattleLook");go.transform.SetParent(doc.PreviewCamera.transform.parent,false);look=go.transform;}
            var volume=look.GetComponent<Volume>();if(!volume)volume=look.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;volume.sharedProfile=Resources.Load<VolumeProfile>("BattleLook");
            doc.RefreshSelection();preview.Rebuild();EditorUtility.SetDirty(doc);EditorSceneManager.MarkSceneDirty(doc.gameObject.scene);
            if(before!=doc.ToJson())throw new InvalidOperationException("Map properties layout changed the draft.");
            doc.Status("Map size, surface types and small tiles are ready. Your draft is preserved.");
        }
        static void Clear(UnityEngine.UI.Button button){while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);}
        static void Bind(UnityEngine.UI.Button button,UnityEngine.Events.UnityAction action){Clear(button);UnityEventTools.AddPersistentListener(button.onClick,action);}
        static RectTransform Rect(Transform parent,string name)
        {var found=parent.Find(name);if(found)return (RectTransform)found;var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Add map properties control");return (RectTransform)go.transform;}
        static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        static void Move(Transform item,Transform parent,float x,float y,float w,float h){Undo.SetTransformParent(item,parent,"Arrange map properties");Place((RectTransform)item,x,y,w,h);}
        static TMP_Text Label(Transform parent,string name,string caption,float size)
        {
            var rect=Rect(parent,name);var text=rect.GetComponent<TextMeshProUGUI>();if(!text)text=rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text=caption;text.font=ArcadeTextStyles.HeadingSdf;text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/BattleCities/LevelEditor/Art/CleanText.mat");
            text.fontStyle=FontStyles.Normal;text.enableVertexGradient=false;text.enableAutoSizing=true;text.fontSizeMax=size;text.fontSizeMin=size-2;text.fontSize=size;text.color=ArcadeTextStyles.PrizeAmountFace;text.raycastTarget=false;text.alignment=TextAlignmentOptions.MidlineLeft;return text;
        }
        static TMP_InputField Field(Transform parent,string name,string value,float x,float y)
        {
            var rect=Rect(parent,name);Place(rect,x,y,80,36);var image=rect.GetComponent<UnityEngine.UI.Image>();if(!image)image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.98f,.98f,.94f);image.raycastTarget=true;
            var field=rect.GetComponent<TMP_InputField>();if(!field)field=rect.gameObject.AddComponent<TMP_InputField>();
            var text=Label(rect,"Text",value,23);Place(text.rectTransform,8,2,64,32);field.textViewport=rect;field.textComponent=text;field.targetGraphic=image;field.contentType=TMP_InputField.ContentType.IntegerNumber;field.characterLimit=2;field.SetTextWithoutNotify(value);return field;
        }
        static UnityEngine.UI.Button Button(LevelEditorDocument doc,Transform parent,string name,string caption,float x,float y,float w,float h)
        {
            var rect=Rect(parent,name);Place(rect,x,y,w,h);var image=rect.GetComponent<UnityEngine.UI.Image>();if(!image)image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.raycastTarget=true;
            var button=rect.GetComponent<UnityEngine.UI.Button>();if(!button)button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.transition=UnityEngine.UI.Selectable.Transition.None;
            var text=Label(rect,"Caption",caption,20);Place(text.rectTransform,4,2,w-8,h-4);text.alignment=TextAlignmentOptions.Center;
            var visual=rect.GetComponent<LevelEditorButton>();if(!visual)visual=rect.gameObject.AddComponent<LevelEditorButton>();visual.Document=doc;visual.Background=image;visual.Caption=text;visual.Flat=true;
            var border=rect.GetComponent<UnityEngine.UI.Outline>();if(!border)border=rect.gameObject.AddComponent<UnityEngine.UI.Outline>();border.effectDistance=new Vector2(1,-1);visual.Border=border;visual.Refresh();return button;
        }
    }
}
#endif
