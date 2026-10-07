using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class TvReplayRigBuilder
    {
        public const string Path="Assets/BattleCities/Resources/TvReplayRig.prefab";
        [MenuItem("Battle Cities/Rebuild TV replay renderer")]
        public static void Build()
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/BattleCities/Scenes/BattleCity.unity");
            GameObject root=null;
            try
            {
                BattleGame source=null;
                foreach(var go in scene.GetRootGameObjects()){source=go.GetComponentInChildren<BattleGame>(true);if(source)break;}
                if(!source)throw new InvalidOperationException("BattleCity renderer was not found.");
                root=new GameObject("TV replay renderer");root.SetActive(false);
                // Copy only the renderer's authored assets/settings, never scene services or input UI.
                var copy=root.AddComponent<BattleGame>();EditorUtility.CopySerialized(source,copy);
                PrefabUtility.SaveAsPrefabAsset(root,Path);AssetDatabase.SaveAssets();
                Debug.Log("TV replay rig rebuilt from authored BattleCity renderer.");
            }
            finally{if(root)UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}