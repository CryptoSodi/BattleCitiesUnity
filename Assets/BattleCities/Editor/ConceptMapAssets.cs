using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BattleCities.EditorTools
{
    // Explicit authoring utility. Never rebuilds assets or UI when entering a scene.
    public static class ConceptMapAssets
    {
        const string Folder = "Assets/BattleCities/Art/ConceptMapPlaceholders";
        static readonly Dictionary<string,Material> materials = new Dictionary<string,Material>();
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var palette = Resources.Load<EnvironmentPalette>("EnvironmentPalette");
            var entries = palette.Prefabs.ToList();
            foreach (string key in new[]{"building_blue","building_coral","bridge_concrete","bridge_timber","log","sandbag","mud","rubble","conveyor","barrel","tree_2x3","tree_3x2","tree_2x4","tree_2x2"})
            {
                string fullKey = "draft_"+key, path=Folder+"/"+fullKey+".prefab";
                var existing=AssetDatabase.LoadAssetAtPath<EnvironmentPiece>(path);
                if(existing) { if(!entries.Contains(existing))entries.Add(existing); continue; }
                var root = new GameObject(fullKey);
                try
                {
                    var piece=root.AddComponent<EnvironmentPiece>(); piece.Key=fullKey;
                    piece.Role=key.StartsWith("building")||key.StartsWith("tree")?"solidCover":key=="log"||key=="sandbag"||key=="barrel"?"destructibleObstacle":"groundDetail";
                    var visual=new GameObject("Replacement visual - swap model here"); visual.transform.SetParent(root.transform,false); piece.IntactVisual=visual;
                    BuildVisual(key, visual.transform, piece, palette);
                    var saved=PrefabUtility.SaveAsPrefabAsset(root,path).GetComponent<EnvironmentPiece>(); entries.Add(saved);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Undo.RecordObject(palette,"Add concept map replacement models"); palette.Prefabs=entries.ToArray(); EditorUtility.SetDirty(palette);
            AssetDatabase.SaveAssets(); Debug.Log("Concept map models registered: "+entries.Count+" total environment prefabs.");
        }
        static Material Mat(string name, Color color)
        {
            if(materials.TryGetValue(name,out var mat)&&mat)return mat;
            string path=Folder+"/"+name+".mat";
            mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};mat.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(mat,path);}
            materials[name]=mat;return mat;
        }
        static GameObject Shape(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material mat,Quaternion? rotation=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            if(rotation.HasValue)go.transform.localRotation=rotation.Value;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go;
        }
        static void BuildVisual(string key,Transform root,EnvironmentPiece piece,EnvironmentPalette palette)
        {
            var concrete=Mat("Concrete",new Color(.53f,.56f,.55f));var dark=Mat("Dark metal",new Color(.12f,.17f,.20f));
            var wood=Mat("Timber",new Color(.42f,.23f,.095f));var endgrain=Mat("Cut timber",new Color(.69f,.48f,.23f));
            var yellow=Mat("Safety yellow",new Color(.92f,.66f,.13f));
            void Cube(string name,Vector3 p,Vector3 s,Material m)=>Shape(root,name,PrimitiveType.Cube,p,s,m);
            if(key.StartsWith("building"))
            {
                var roof=Mat(key.EndsWith("blue")?"Blue roof":"Coral roof",key.EndsWith("blue")?new Color(.13f,.40f,.57f):new Color(.67f,.27f,.20f));
                Cube("Solid footprint",new Vector3(0,.29f,0),new Vector3(.98f,.58f,.98f),concrete);
                Cube("Roof",new Vector3(0,.62f,0),new Vector3(.99f,.09f,.99f),roof);
                for(int i=0;i<7;i++)Cube("Roof seam",new Vector3(-.42f+i*.14f,.675f,0),new Vector3(.017f,.017f,.97f),dark);
                Cube("Roof vent",new Vector3(.22f,.735f,.18f),new Vector3(.19f,.12f,.22f),concrete);
                Cube("Loading door",new Vector3(0,.22f,-.496f),new Vector3(.34f,.40f,.008f),dark);
            }
            else if(key.StartsWith("tree"))
            {
                int w=key[5]-'0',h=key[7]-'0';piece.FootprintWidth=w*64;piece.FootprintHeight=h*64;
                Cube("Solid island footprint",new Vector3(0,.015f,0),new Vector3(w,.045f,h),Mat("Forest island",new Color(.22f,.30f,.12f)));
                for(int x=0;x<w;x++)for(int y=0;y<h;y++)
                {
                    var source=palette.Prefabs.First(p=>p.Key==((x+y)%2==0?"forest_tree_a":"forest_tree_b"));
                    var tree=UnityEngine.Object.Instantiate(source.IntactVisual?source.IntactVisual:source.gameObject,root);tree.name="Existing tree model";
                    var component=tree.GetComponent<EnvironmentPiece>();if(component)UnityEngine.Object.DestroyImmediate(component);
                    tree.transform.localPosition=Vector3.zero;tree.transform.localRotation=Quaternion.identity;tree.transform.localScale=Vector3.one;
                    var renderers=tree.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                    float scale=.92f/Mathf.Max(bounds.size.x,bounds.size.z);tree.transform.localScale=Vector3.one*scale;
                    tree.transform.localPosition=new Vector3(x+.5f-w*.5f-bounds.center.x*scale,.035f-bounds.min.y*scale,-y-.5f+h*.5f-bounds.center.z*scale);
                }
            }
            else if(key.StartsWith("bridge"))
            {
                bool timber=key.EndsWith("timber");Cube("Passable deck",new Vector3(0,.015f,0),new Vector3(1,.055f,1),timber?wood:concrete);
                for(int i=0;i<12;i++)Cube(timber?"Deck plank":"Deck joint",new Vector3(0,.047f,-.46f+i*.084f),new Vector3(.98f,.008f,timber?.065f:.007f),timber?endgrain:dark);
                // Edge paint stays flat: no apparent railing blocking the one-cell route.
                Cube("West deck edge",new Vector3(-.47f,.052f,0),new Vector3(.035f,.012f,1),timber?wood:yellow);
                Cube("East deck edge",new Vector3(.47f,.052f,0),new Vector3(.035f,.012f,1),timber?wood:yellow);
            }
            else if(key=="log")
            {
                for(int i=0;i<3;i++)
                {
                    Shape(root,"Log",PrimitiveType.Cylinder,new Vector3(0,.16f,-.30f+i*.30f),new Vector3(.28f,.49f,.28f),wood,Quaternion.Euler(0,0,90));
                    Shape(root,"Cut end",PrimitiveType.Cylinder,new Vector3(.49f,.16f,-.30f+i*.30f),new Vector3(.25f,.008f,.25f),endgrain,Quaternion.Euler(0,0,90));
                }
            }
            else if(key=="sandbag")
            {
                var sand=Mat("Sandbags",new Color(.64f,.55f,.32f));
                for(int y=0;y<2;y++)for(int x=0;x<2;x++)for(int z=0;z<3;z++)
                    Cube("Sandbag",new Vector3(-.24f+x*.48f,.085f+y*.16f,-.32f+z*.32f),new Vector3(.46f,.15f,.30f),sand);
            }
            else if(key=="barrel")
            {
                var red=Mat("Fuel red",new Color(.65f,.15f,.085f));
                Shape(root,"Fuel drum",PrimitiveType.Cylinder,new Vector3(0,.28f,0),new Vector3(.83f,.28f,.83f),red);
                for(int i=0;i<2;i++)Shape(root,"Drum rim",PrimitiveType.Cylinder,new Vector3(0,.1f+i*.36f,0),new Vector3(.87f,.028f,.87f),dark);
                Cube("Warning stripe",new Vector3(0,.567f,0),new Vector3(.63f,.009f,.16f),yellow);
                Shape(root,"Cap",PrimitiveType.Cylinder,new Vector3(.22f,.58f,.13f),new Vector3(.12f,.025f,.12f),dark);
            }
            else if(key=="conveyor")
            {
                Cube("Inactive belt",new Vector3(0,.015f,0),new Vector3(1,.045f,1),dark);
                for(int i=0;i<10;i++)Cube("Belt rib",new Vector3(0,.043f,-.45f+i*.1f),new Vector3(.88f,.01f,.018f),concrete);
                Cube("West trim",new Vector3(-.46f,.05f,0),new Vector3(.04f,.02f,1),yellow);Cube("East trim",new Vector3(.46f,.05f,0),new Vector3(.04f,.02f,1),yellow);
                for(int i=0;i<3;i++)
                {
                    float z=-.32f+i*.32f;
                    Shape(root,"North arrow left",PrimitiveType.Cube,new Vector3(-.11f,.065f,z),new Vector3(.31f,.015f,.06f),yellow,Quaternion.Euler(0,-35,0));
                    Shape(root,"North arrow right",PrimitiveType.Cube,new Vector3(.11f,.065f,z),new Vector3(.31f,.015f,.06f),yellow,Quaternion.Euler(0,35,0));
                }
            }
            else if(key=="mud")
            {
                var mud=Mat("Mud",new Color(.27f,.18f,.10f));Cube("Passable mud",new Vector3(0,-.001f,0),new Vector3(1,.025f,1),mud);
                for(int i=0;i<4;i++)Cube("Wheel rut",new Vector3(-.3f+i*.2f,.014f,0),new Vector3(.05f,.008f,.86f),wood);
            }
            else if(key=="rubble")
            {
                for(int i=0;i<13;i++)
                {
                    float x=-.38f+(i*7%11)*.073f,z=-.38f+(i*5%13)*.06f;
                    Shape(root,"Passable rubble",PrimitiveType.Cube,new Vector3(x,.035f,z),new Vector3(.12f,.07f,.11f),concrete,Quaternion.Euler(0,i*37,0));
                }
            }
            foreach(var c in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        }
    }
}
