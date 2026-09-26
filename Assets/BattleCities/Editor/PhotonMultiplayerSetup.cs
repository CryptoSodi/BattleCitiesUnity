using BattleCities.Multiplayer;
using Fusion;
using Fusion.Editor;
using Fusion.Photon.Realtime;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BattleCities
{
    public static class PhotonMultiplayerSetup
    {
        public static string Diagnostics()
        {
            var s=BattleSession.Instance;if(!s)return "No session";
            return "Online="+s.Online+" Host="+s.IsHost+" Status="+s.Status+" Room="+s.RoomCode+
                (s.Match?" Players="+s.Match.PlayerCount+" Tick="+s.Match.StateTick+" Local="+s.Match.LocalSlot:"");
        }
        public static void BuildWindowsSmoke(string output=null)
        {
            if(string.IsNullOrEmpty(output))output=System.IO.Path.GetFullPath("Builds/Multiplayer/BattleCities.exe");
            EnsureRuntimeShaders();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{"Assets/BattleCities/Scenes/Login.unity","Assets/BattleCities/Scenes/MainMenu.unity","Assets/BattleCities/Scenes/BattleCity.unity"},
                locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(output),"build-result.txt"),report.summary.result+" errors="+report.summary.totalErrors);
            Debug.Log("PHOTON BUILD "+report.summary.result+" errors="+report.summary.totalErrors);
        }
        private static void EnsureRuntimeShaders()
        {
            // Runtime-created materials need asset references so player builds retain their shaders.
            const string folder="Assets/BattleCities/Resources/RuntimeShaders";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/BattleCities/Resources","RuntimeShaders");
            foreach(var shaderName in new[]{"Universal Render Pipeline/Particles/Unlit","Universal Render Pipeline/Unlit"})
            foreach(bool additive in new[]{false,true})
            {
                string path=folder+"/"+shaderName.Replace("/","_")+(additive?"_Additive":"_Alpha")+".mat";
                if(AssetDatabase.LoadAssetAtPath<Material>(path))continue;
                var material=new Material(Shader.Find(shaderName));
                material.SetFloat("_Surface",1);material.SetFloat("_Blend",additive?2:0);
                material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",additive?1:10);
                material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;
                AssetDatabase.CreateAsset(material,path);
            }
            const string terrainFolder=folder+"/Terrain";
            if(!AssetDatabase.IsValidFolder(terrainFolder))AssetDatabase.CreateFolder(folder,"Terrain");
            foreach(var modelPath in new[]{"Assets/BattleCities/Art/Terrain/Brick/brick-block.glb",
                "Assets/BattleCities/Art/Terrain/Steel/steel-brick.glb","Assets/BattleCities/Art/Terrain/Bush/bush.glb"})
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                foreach(var source in model.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r=>r.sharedMaterials).Distinct())
                {
                    string path=terrainFolder+"/"+source.name+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!material){material=new Material(source);AssetDatabase.CreateAsset(material,path);}
                    else EditorUtility.CopySerialized(source,material);
                    material.name=source.name;material.enableInstancing=true;EditorUtility.SetDirty(material);
                }
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Battle Cities/Multiplayer/Configure Fusion")]
        public static void Configure()
        {
            EnsureRuntimeShaders();
            var settings=PhotonAppSettings.Global;
            settings.AppSettings.AppIdFusion=BattleSession.AppId;
            settings.AppSettings.AppVersion=BattleSession.ProtocolVersion;
            EditorUtility.SetDirty(settings);
            var config=NetworkProjectConfig.Global;
            config.Simulation.PlayerCount=4;
            config.Simulation.TickRateSelection=new TickRate.Selection{Client=60,ClientSendInterval=1,ServerTickInterval=1,ServerSendInterval=2};
            NetworkProjectConfigUtilities.SaveGlobalConfig(config);
            if(!AssetDatabase.IsValidFolder("Assets/BattleCities/Resources/Multiplayer"))
                AssetDatabase.CreateFolder("Assets/BattleCities/Resources","Multiplayer");
            const string path="Assets/BattleCities/Resources/Multiplayer/BattleNetworkMatch.prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!existing)
            {
                var root=new GameObject("BattleNetworkMatch");
                try
                {
                    root.AddComponent<NetworkObject>();root.AddComponent<BattleNetworkMatch>();
                    new NetworkObjectBakerEditTime().Bake(root);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            Debug.Log("PHOTON SETUP PASS: Fusion 2 app configured, 60 Hz simulation / 30 Hz snapshots, four players, network prefab registered.");
        }
    }
}
