using System;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace BattleCities.Editor
{
    // Editor-only capture of the actual BattleCity scene; never saves the scene or builds a player.
    [InitializeOnLoad]
    public static class PowerupGameplayPreviewLauncher
    {
        const string Pending="BattleCities.PowerupGameplayPreview";
        static PowerupGameplayPreviewLauncher(){EditorApplication.playModeStateChanged+=Changed;}
        public static void Run()
        {
            SessionState.SetBool(Pending,true);
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/BattleCity.unity",OpenSceneMode.Single);
            SetGameViewSize();
            EditorApplication.EnterPlaymode();
        }
        public static void Play()
        {
            SessionState.SetBool(Pending,false);
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/BattleCity.unity",OpenSceneMode.Single);
            SetGameViewSize();
            var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            view.maximized=true;view.Focus();
            EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Pending,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)new UnityEngine.GameObject("Power-up Play Mode capture").AddComponent<PowerupGameplayPreviewCapture>();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Pending,false);if(UnityEngine.Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(0);}
        }
        static void SetGameViewSize()
        {
            var assembly=typeof(EditorWindow).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(singleton,new object[]{0});
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");
            var kind=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,new[]{Enum.ToObject(kind,1),(object)1920,1080,"Power-up preview 1920x1080"},null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            view.GetType().GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).SetValue(view,count-1);
        }
    }
}
