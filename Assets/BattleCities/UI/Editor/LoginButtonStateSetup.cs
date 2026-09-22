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
            var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>("Assets/BattleCities/UI/Settings/ArcadeMenuTheme.asset");
            int count=0;
            foreach(var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(b.gameObject.scene!=SceneManager.GetActiveScene() ||
                   (b.name!="Connect Phantom" && b.name!="Continue as Guest" && b.name!="Solana dApp Store")) continue;
                Undo.RecordObject(b,"Login button states"); b.transition=Selectable.Transition.None;
                var existing=b.transform.Find("Active Focus Ring");
                Image ring;
                if(existing) ring=existing.GetComponent<Image>();
                else
                {
                    var go=new GameObject("Active Focus Ring",typeof(RectTransform),typeof(Image));
                    Undo.RegisterCreatedObjectUndo(go,"Login focus ring");go.transform.SetParent(b.transform,false);ring=go.GetComponent<Image>();
                }
                ring.sprite=theme.FocusRing;ring.type=Image.Type.Sliced;ring.fillCenter=false;ring.raycastTarget=false;
                ring.color=new Color32(255,221,65,255);
                var rect=ring.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(-6,-6);rect.offsetMax=new Vector2(6,6);
                var state=b.GetComponent<LoginButtonState>();if(!state)state=Undo.AddComponent<LoginButtonState>(b.gameObject);
                state.Configure(ring);EditorUtility.SetDirty(state);count++;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Configured active/inactive/pressed/disabled states on "+count+" login buttons.");
        }
    }
}
