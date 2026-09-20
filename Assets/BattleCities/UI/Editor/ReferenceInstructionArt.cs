using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class ReferenceInstructionArt
    {
        private const string ArtRoot = MainMenuBuilder.Root + "Art/reference-style-v2/shared/icons/";

        public static void ApplyTo(RectTransform controls, RectTransform howItWorks)
        {
            Sprite dpad = LoadSprite(ArtRoot + "controls/dpad.png");
            Sprite buttonA = LoadSprite(ArtRoot + "controls/button-a.png");
            Sprite buttonB = LoadSprite(ArtRoot + "controls/button-b.png");
            Sprite[] steps =
            {
                LoadSprite(ArtRoot + "steps/step-1.png"),
                LoadSprite(ArtRoot + "steps/step-2.png"),
                LoadSprite(ArtRoot + "steps/step-3.png")
            };

            ApplyControls(controls, dpad, buttonA, buttonB);
            ApplySteps(howItWorks, steps);
        }

        private static Sprite LoadSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing icon texture: " + path);

            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite)
                throw new InvalidOperationException("Could not import icon sprite: " + path);
            return sprite;
        }

        private static void ApplyControls(RectTransform controls, Sprite dpad, Sprite buttonA, Sprite buttonB)
        {
            var oldHints = controls.Find("Hints");
            Font hintFont = oldHints ? oldHints.GetComponent<Text>()?.font : null;
            if (oldHints)
                oldHints.gameObject.SetActive(false);

            CreateControlHint(controls, "D-Pad Hint", dpad, "D-PAD  NAVIGATE", hintFont, .055f, .36f, .18f);
            CreateControlHint(controls, "A Hint", buttonA, "SELECT", hintFont, .46f, .20f, .28f);
            CreateControlHint(controls, "B Hint", buttonB, "BACK", hintFont, .72f, .18f, .30f);
        }

        private static void CreateControlHint(
            RectTransform parent, string name, Sprite sprite, string caption, Font font,
            float x, float width, float iconWidth)
        {
            RectTransform group = FindOrCreateRect(parent, name);
            MainMenuBuilder.Box(group, x, 0f, width, 1f);

            var icon = FindOrCreateImage(group, "Icon");
            icon.sprite = sprite;
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            MainMenuBuilder.Box(icon.rectTransform, 0f, .04f, iconWidth, .92f);

            var label = FindOrCreateText(group, "Label");
            if (font)
                label.font = font;
            label.text = caption;
            label.fontSize = 25;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = 25;
            label.raycastTarget = false;
            MainMenuBuilder.Box(label.rectTransform, iconWidth + .035f, 0f, 1f - iconWidth - .035f, 1f);
        }

        private static void ApplySteps(RectTransform howItWorks, Sprite[] badges)
        {
            var paper = howItWorks.Find("Paper");
            if (!paper)
                throw new InvalidOperationException("How It Works/Paper was not found.");

            string[] titles = { "PLAY", "REACH TOP 10", "EARN BATC" };
            for (int i = 0; i < 3; i++)
            {
                var step = paper.Find("Step " + (i + 1)) as RectTransform;
                if (!step)
                    throw new InvalidOperationException("Missing How It Works step " + (i + 1));

                var icon = step.Find("Icon") as RectTransform;
                var heading = step.Find("Title")?.GetComponent<Text>();
                var body = step.Find("Body") as RectTransform;
                if (!icon || !heading || !body)
                    throw new InvalidOperationException("Incomplete How It Works step " + (i + 1));

                MainMenuBuilder.Box(icon, 0f, .08f, .22f, .78f);

                var badge = FindOrCreateImage(step, "Step Badge");
                badge.sprite = badges[i];
                badge.type = Image.Type.Simple;
                badge.preserveAspect = true;
                badge.raycastTarget = false;
                MainMenuBuilder.Box(badge.rectTransform, .225f, .12f, .13f, .58f);

                heading.text = titles[i];
                heading.alignment = TextAnchor.MiddleLeft;
                MainMenuBuilder.Box(heading.rectTransform, .37f, 0f, .62f, .34f);
                MainMenuBuilder.Box(body, .37f, .36f, .62f, .61f);
            }
        }

        private static RectTransform FindOrCreateRect(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing)
                return existing;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image FindOrCreateImage(Transform parent, string name)
        {
            var rect = FindOrCreateRect(parent, name);
            var image = rect.GetComponent<Image>();
            return image ? image : rect.gameObject.AddComponent<Image>();
        }

        private static Text FindOrCreateText(Transform parent, string name)
        {
            var rect = FindOrCreateRect(parent, name);
            var text = rect.GetComponent<Text>();
            if (text)
                return text;
            text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }
    }
}
