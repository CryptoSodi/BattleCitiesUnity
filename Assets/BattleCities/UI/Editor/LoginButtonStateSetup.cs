using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class LoginButtonStateSetup
    {
        [MenuItem("Battle Cities/Login/Apply button states")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying || SceneManager.GetActiveScene().name!="Login")
                throw new System.InvalidOperationException("Open Login outside Play Mode.");
            int count=0;
            foreach(var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(b.gameObject.scene!=SceneManager.GetActiveScene() ||
                   (b.name!="Connect Phantom" && b.name!="Continue as Guest" && b.name!="Solana dApp Store")) continue;
                Undo.RecordObject(b,"Login button states"); b.transition=Selectable.Transition.None;
                var existing=b.transform.Find("Active Focus Ring");
                var ring=existing?existing.GetComponent<Image>():null;
                if(ring){Undo.RecordObject(ring,"Remove login focus outline");ring.enabled=false;}
                var state=b.GetComponent<LoginButtonState>();if(!state)state=Undo.AddComponent<LoginButtonState>(b.gameObject);
                state.Configure(ring);EditorUtility.SetDirty(state);count++;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Configured active/inactive/pressed/disabled states on "+count+" login buttons.");
        }
    }
}
