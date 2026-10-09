using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEngine;
using BattleCities.Multiplayer;

namespace BattleCities
{
    public static class MultiplayerChunkSetup
    {
        public static void Configure()
        {
            const string path="Assets/BattleCities/Resources/Multiplayer/BattleNetworkChunk.prefab";
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(path))
            {
                var root=new GameObject("BattleNetworkChunk");
                try{root.AddComponent<NetworkObject>();root.AddComponent<BattleNetworkChunk>();new NetworkObjectBakerEditTime().Bake(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{Object.DestroyImmediate(root);}
            }
            NetworkProjectConfigUtilities.RebuildPrefabTable();AssetDatabase.SaveAssets();
        }
    }
}
