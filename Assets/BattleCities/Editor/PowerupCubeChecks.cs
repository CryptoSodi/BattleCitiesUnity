using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using BattleCities.Core;
using BattleCities.UI;
using BattleCities.LevelEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace BattleCities.Editor
{
    public static class PowerupCubeChecks
    {
        const string Output="Builds/PowerupCubePreviews";
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var prefab=Resources.Load<GameObject>("PowerupCube");
            if(!prefab)throw new Exception("PowerupCube prefab missing");
            int checks=0;
            void Check(bool ok,string message){checks++;if(!ok)throw new Exception("POWERUP CUBE: "+message);}
            var root=new GameObject("Isolated cube preview");
            var camera=new GameObject("Cube preview camera",typeof(Camera)).GetComponent<Camera>();
            camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.035f,.07f,.12f);
            camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            var volume=new GameObject("Existing battle bloom",typeof(Volume)).GetComponent<Volume>();
            volume.isGlobal=true;volume.priority=10;volume.sharedProfile=Resources.Load<VolumeProfile>("BattleLook");
            var sun=new GameObject("Preview sunlight",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;
            sun.transform.rotation=Quaternion.Euler(55,-35,0);sun.intensity=1.5f;sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.65f,.75f,.85f);RenderSettings.ambientEquatorColor=new Color(.4f,.46f,.52f);RenderSettings.ambientGroundColor=new Color(.22f,.19f,.14f);
            var target=new RenderTexture(1920,1080,24);target.Create();camera.targetTexture=target;
            var meshes=new List<Mesh>();var materials=new List<Material>();
            try{
                var types=ShopCatalog.InventoryIds.Select(ShopCatalog.PowerupType).ToArray();
                for(int i=0;i<types.Length;i++){
                    var view=Object.Instantiate(prefab,root.transform).GetComponent<BattlePickupCube>();
                    var position=new Vector3((i%4)*1.8f,0,-(i/4)*1.8f);
                    view.Present(types[i],position,0);
                    Check(view.transform.position.y==.025f,"floor anchor stays fixed");
                    var floating=view.transform.Find("Floating power-up cube");
                    var shell=floating.Find("Gold frame and blue faces");
                    var renderers=shell.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                    foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    Check(Mathf.Abs(shell.TransformVector(Vector3.right).magnitude-.54f*BattlePickupSizing.TileWorldSize)<.002f,"smaller 54 percent tile cube "+types[i]);
                    Check(floating.Find("Power-up illustrations").childCount==5,"top and four side illustrations "+types[i]);
                    var faces=floating.Find("Power-up illustrations").GetComponentsInChildren<Renderer>();
                    Check(faces.All(r=>r.sharedMaterial.GetTexture("_BaseMap")==BattlePickupCube.Illustration(types[i]).texture),"correct shop illustration "+types[i]);
                    var sprite=BattlePickupCube.Illustration(types[i]);var rect=sprite.textureRect;
                    foreach(var face in faces){
                        var mesh=face.GetComponent<MeshFilter>().sharedMesh;var uv=mesh.uv;
                        Check(Mathf.Abs(uv.Min(v=>v.x)-rect.xMin/sprite.texture.width)<.0001f&&Mathf.Abs(uv.Max(v=>v.x)-rect.xMax/sprite.texture.width)<.0001f&&
                            Mathf.Abs(uv.Min(v=>v.y)-rect.yMin/sprite.texture.height)<.0001f&&Mathf.Abs(uv.Max(v=>v.y)-rect.yMax/sprite.texture.height)<.0001f,"entire sprite UV retained on every face "+types[i]);
                        Check(mesh.bounds.size.x<=.601f&&mesh.bounds.size.y<=.601f,"icon stays inset from gold frame");
                    }
                    var halo=view.transform.Find("Animated gold floor halo");var angle=halo.localRotation;
                    float haloSize=halo.localScale.x,hoverHeight=floating.localPosition.y;var bodyAngle=floating.localRotation;
                    view.Present(types[i],position,.25f);
                    Check(Quaternion.Angle(angle,halo.localRotation)>1,"gold floor halo rotates");
                    var light=view.GetComponentInChildren<Light>();
                    Check(light&&light.type==LightType.Point&&light.intensity>0&&light.shadows==LightShadows.None,"warm local light, no shadow-map overhead");
                    Check(renderers.SelectMany(r=>r.sharedMaterials).All(m=>m.IsKeywordEnabled("_EMISSION")),"shell supports bloom emission");
                    foreach(var renderer in renderers){
                        var source=renderer.sharedMaterials;
                        for(int slot=0;slot<source.Length;slot++){
                            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,slot);
                            if(source[slot].name.Contains("Gold"))Check(!block.HasColor("_BaseColor"),"gold frame keeps its original material");
                            else Check(block.GetColor("_BaseColor")==BattlePickupCube.PanelColor(types[i]),"per-kind panel colour applied "+types[i]);
                        }
                    }
                    Check(Mathf.Abs(view.transform.position.y-.025f)<.0001f&&Mathf.Abs(halo.localPosition.y-.006f)<.0001f,"halo stays on floor");
                    Check(floating.localPosition.y>.13f&&floating.localPosition.y<.25f&&Mathf.Abs(floating.localPosition.y-hoverHeight)>.01f,"cube hovers and bobs");
                    Check(Quaternion.Angle(bodyAngle,floating.localRotation)>5,"cube rotates independently of halo");
                    Check(Mathf.Abs(halo.localScale.x-haloSize)>.05f,"halo shrinks and expands");
                    float haloRadius=halo.GetComponent<MeshFilter>().sharedMesh.vertices.Max(v=>new Vector2(v.x,v.z).magnitude);
                    Check(Mathf.Abs(haloRadius*2-BattlePickupSizing.TileWorldSize)<.001f&&halo.localScale.x<=1,"halo maximum diameter is exactly one tile");
                    Check(view.GetComponentsInChildren<MeshFilter>().Count(m=>m.name=="Orbiting gold spark")==6,"six animated gold sparks");
                    view.Present(null,position,0);Check(!view.gameObject.activeSelf,"removed/collected drop hides");
                    view.Present(types[i],position,.25f);Check(view.gameObject.activeSelf,"a new drop reactivates");
                }
                camera.transform.rotation=Quaternion.Euler(55,25,0);camera.orthographicSize=3.35f;
                camera.transform.position=new Vector3(2.7f,.4f,-1.8f)-camera.transform.forward*20;
                Capture(camera,target,"all-powerup-cubes.png");
                Capture(camera,target,"powerup-panel-colours.png");
                foreach(var view in root.GetComponentsInChildren<BattlePickupCube>())view.transform.Find("Floating power-up cube/Power-up illustrations").gameObject.SetActive(false);
                Capture(camera,target,"cube-shells-check.png");
                Object.DestroyImmediate(root);root=new GameObject("Battlefield cube preview");
                var rig=Resources.Load<GameObject>("TvReplayRig").GetComponent<BattleGame>();
                var documentObject=new GameObject("Isolated map",typeof(LevelEditorDocument));
                var document=documentObject.GetComponent<LevelEditorDocument>();document.BuildPreviews=false;document.TrackUndo=false;
                document.MapRoot=new GameObject("Imported layout").transform;document.MapRoot.SetParent(documentObject.transform);
                document.BrickModel=rig.BrickModel;document.SteelModel=rig.SteelModel;document.BushModel=rig.BushModel;document.EagleModel=rig.EagleModel;
                document.TankModel=rig.TankModels[0];document.BrickSectionData=rig.BrickSectionData;document.GroundTextures=rig.GroundTextures;
                document.Import(Resources.Load<TextAsset>("Maps/01").text);
                BattleGame.BuildEditorMap(root.transform,document,materials,meshes);Object.DestroyImmediate(documentObject);
                var cube=Object.Instantiate(prefab,root.transform).GetComponent<BattlePickupCube>();
                cube.Present("shield",BattleGame.World(416,640),.25f);
                camera.transform.rotation=Quaternion.Euler(55,0,0);camera.orthographicSize=2.5f;
                camera.transform.position=BattleGame.World(416,688)-camera.transform.forward*25;
                Capture(camera,target,"battlefield-cube-preview.png");
                var widePosition=camera.transform.position;
                camera.orthographicSize=1.1f;
                camera.transform.position=BattleGame.World(416,640)+Vector3.up*.35f-camera.transform.forward*25;
                Capture(camera,target,"cube-icon-fit-closeup.png");
                var animation=new RenderTexture(960,540,24);animation.Create();camera.targetTexture=animation;
                try{for(int frame=0;frame<32;frame++){
                    cube.Present("shield",BattleGame.World(416,640),frame/12f);
                    Capture(camera,animation,"Animation/frame-"+frame.ToString("D3")+".png");
                }}finally{camera.targetTexture=target;camera.orthographicSize=2.5f;camera.transform.position=widePosition;animation.Release();Object.DestroyImmediate(animation);}
                cube.Present("freeze",BattleGame.World(480,672),.5f);Check(cube.transform.Find("Floating power-up cube/Power-up illustrations").childCount==5,"reused drop swaps all five emblems");
                Check(AssetDatabase.GetDependencies(PowerupCubeImport.PrefabPath,true).Contains(PowerupCubeImport.ModelPath),"prefab retains authored model in remote content dependencies");
                var carrier=new GameObject("Drop integration check");carrier.transform.SetParent(root.transform);carrier.SetActive(false);
                var game=carrier.AddComponent<BattleGame>();const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(BattleGame).GetField("authoringPreview",flags).SetValue(game,true);
                var sim=new BattleSimulation(new MapData());typeof(BattleGame).GetProperty("Simulation").SetValue(game,sim);
                sim.PickupType="shield";sim.PickupX=416;sim.PickupY=640;
                var sync=typeof(BattleGame).GetMethod("SyncPickupCube",flags);sync.Invoke(game,null);
                var live=carrier.GetComponentInChildren<BattlePickupCube>(true);
                Check(live&&live.gameObject.activeSelf,"live game creates a 3D drop");
                sim.PickupType=null;sync.Invoke(game,null);Check(!live.gameObject.activeSelf,"simulation collection hides world model");
                sim.PickupType="rapidlong";sim.PickupX=288;sync.Invoke(game,null);
                Check(live.gameObject.activeSelf&&Mathf.Abs(live.transform.position.x-4.5f)<.001f,"new drop replaces kind and position");
                typeof(BattleGame).GetMethod("IsolateTvReplayVisuals",flags).Invoke(game,null);
                Check(live.GetComponentsInChildren<Transform>(true).All(t=>t.gameObject.layer==BattleGame.TvReplayLayer),"TV replay isolates cube, icons and halo on its own layer");
                Debug.Log("POWERUP CUBE PASS: "+checks+" checks; all twelve kinds, world scale, floor placement, animated halo, removal/reuse, authored mesh dependency. Editor visual previews: "+Path.GetFullPath(Output));
            }finally{
                camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);
                Object.DestroyImmediate(root);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(sun.gameObject);
                Object.DestroyImmediate(volume.gameObject);
                foreach(var mesh in meshes)if(mesh)Object.DestroyImmediate(mesh);foreach(var material in materials)if(material)Object.DestroyImmediate(material);
            }
        }
        static void Capture(Camera camera,RenderTexture target,string name)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output+"/"+name));camera.Render();camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try{texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name,texture.EncodeToPNG());}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(texture);}
        }
    }
}
