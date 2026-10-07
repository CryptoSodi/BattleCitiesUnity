#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using BattleCities.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.LevelEditor
{
    /// <summary>Explicit scene migration. Never rebuilds the authored UI on entry.</summary>
    public static class LevelEditorPaletteAppearance
    {
        const string Icons = "Assets/BattleCities/LevelEditor/Art/Palette";

        [MenuItem("Battle Cities/Level Editor/Apply campaign palette styling")]
        public static void ApplyCurrent()
        {
            var doc = UnityEngine.Object.FindAnyObjectByType<LevelEditorDocument>();
            if (!doc || EditorApplication.isPlaying) throw new InvalidOperationException("Open the LevelEditor scene outside Play mode first.");
            Apply(doc);
            EditorSceneManager.SaveScene(doc.gameObject.scene);
        }
        public static void Apply(LevelEditorDocument doc, bool regenerateThumbnails = false)
        {
            string before = doc.ToJson();
            var canvas = doc.gameObject.scene.GetRootGameObjects().First(g => g.name == "EditorCanvas");
            var content = (RectTransform)canvas.transform.Find("Workspace/ElementPalette/ScrollView/Viewport/Content");
            if (!content) throw new InvalidOperationException("The authored element palette is missing.");
            System.IO.Directory.CreateDirectory(Icons); AssetDatabase.Refresh();
            LevelEditorTreeLayout.EnsurePaletteItem(doc, content);
            LevelEditorBuildingLayout.EnsurePaletteItem(doc, content);
            var cards = content.GetComponentsInChildren<LevelEditorButton>(true).Where(b => !string.IsNullOrEmpty(b.BrushKey)).ToArray();
            // Remove only the palette entries excluded by the map audit. MapRoot is untouched.
            foreach (var card in cards)
            {
                var pair = card.BrushKey.Split(':');
                if (pair.Length != 2 || !Enum.TryParse(pair[0], out LevelElementKind kind) || (!LevelEditorCatalog.Allows(kind, pair[1]) || (kind == LevelElementKind.Terrain && !LevelEditorCatalog.Terrain.Contains(pair[1]))))
                    Undo.DestroyObjectImmediate(card.gameObject);
            }
            foreach (var child in content.Cast<Transform>().ToArray())
                if (child.GetComponent<TMP_Text>()) Undo.DestroyObjectImmediate(child.gameObject);
            var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing = 8; layout.padding = new RectOffset(1,1,0,0);
            layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = false;
            MakeHeading(content, "TerrainHeading", "TERRAIN");
            var terrain = MakeGrid(content, "TerrainItems", 3);
            MakeHeading(content, "PlacementHeading", "PLACEMENT");
            var placement = MakeGrid(content, "PlacementItems", 2);
            string[] ids = LevelEditorCatalog.Terrain.Select(k => "Terrain:" + k).Concat(new[] { "Prop:forest_tree_a", "Prop:" + LevelEditorCatalog.DefaultBuilding, "PlayerSpawn:player", "EnemySpawn:enemy", "Base:base" }).ToArray();
            foreach (string id in ids)
            {
                var key = id.Split(':')[1];
                var visual = content.GetComponentsInChildren<LevelEditorButton>(true).FirstOrDefault(b => b.BrushKey == id);
                if (!visual) throw new InvalidOperationException("Missing palette element " + id);
                Undo.SetTransformParent(visual.transform, (id.StartsWith("Terrain:") || LevelEditorCatalog.IsTree(key) || LevelEditorCatalog.IsBuilding(key)) ? terrain : placement, "Arrange element thumbnails");
                visual.transform.SetAsLastSibling();
                var rect = (RectTransform)visual.transform; rect.localScale = Vector3.one;
                var oldLayout = visual.GetComponent<UnityEngine.UI.LayoutElement>(); if (oldLayout) Undo.DestroyObjectImmediate(oldLayout);
                ConfigureFlat(visual, true);
                visual.Caption.text = LevelEditorCatalog.IsTree(key) ? "TREE" : LevelEditorCatalog.IsBuilding(key) ? "BUILDING" : key == "player" ? "PLAYER" : key == "enemy" ? "ENEMY" : key == "base" ? "BASE" : key.ToUpperInvariant();
                visual.Caption.fontSize = visual.Caption.fontSizeMax = 18; visual.Caption.fontSizeMin = 17;
                visual.Caption.fontStyle = FontStyles.Normal;
                Place(visual.Caption.rectTransform, 4, 70, 102, 20);
                var shadowRect = FindOrCreate("ContactShadow", visual.transform); Place(shadowRect,21,52,68,15);
                var shadow = shadowRect.GetComponent<TankGroundShadow>(); if (!shadow) shadow = shadowRect.gameObject.AddComponent<TankGroundShadow>();
                shadow.raycastTarget = false; shadow.color = new Color32(35,25,13,112); shadowRect.SetAsFirstSibling();
                var imageRect = FindOrCreate("Element", visual.transform); Place(imageRect,7,1,96,67);
                var image = imageRect.GetComponent<UnityEngine.UI.Image>(); if (!image) image = imageRect.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.sprite = Thumbnail(doc, key, regenerateThumbnails); image.type = UnityEngine.UI.Image.Type.Simple; image.preserveAspect = true; image.raycastTarget = false;
                visual.Caption.transform.SetAsLastSibling();
                EditorUtility.SetDirty(visual);
            }
            terrain.SetSiblingIndex(1); placement.SetSiblingIndex(3);
            foreach (var visual in canvas.GetComponentsInChildren<LevelEditorButton>(true))
                if (string.IsNullOrEmpty(visual.BrushKey)) ConfigureFlat(visual, false);
            var scroll = content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            scroll.verticalNormalizedPosition = 1;
            if (!LevelEditorCatalog.Allows(doc.BrushKind,doc.Brush)) { doc.BrushKind=LevelElementKind.Terrain; doc.Brush="brick"; doc.Tool="Select"; }
            foreach (var t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=5;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            doc.Status("Campaign elements / Choose Water for surface types.");
            EditorUtility.SetDirty(doc); EditorSceneManager.MarkSceneDirty(doc.gameObject.scene); AssetDatabase.SaveAssets();
            if (before != doc.ToJson()) throw new InvalidOperationException("Palette update altered the map document.");
        }
        static void ConfigureFlat(LevelEditorButton visual, bool thumbnail)
        {
            Undo.RecordObject(visual,"Style editor control"); Undo.RecordObject(visual.Background,"Style editor control");
            visual.Flat = true; visual.Thumbnail = thumbnail;
            visual.Blue = visual.Gold = null; visual.Background.sprite = null;
            visual.Background.type = UnityEngine.UI.Image.Type.Simple;
            var outline = visual.GetComponent<UnityEngine.UI.Outline>(); if(!outline)outline=Undo.AddComponent<UnityEngine.UI.Outline>(visual.gameObject);
            outline.effectDistance = new Vector2(1,-1); outline.useGraphicAlpha = true; visual.Border = outline;
            visual.Caption.fontStyle=FontStyles.Normal;visual.Caption.enableVertexGradient=false;
            if(!thumbnail){visual.Caption.fontSizeMax=21;visual.Caption.fontSizeMin=19;}
            visual.Refresh();
        }
        static void MakeHeading(RectTransform content,string name,string caption)
        {
            var rect=FindOrCreate(name,content);rect.SetAsLastSibling();
            var text=rect.GetComponent<TextMeshProUGUI>();if(!text)text=rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=ArcadeTextStyles.HeadingSdf;text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/BattleCities/LevelEditor/Art/CleanText.mat");
            text.text=caption;text.fontSize=20;text.fontStyle=FontStyles.Normal;text.color=ArcadeTextStyles.PrizeAmountFace;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;
            var size=rect.GetComponent<UnityEngine.UI.LayoutElement>();if(!size)size=rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();size.preferredHeight=24;
        }
        static RectTransform MakeGrid(RectTransform content,string name,int rows)
        {
            var rect=FindOrCreate(name,content);rect.SetAsLastSibling();
            var grid=rect.GetComponent<UnityEngine.UI.GridLayoutGroup>();if(!grid)grid=rect.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize=new Vector2(110,92);grid.spacing=new Vector2(8,8);grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;
            var size=rect.GetComponent<UnityEngine.UI.LayoutElement>();if(!size)size=rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();size.preferredHeight=rows*92+(rows-1)*8;
            return rect;
        }
        static RectTransform FindOrCreate(string name,Transform parent)
        {
            var found=parent.Find(name);if(found)return (RectTransform)found;
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Add palette artwork");return (RectTransform)go.transform;
        }
        static void Place(RectTransform rect,float x,float y,float w,float h)
        {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);}

        internal static Sprite Thumbnail(LevelEditorDocument doc,string key,bool regenerate)
        {
            string path=Icons+"/"+key+".png";
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite&&!regenerate)return sprite;
            var preview=new PreviewRenderUtility();var owned=new List<UnityEngine.Object>();
            bool asyncShaders=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            try
            {
                GameObject model = key=="brick"?doc.BrickModel:key=="steel"?doc.SteelModel:key=="jungle"?doc.BushModel:key=="base"?doc.EagleModel:key=="player"||key=="enemy"?doc.TankModel:null;
                if (LevelEditorCatalog.IsTree(key) || LevelEditorCatalog.IsBuilding(key)) model = Resources.Load<EnvironmentPalette>("EnvironmentPalette")?.Prefabs.FirstOrDefault(p => p && p.Key == key)?.gameObject;
                GameObject root;
                if(model)
                {
                    root=UnityEngine.Object.Instantiate(model);root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.Euler(0,180,0);
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>
                        {
                            var runtime=Resources.Load<Material>("RuntimeShaders/Terrain/"+source.name);
                            var mat=runtime?new Material(runtime):source.shader.name.Contains("glTF")?ConvertMaterial(source):new Material(source);
                            owned.Add(mat);
                            if((key=="player"||key=="enemy")&&(source.name=="PrimaryPaint"||source.name=="SecondaryArmor"))
                            {var tint=key=="player"?new Color(.93f,.66f,.09f):new Color(.65f,.19f,.12f);if(source.name=="SecondaryArmor")tint*=.68f;tint.a=1;mat.color=tint;}
                            return mat;
                        }).ToArray();
                    }
                }
                else
                {
                    root=new GameObject(key);var mesh=SurfaceMesh();owned.Add(mesh);root.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var water=Resources.Load<BattleVisualAssets>("BattleVisualAssets").WaterMaterial;
                    var material=key=="water"?new Material(water):new Material(Resources.Load<Shader>("BattleTerrainSurfaces"));owned.Add(material);
                    if(key=="ice"){material.SetFloat("_Kind",3);material.SetTexture("_RippleMap",water.GetTexture("_RippleMap"));}
                    root.AddComponent<MeshRenderer>().sharedMaterial=material;
                }
                preview.AddSingleGO(root);
                var renderers=root.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
                var camera=preview.camera;camera.orthographic=true;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                camera.transform.rotation=Quaternion.Euler(90,0,0);
                camera.transform.position=bounds.center-camera.transform.forward*10;
                float extent=0;
                for(int i=0;i<8;i++){var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var local=camera.transform.InverseTransformPoint(corner);extent=Mathf.Max(extent,Mathf.Abs(local.x),Mathf.Abs(local.y));}
                camera.orthographicSize=Mathf.Max(.1f,extent)*1.08f;
                preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-35,0);preview.lights[1].intensity=.65f;
                preview.ambientColor=new Color(.65f,.65f,.65f);
                preview.BeginPreview(new Rect(0,0,384,384),GUIStyle.none);preview.Render(true,true);preview.Render(true,true);
                var rendered=(RenderTexture)preview.EndPreview();var previous=RenderTexture.active;
                var texture=new Texture2D(384,384,TextureFormat.RGBA32,false);
                try{RenderTexture.active=rendered;texture.ReadPixels(new Rect(0,0,384,384),0,0);texture.Apply();}
                finally{RenderTexture.active=previous;}
                System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            finally{ShaderUtil.allowAsyncCompilation=asyncShaders;preview.Cleanup();foreach(var obj in owned)if(obj)UnityEngine.Object.DestroyImmediate(obj);}
        }
        static Material ConvertMaterial(Material source)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if(source.HasProperty("baseColorFactor"))mat.color=source.GetColor("baseColorFactor");
            if(source.HasProperty("baseColorTexture"))mat.mainTexture=source.GetTexture("baseColorTexture");
            if(source.HasProperty("metallicFactor"))mat.SetFloat("_Metallic",source.GetFloat("metallicFactor"));
            if(source.HasProperty("roughnessFactor"))mat.SetFloat("_Smoothness",1-source.GetFloat("roughnessFactor"));
            if(source.HasProperty("emissiveFactor"))
            {
                mat.SetColor("_EmissionColor",source.GetColor("emissiveFactor"));
                if(source.HasProperty("emissiveTexture"))mat.SetTexture("_EmissionMap",source.GetTexture("emissiveTexture"));
                mat.EnableKeyword("_EMISSION");
            }
            return mat;
        }
        static Mesh SurfaceMesh()
        {
            const int cells=12;var vertices=new List<Vector3>();var uv=new List<Vector2>();var shore=new List<Vector2>();var triangles=new List<int>();
            for(int y=0;y<=cells;y++)for(int x=0;x<=cells;x++){float px=(float)x/cells,py=(float)y/cells;vertices.Add(new Vector3(px-.5f,0,py-.5f));uv.Add(new Vector2(px,py));shore.Add(new Vector2(Mathf.Min(px,py,1-px,1-py),0));}
            for(int y=0;y<cells;y++)for(int x=0;x<cells;x++){int a=y*(cells+1)+x,b=a+1,c=a+cells+2,d=a+cells+1;triangles.AddRange(new[]{a,c,b,a,d,c});}
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,shore);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
#endif
