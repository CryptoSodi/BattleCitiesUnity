using System;
using System.Linq;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities.Editor
{
    public static class LoginPsg1BattlefieldLayout
    {
        const string ArtPath = "Assets/BattleCities/UI/Art/login/psg1-battlefield.png";

        [MenuItem("Battle Cities/Login/Apply PSG1 battlefield composition")]
        public static void Apply()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().name != "Login")
                throw new InvalidOperationException("Open Login outside Play Mode.");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            if (!sprite) throw new InvalidOperationException("Import the approved PSG1 battlefield background first.");
            var theme = AssetDatabase.LoadAssetAtPath<MenuTheme>("Assets/BattleCities/UI/Settings/ArcadeMenuTheme.asset");
            var layout = UnityEngine.Object.FindFirstObjectByType<LoginLayout>();
            var canvas = layout.GetComponent<Canvas>();
            var root = (RectTransform)canvas.transform.Find("PSG1 Login Layout");
            var copy = canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true).ToDictionary(t => t, t => t.text);
            var buttons = canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            var buttonSprites = buttons.ToDictionary(b => b, b => b.image.sprite);
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "PSG1 login battlefield composition");

            var background = Picture(canvas.transform, "PSG1 Battlefield", sprite);
            background.type = UnityEngine.UI.Image.Type.Simple;
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            background.transform.SetAsFirstSibling();
            var cover = background.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            if (!cover) cover = background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            cover.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = sprite.rect.width / sprite.rect.height;

            // Only decorative plates are added. The separate PSG1 controls remain authored objects.
            var title = Picture(root, "PSG1 Heading Plate", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BattleCities/UI/Art/shared/panels/prebattle-title.png"));
            title.type = UnityEngine.UI.Image.Type.Simple;
            title.preserveAspect = true;
            title.transform.SetSiblingIndex(1);
            Put(root, "Main Menu Blue Container", 0, -195, 1100, 450);
            var interior = root.Find("Main Menu Blue Container/Grey White TV Interior").GetComponent<UnityEngine.UI.Image>();
            interior.pixelsPerUnitMultiplier = 3;
            root.Find("Main Menu Blue Container/Main Menu TV Frame").GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier = 3;
            interior.rectTransform.offsetMin = new Vector2(10, 10);
            interior.rectTransform.offsetMax = new Vector2(-10, -10);
            Put(root, "Battle Cities Logo", -350, 315, 400, 400);
            Put(root, "PSG1 Heading Plate", 0, -40, 1030, 88);
            var heading = PutText(root, "Heading", 0, -40, 920, 76, 50);
            ArcadeTextStyles.ApplyGold(heading, theme.HeadingFont);
            PutText(root, "Description", 0, -100, 1000, 32, 28);
            PutArt(root, "Connect Phantom", 0, -184, 640);
            PutArt(root, "Continue as Guest", 0, -310, 640);
            PutText(root, "Login Status", 0, -382, 1000, 28, 24);
            foreach (var child in root.Cast<Transform>())
                if (child.name == "Divider" || child.name == "Or" || child.name == "Footer Divider" ||
                    child.name == "Golden Eagle" || child.name == "Choose Entry" || child.name == "Solana dApp Store")
                    child.gameObject.SetActive(false);

            // The original partner art contains navy lettering, so retain readable cream backing.
            var solanaPlate = Picture(root, "PSG1 Solana Plate", theme.Rounded);
            var magicPlate = Picture(root, "PSG1 MagicBlock Plate", theme.Rounded);
            foreach (var plate in new[] { solanaPlate, magicPlate })
            {
                plate.type = UnityEngine.UI.Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = plate.sprite.border.x / (6f * Mathf.Max(.01f, plate.pixelsPerUnit));
                plate.transform.SetSiblingIndex(2);
            }
            Put(root, "PSG1 Solana Plate", -156, -450, 268, 46);
            Put(root, "PSG1 MagicBlock Plate", 156, -450, 268, 46);
            Put(root, "Built on Solana", -156, -450, 300, 62);
            Put(root, "Powered by MagicBlock", 156, -450, 300, 62);
            Put(root, "Partner Divider", 0, -450, 2, 38);

            Put(root, "PSG1 Controller Legend", 0, -508, 1240, 64);
            var legend = root.Find("PSG1 Controller Legend");
            Put(legend, "Legend Interior", 0, 0, 1220, 46);
            string[] labels = { "NAVIGATE", "SELECT", "BACK" };
            float[] columns = { -398, 0, 398 };
            for (int i = 0; i < labels.Length; i++)
            {
                Put(legend, labels[i] + " Icon", columns[i] - 89, 0, 46, 46);
                PutText(legend, labels[i], columns[i] + 37, 0, 184, 50, 30, TextAnchor.MiddleLeft);
            }
            var actions = new[] { root.Find("Connect Phantom").GetComponent<UnityEngine.UI.Button>(),
                root.Find("Continue as Guest").GetComponent<UnityEngine.UI.Button>() };
            for (int i = 0; i < actions.Length; i++)
            {
                var navigation = actions[i].navigation;
                navigation.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                navigation.selectOnUp = navigation.selectOnDown = actions[1 - i];
                navigation.selectOnLeft = navigation.selectOnRight = null;
                actions[i].navigation = navigation;
            }
            layout.ConfigurePsg1Battlefield(background.rectTransform);
            Canvas.ForceUpdateCanvases();
            if (copy.Any(p => p.Key.text != p.Value) ||
                buttons.Length != canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length ||
                buttonSprites.Any(p => p.Key.image.sprite != p.Value))
                throw new InvalidOperationException("Keep all existing login copy, six controls and button sprites.");
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
        static void Put(Transform root, string name, float x, float y, float w, float h)
        {
            var rect = (RectTransform)root.Find(name);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            rect.gameObject.SetActive(true);
        }
        static void PutArt(Transform root, string name, float x, float y, float width)
        {
            var image = root.Find(name).GetComponent<UnityEngine.UI.Image>();
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = true;
            Put(root, name, x, y, width, width * image.sprite.rect.height / image.sprite.rect.width);
        }
        static UnityEngine.UI.Text PutText(Transform root, string name, float x, float y, float w, float h,
            int font, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Put(root, name, x, y, w, h);
            var text = root.Find(name).GetComponent<UnityEngine.UI.Text>();
            text.alignment = alignment;
            text.fontSize = text.resizeTextMaxSize = font;
            text.resizeTextMinSize = Mathf.RoundToInt(font * .78f);
            text.resizeTextForBestFit = true;
            return text;
        }
    }
}