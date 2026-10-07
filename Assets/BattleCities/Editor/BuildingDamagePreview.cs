using System;
using System.IO;
using System.Linq;
using BattleCities.Core;
using UnityEditor;
using UnityEngine;

namespace BattleCities.EditorTools
{
    public static class BuildingDamagePreview
    {
        public static void Render()
        {
            const int size=420;
            var sheet=new Texture2D(size*4,size*2,TextureFormat.RGB24,false);
            var prefab=Resources.Load<EnvironmentPalette>("EnvironmentPalette").Prefabs.First(p=>p.Key=="draft_bld_harbor_warehouse_blue_3x2");
            for(int column=0;column<4;column++)for(int row=0;row<2;row++)
            {
                var preview=new PreviewRenderUtility();Material ground=null;
                try
                {
                    var map=new MapData{field=new FieldData(),objects=new[]{new MapObjectData{type=prefab.Key,role=prefab.Role,x=256,y=128,width=192,height=128}},terrain=new Core.TerrainData{regions=Array.Empty<Region>()}};
                    var sim=new BattleSimulation(map);var cells=sim.Terrain.Where(w=>w.IsBuildingSection).ToArray();
                    var center=new Vector3(5.5f,0,-3);
                    var piece=UnityEngine.Object.Instantiate(prefab,center,Quaternion.identity);preview.AddSingleGO(piece.gameObject);piece.BindBuilding(cells);
                    // Damage the front-left corner where both wall and roof are visible.
                    var corner=cells.Where(w=>w.Bounds.X<320&&w.Bounds.Y>=192).ToArray();
                    foreach(var w in corner)
                    {
                        int hits=column==0?0:column==1?1:column==2?3:3;
                        for(int i=0;i<hits;i++)sim.DestroyWall(w,new ShotState{Damage=1,Direction=Facing.Up});
                    }
                    if(column==3)foreach(var w in cells.Where(w=>w.Bounds.X>=320&&w.Bounds.X<384))for(int i=0;i<3;i++)sim.DestroyWall(w,new ShotState{Damage=1,Direction=Facing.Up});
                    piece.RefreshVisual();
                    var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Review ground";floor.transform.position=center+Vector3.down*.065f;floor.transform.localScale=new Vector3(9,.1f,9);UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
                    ground=new Material(Shader.Find("Universal Render Pipeline/Lit"));ground.color=new Color(.19f,.22f,.24f);floor.GetComponent<Renderer>().sharedMaterial=ground;preview.AddSingleGO(floor);
                    preview.camera.orthographic=true;preview.camera.orthographicSize=2.3f;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100;
                    preview.camera.transform.position=center+(row==0?new Vector3(0,12,0):new Vector3(6,7,-8));preview.camera.transform.LookAt(center+Vector3.up*.2f,row==0?Vector3.forward:Vector3.up);
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.055f,.07f,.1f);
                    preview.ambientColor=new Color(.2f,.22f,.27f);preview.lights[0].intensity=.55f;preview.lights[0].transform.rotation=Quaternion.Euler(55,-35,0);preview.lights[1].intensity=.12f;
                    preview.BeginStaticPreview(new Rect(0,0,size,size));preview.Render(true);preview.Render(true);var capture=preview.EndStaticPreview();sheet.SetPixels(column*size,row*size,size,size,capture.GetPixels());UnityEngine.Object.DestroyImmediate(capture);
                }
                finally{preview.Cleanup();if(ground)UnityEngine.Object.DestroyImmediate(ground);}
            }
            sheet.Apply();Directory.CreateDirectory("Docs/ConceptMaps/Previews");File.WriteAllBytes("Docs/ConceptMaps/Previews/building-damage-and-lights.png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log("Damage preview: intact / hit / corner removed / breach. Upper row oblique, lower row top-down.");
        }
    }
}
