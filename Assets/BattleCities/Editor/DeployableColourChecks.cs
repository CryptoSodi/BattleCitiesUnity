using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleCities.Core;
using BattleCities.Multiplayer;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BattleCities.Editor
{
    public static class DeployableColourChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static bool Same(Color a,Color b)=>Mathf.Abs(a.r-b.r)<.0001f&&Mathf.Abs(a.g-b.g)<.0001f&&Mathf.Abs(a.b-b.b)<.0001f;
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);int checks=0;
            void Check(bool ok,string label){checks++;if(!ok)throw new Exception("DEPLOYABLE COLOUR: "+label);}
            var carrier=new GameObject("Deployable colour checks");carrier.SetActive(false);
            var game=carrier.AddComponent<BattleGame>();typeof(BattleGame).GetField("authoringPreview",Private).SetValue(game,true);
            var owned=(List<Material>)typeof(BattleGame).GetField("ownedMaterials",Private).GetValue(game);
            var tint=typeof(BattleGame).GetMethod("TintDeployable",Private);var ownerColour=typeof(BattleGame).GetMethod("PlayerColor",Private);
            var rig=Resources.Load<GameObject>("TvReplayRig").GetComponent<BattleGame>();
            var sources=new[]{rig.MineModel,rig.DroneModel,rig.GroundTurretPrefab,RemoteGameArt.Load<GameObject>("Deployables/LandAttackDrone")};
            var originals=new Dictionary<Material,Color>();string[] expectedHex={"35C759","F5B82E","168BFF","A855F7"};
            try
            {
                foreach(var mode in new[]{BattleMode.Offline,BattleMode.Coop,BattleMode.Versus,BattleMode.TeamBattle,BattleMode.CaptureFlag})
                {
                    var simulation=new BattleSimulation(new MapData{rivalBase=new BaseData{x=320,y=0}});
                    if(mode!=BattleMode.Offline)simulation.ConfigureMultiplayer(mode);
                    typeof(BattleGame).GetProperty("Simulation").SetValue(game,simulation);
                    for(int slot=-1;slot<4;slot++)
                    {
                        ColorUtility.TryParseHtmlString("#"+expectedHex[Math.Max(0,slot)],out var expected);
                        Check(Same((Color)ownerColour.Invoke(game,new object[]{slot}),expected),"owner palette and patrol boundary "+mode+" slot "+slot);
                        Check(NetMineState.From(new MineState{OwnerSlot=slot}).ToState().OwnerSlot==slot&&
                            NetDroneState.From(new DroneState{OwnerSlot=slot}).ToState().OwnerSlot==slot&&
                            NetTurretState.From(new TurretState{OwnerSlot=slot}).ToState().OwnerSlot==slot&&
                            NetLandDroneState.From(new LandDroneState{OwnerSlot=slot}).ToState().OwnerSlot==slot,"replicated special ownership");
                        foreach(var source in sources)
                        {
                            Check(source,"deployed special prefab present");var model=Object.Instantiate(source,carrier.transform);
                            try
                            {
                                var renderers=model.GetComponentsInChildren<Renderer>(true);
                                var before=renderers.Select(r=>r.sharedMaterials).ToArray();
                                foreach(var material in before.SelectMany(m=>m).Where(m=>m).Distinct())if(!originals.ContainsKey(material))originals[material]=material.color;
                                tint.Invoke(game,new object[]{model,slot});int painted=0;
                                for(int r=0;r<renderers.Length;r++)
                                {
                                    var after=renderers[r].sharedMaterials;Check(after.Length==before[r].Length,"material hierarchy preserved");
                                    for(int i=0;i<after.Length;i++)
                                    {
                                        var original=before[r][i];var material=after[i];Check(material&&material.shader==original.shader,"original shader retained");
                                        if(material!=original)
                                        {
                                            painted++;bool shade=original.name.EndsWith("Shadow",StringComparison.Ordinal)||original.name.EndsWith("Shade",StringComparison.Ordinal);
                                            Check(Same(material.color,shade?expected*.68f:expected),"paint matches owning tank "+source.name+" "+mode+" "+slot);
                                            Check(!new[]{"metal","steel","rubber","marking","insignia","lens","glow","flash","lamp","chevron"}.Any(word=>original.name.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0),"mechanics, markings and effects not recoloured");
                                        }
                                        foreach(string property in original.GetTexturePropertyNames())Check(material.GetTexture(property)==original.GetTexture(property),"authored textures retained");
                                    }
                                }
                                Check(painted>=2,"primary and secondary painted surfaces changed on "+source.name);
                            }
                            finally{Object.DestroyImmediate(model);}
                        }
                    }
                }
                foreach(var original in originals)Check(Same(original.Key.color,original.Value),"shared source material untouched");
                Debug.Log("DEPLOYABLE COLOUR PASS: "+checks+" checks across mines, patrol drones, turrets and tank drones; five modes and all owner identities.");
            }
            finally{foreach(var material in owned)if(material)Object.DestroyImmediate(material);owned.Clear();Object.DestroyImmediate(carrier);}
        }
    }
}
