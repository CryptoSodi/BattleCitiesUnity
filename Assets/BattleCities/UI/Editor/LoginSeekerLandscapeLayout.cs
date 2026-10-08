using System;
using System.Linq;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities.Editor
{
    public static class LoginSeekerLandscapeLayout
    {
        [MenuItem("Battle Cities/Login/Apply Seeker horizontal battlefield composition")]
        public static void Apply()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().name != "Login")
                throw new InvalidOperationException("Open Login outside Play Mode.");
            var background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BattleCities/UI/Art/login/web-battlefield.png");
            if (!background) throw new InvalidOperationException("Import the existing web battlefield background first.");
            var layout = UnityEngine.Object.FindFirstObjectByType<LoginLayout>();
            var canvas = layout.GetComponent<Canvas>();
            var root = (RectTransform)canvas.transform.Find("Login Layout (edit child positions freely)");
            if (!canvas.transform.Find("Web Battlefield") || !root.Find("Web Heading Plate"))
                throw new InvalidOperationException("Apply the web battlefield composition first to reuse its artwork.");
            var texts = canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            var originalCopy = texts.Select(t => t.text).ToArray();
            var buttons = canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            var originalSprites = buttons.Select(b => b.image.sprite).ToArray();
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Seeker horizontal login composition");
            var sharedBackdrop = canvas.transform.Find("Web Battlefield").GetComponent<UnityEngine.UI.Image>();
            sharedBackdrop.sprite = background;
            sharedBackdrop.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio = background.rect.width / background.rect.height;
            layout.Preview(MainMenuPlatform.Android);
            var landscape = root.Cast<Transform>().Select(t => LoginElementPlacement.Capture((RectTransform)t)).ToArray();
            Set(landscape, "Main Menu Blue Container", 420, -14, 700, 540);
            Set(landscape, "Battle Cities Logo", -570, 201, 410, 410);
            Set(landscape, "Web Heading Plate", 420, 175, 646, 74);
            Set(landscape, "Heading", 420, 175, 574, 66, 52);
            Set(landscape, "Description", 420, 105, 634, 60, 28);
            Art(landscape, "Connect Phantom", 420, 8, 630);
            Art(landscape, "Continue as Guest", 420, -119, 630);
            Set(landscape, "Login Status", 420, -207, 634, 54, 24);
            Set(landscape, "Web Partner Dock", 607, -360, 560, 90);
            Set(landscape, "Web Solana Plate", 465, -360, 236, 66);
            Set(landscape, "Web MagicBlock Plate", 749, -360, 236, 66);
            Set(landscape, "Built on Solana", 465, -360, 212, 62);
            Set(landscape, "Powered by MagicBlock", 749, -360, 212, 62);
            Set(landscape, "Partner Divider", 607, -360, 2, 50);
            Hide(landscape, "Solana dApp Store", "Or", "Golden Eagle", "Choose Entry",
                "Column Divider", "Footer Separator", "Store Divider", "Divider");
            layout.ConfigureSeekerLandscape(landscape);
            layout.Preview(MainMenuPlatform.Auto);
            Canvas.ForceUpdateCanvases();
            if (!texts.Select(t => t.text).SequenceEqual(originalCopy) ||
                buttons.Length != canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length ||
                buttons.Where((b, i) => b.image.sprite != originalSprites[i]).Any())
                throw new InvalidOperationException("Keep the original login text, six controls and button sprites.");
            LoginGlassLayout.ApplyToLayout(layout);
            EditorUtility.SetDirty(layout);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        static void Set(LoginElementPlacement[] list, string name, float x, float y, float w, float h, int font = 0)
        {
            int index = Array.FindIndex(list, p => p.target.name == name);
            if (index < 0) throw new InvalidOperationException("Missing " + name);
            list[index].position = new Vector2(x, y);
            list[index].size = new Vector2(w, h);
            list[index].visible = true;
            if (font > 0)
            {
                list[index].fontSize = list[index].maxFontSize = font;
                list[index].minFontSize = Mathf.RoundToInt(font * .78f);
                list[index].alignment = TextAnchor.MiddleCenter;
                list[index].bestFit = true;
            }
        }
        static void Art(LoginElementPlacement[] list, string name, float x, float y, float width)
        {
            var sprite = list.First(p => p.target.name == name).target.GetComponent<UnityEngine.UI.Image>().sprite;
            Set(list, name, x, y, width, width * sprite.rect.height / sprite.rect.width);
        }
        static void Hide(LoginElementPlacement[] list, params string[] names)
        {
            for (int i = 0; i < list.Length; i++) if (names.Contains(list[i].target.name)) list[i].visible = false;
        }
    }
}
