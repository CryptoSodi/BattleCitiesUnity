using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class RemoteSceneCaptureChecks
    {
        public static void Run()
        {
            const string staging = "Assets/BattleCities/RemoteArtBuildStaging";
            if (AssetDatabase.IsValidFolder(staging) || File.Exists("Builds/RemoteArtRecovery.json"))
                throw new Exception("An artwork staging operation is already present; leave it intact.");
            int checks = 0;
            bool created = false;
            try
            {
                AssetDatabase.CreateFolder("Assets/BattleCities", "RemoteArtBuildStaging");
                created = true;
                var resourceRoots = RemoteGameArtBuild.ImageResourceRoots();
                foreach (var key in new[] { "BattleVisualAssets.asset", "RankingCardArt.asset", "BattleGuideArt.asset", "Psg1BattleGuideArt.asset", "HudPlayer.png", "HudEnemy.png" })
                    if (!resourceRoots.Any(path => path.EndsWith("/" + key, StringComparison.Ordinal)))
                        throw new Exception("Image-bearing Resources root was missed: " + key);
                if (resourceRoots.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                    throw new Exception("Image-bearing Resources roots have ambiguous filenames.");
                foreach (var setting in EditorBuildSettings.scenes.Where(s => s.enabled))
                {
                    var originalBytes = File.ReadAllBytes(setting.path);
                    var source = EditorSceneManager.OpenPreviewScene(setting.path);
                    int roots;
                    int components;
                    try
                    {
                        var all = source.GetRootGameObjects();
                        roots = all.Length;
                        components = all.Sum(r => r.GetComponentsInChildren<Component>(true).Length);
                    }
                    finally { EditorSceneManager.ClosePreviewScene(source); }
                    var destination = staging + "/" + Path.GetFileNameWithoutExtension(setting.path) + "-check.prefab";
                    RemoteSceneCapture.Capture(setting.path, destination);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(destination);
                    if (!prefab || prefab.transform.childCount != roots ||
                        prefab.GetComponentsInChildren<Component>(true).Length != components + 1)
                        throw new Exception("Scene hierarchy changed in captured prefab: " + setting.path);
                    var original = EditorSceneManager.OpenPreviewScene(setting.path);
                    try
                    {
                        VerifySceneReferences(original.GetRootGameObjects(), prefab, setting.path);
                        RemoteSceneBuild.ReplaceScene(original);
                        var replacement = original.GetRootGameObjects();
                        if (replacement.Length != 1 || !replacement[0].GetComponent<RemoteSceneLoader>())
                            throw new Exception("Scene does not contain exactly one remote loader: " + setting.path);
                        var loader = new SerializedObject(replacement[0].GetComponent<RemoteSceneLoader>());
                        if (loader.FindProperty("sceneName").stringValue != Path.GetFileNameWithoutExtension(setting.path))
                            throw new Exception("Remote loader points to the wrong scene: " + setting.path);
                        bool login = Path.GetFileNameWithoutExtension(setting.path) == "Login";
                        if (login && (!loader.FindProperty("startupLogo").objectReferenceValue || !loader.FindProperty("startupFont").objectReferenceValue))
                            throw new Exception("Remote loader is missing its startup logo or font.");
                        if (!login && (loader.FindProperty("startupLogo").objectReferenceValue || loader.FindProperty("startupFont").objectReferenceValue))
                            throw new Exception("Only login may depend on local startup artwork.");
                    }
                    finally { EditorSceneManager.ClosePreviewScene(original); }
                    if (!originalBytes.SequenceEqual(File.ReadAllBytes(setting.path)))
                        throw new Exception("Authored scene changed during capture: " + setting.path);
                    checks++;
                    AssetDatabase.DeleteAsset(destination);
                }
                Directory.CreateDirectory("Builds/RemoteArtChecks");
                File.WriteAllText("Builds/RemoteArtChecks/scene-capture.txt", "PASS " + checks);
                Debug.Log("REMOTE SCENE CAPTURE CHECKS PASSED: " + checks);
            }
            finally
            {
                if (created && AssetDatabase.IsValidFolder(staging))
                {
                    foreach (var setting in EditorBuildSettings.scenes.Where(s => s.enabled))
                    {
                        var path = staging + "/" + Path.GetFileNameWithoutExtension(setting.path) + "-check.prefab";
                        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                    }
                    if (!Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).Any(path => !path.EndsWith(".meta", StringComparison.Ordinal)))
                        AssetDatabase.DeleteAsset(staging);
                    else Debug.LogError("Unexpected files remain in remote scene check staging; they were preserved.");
                }
            }
        }

        static void VerifySceneReferences(GameObject[] roots, GameObject prefab, string scene)
        {
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            void Pair(Transform source, Transform target)
            {
                if (source.childCount != target.childCount) throw new Exception("Child hierarchy changed: " + scene);
                map[source.gameObject] = target.gameObject;
                var left = source.GetComponents<Component>(); var right = target.GetComponents<Component>();
                if (left.Length != right.Length) throw new Exception("Component count changed: " + scene);
                for (int i = 0; i < left.Length; i++)
                {
                    if (!left[i] || !right[i]) continue;
                    if (left[i].GetType() != right[i].GetType()) throw new Exception("Component type changed: " + scene);
                    map[left[i]] = right[i];
                }
                for (int i = 0; i < source.childCount; i++) Pair(source.GetChild(i), target.GetChild(i));
            }
            for (int i = 0; i < roots.Length; i++) Pair(roots[i].transform, prefab.transform.GetChild(i));
            foreach (var pair in map.Where(item => item.Key is Component && item.Value is Component))
            {
                var source = new SerializedObject(pair.Key); var target = new SerializedObject(pair.Value);
                var property = source.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || !property.objectReferenceValue ||
                        property.propertyPath == "m_CorrespondingSourceObject" || property.propertyPath == "m_PrefabInstance" ||
                        property.propertyPath == "m_PrefabAsset") continue;
                    var reference = property.objectReferenceValue;
                    if (!map.TryGetValue(reference, out var expected))
                    {
                        if (!EditorUtility.IsPersistent(reference)) continue;
                        expected = reference;
                    }
                    var result = target.FindProperty(property.propertyPath);
                    if (result == null || result.objectReferenceValue != expected)
                        throw new Exception("Scene reference changed: " + scene + " / " + pair.Key.name + " / " + property.propertyPath);
                }
            }
        }
    }
}
