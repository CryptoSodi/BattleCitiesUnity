using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities.Editor
{
    [InitializeOnLoad]
    public static class MainMenuSceneEditing
    {
        static MainMenuSceneEditing()
        {
            EditorSceneManager.sceneSaving+=BeforeSceneSave;
        }

        private static void BeforeSceneSave(Scene scene,string path)
        {
            if(EditorApplication.isPlaying)return;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var menu in root.GetComponentsInChildren<MainMenuScene>(true))
                {
                    menu.SaveAuthoredLayout();
                    EditorUtility.SetDirty(menu);
                }
        }
    }

    [CustomEditor(typeof(MainMenuScene))]
    public sealed class MainMenuSceneInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Scene Editing is enabled by default: move and resize children in the Scene view and edit their Text components. Save the scene to keep your changes. Changing Platform remembers separate Web, PSG1 and Android layouts. The older numeric layout settings only generate a platform's initial arrangement.",MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
