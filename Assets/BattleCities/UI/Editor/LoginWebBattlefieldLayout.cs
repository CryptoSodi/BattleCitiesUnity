using System;
using System.IO;
using System.Linq;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities.Editor
{
    public static class LoginWebBattlefieldLayout
    {
        const string ArtPath = "Assets/BattleCities/UI/Art/login/web-battlefield.png";

        [MenuItem("Battle Cities/Login/Apply web battlefield composition")]
        public static void Apply()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().name != "Login")
                throw new InvalidOperationException("Open Login outside Play Mode.");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            if (!sprite) throw new InvalidOperationException("Import the approved web battlefield background first.");
            var theme = AssetDatabase.LoadAssetAtPath<MenuTheme>("Assets/BattleCities/UI/Settings/ArcadeMenuTheme.asset");
            var layout = UnityEngine.Object.FindFirstObjectByType<LoginLayout>();
            var canvas = layout.GetComponent<Canvas>();
            var root = (RectTransform)canvas.transform.Find("Login Layout (edit child positions freely)");
            var texts = canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            var originalCopy = texts.Select(t => t.text).ToArray();
            var buttons = canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            var originalSprites = buttons.Select(b => b.image.sprite).ToArray();
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Web login battlefield composition");
            layout.Preview(MainMenuPlatform.Android);

            var background = Picture(canvas.transform, "Web Battlefield", sprite);
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            background.transform.SetAsFirstSibling();
            var cover = background.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            if (!cover) cover = background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            cover.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = sprite.rect.width / sprite.rect.height;

            var title = Picture(root, "Web Heading Plate", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BattleCities/UI/Art/shared/panels/prebattle-title.png"));
            title.type = UnityEngine.UI.Image.Type.Simple;
            title.preserveAspect = true;
            var dock = Picture(root, "Web Partner Dock", theme.BlueFrame);
            dock.type = UnityEngine.UI.Image.Type.Sliced;
            dock.pixelsPerUnitMultiplier = dock.sprite.border.x / (105f * .18f * Mathf.Max(.01f, dock.pixelsPerUnit));
            var dockBody = Picture(dock.transform, "Navy Interior", theme.DarkPanel);
            dockBody.type = UnityEngine.UI.Image.Type.Sliced;
            dockBody.rectTransform.anchorMin = Vector2.zero;
            dockBody.rectTransform.anchorMax = Vector2.one;
            dockBody.rectTransform.offsetMin = new Vector2(8, 8);
            dockBody.rectTransform.offsetMax = new Vector2(-8, -8);
            var solanaPlate = Picture(root, "Web Solana Plate", theme.Rounded);
            var magicPlate = Picture(root, "Web MagicBlock Plate", theme.Rounded);
            foreach (var plate in new[] { solanaPlate, magicPlate })
            {
                plate.type = UnityEngine.UI.Image.Type.Sliced;
                plate.color = new Color32(243, 241, 233, 255);
                plate.pixelsPerUnitMultiplier = plate.sprite.border.x / (6f * Mathf.Max(.01f, plate.pixelsPerUnit));
            }
            // Decorative plates sit behind the original text and controls.
            dock.transform.SetSiblingIndex(1);
            title.transform.SetSiblingIndex(2);
            solanaPlate.transform.SetSiblingIndex(3);
            magicPlate.transform.SetSiblingIndex(4);
            var heading = root.Find("Heading").GetComponent<UnityEngine.UI.Text>();
            var gold = heading.GetComponent<ArcadeGoldText>();
            if (!gold) gold = heading.gameObject.AddComponent<ArcadeGoldText>();
            gold.enabled = false;

            var web = root.Cast<Transform>().Select(t => LoginElementPlacement.Capture((RectTransform)t)).ToArray();
            Set(web, "Main Menu Blue Container", 462, 0, 550, 482);
            Set(web, "Battle Cities Logo", -548, 249, 405, 405);
            Set(web, "Web Heading Plate", 462, 179, 504, 70);
            Set(web, "Heading", 462, 179, 404, 55, 38);
            Set(web, "Description", 462, 104, 476, 64, 24);
            SetArt(web, "Connect Phantom", 462, 14, 480);
            SetArt(web, "Continue as Guest", 462, -96, 480);
            Set(web, "Or", 462, -44, 42, 18, 15);
            Set(web, "Login Status", 462, -163, 474, 54, 21);
            Set(web, "Web Partner Dock", 341, -397, 910, 105);
            SetArt(web, "Solana dApp Store", 88, -397, 300);
            Set(web, "Web Solana Plate", 397, -397, 244, 77);
            Set(web, "Web MagicBlock Plate", 672, -397, 244, 77);
            Set(web, "Built on Solana", 397, -397, 222, 66);
            Set(web, "Powered by MagicBlock", 672, -397, 222, 66);
            Set(web, "Partner Divider", 526, -397, 2, 56);
            Set(web, "Store Divider", 258, -397, 2, 56);
            Hide(web, "Column Divider", "Footer Separator", "Golden Eagle", "Choose Entry");
            var rules = web.Select((p, i) => new { p, i }).Where(p => p.p.target.name == "Divider").ToArray();
            Box(ref web[rules[0].i], 337, -44, 188, 1);
            Box(ref web[rules[1].i], 587, -44, 188, 1);
            web[rules[2].i].visible = web[rules[3].i].visible = false;
            layout.ConfigureWeb(web, background.rectTransform,
                canvas.transform.Find("Blurred Battlefield") as RectTransform,
                canvas.transform.Find("Background Soft Shade") as RectTransform, heading);
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

        public static void ImportBackground()
        {
            AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        static UnityEngine.UI.Image Picture(Transform parent, string name, Sprite sprite)
        {
            var found = parent.Find(name);
            var image = found ? found.GetComponent<UnityEngine.UI.Image>() : null;
            if (!image)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(parent, false);
                image = go.GetComponent<UnityEngine.UI.Image>();
            }
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }
        static void Box(ref LoginElementPlacement p, float x, float y, float w, float h)
        { p.position = new Vector2(x, y); p.size = new Vector2(w, h); p.visible = true; }
        static void Set(LoginElementPlacement[] list, string name, float x, float y, float w, float h, int font = 0)
        {
            int i = Array.FindIndex(list, p => p.target.name == name);
            if (i < 0) throw new InvalidOperationException("Missing " + name);
            Box(ref list[i], x, y, w, h);
            if (font > 0)
            {
                list[i].fontSize = list[i].maxFontSize = font;
                list[i].minFontSize = Mathf.RoundToInt(font * .78f);
                list[i].alignment = TextAnchor.MiddleCenter;
                list[i].bestFit = true;
            }
        }
        static void SetArt(LoginElementPlacement[] list, string name, float x, float y, float width)
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
