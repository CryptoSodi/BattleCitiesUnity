using System;
using System.Linq;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCities.Editor
{
    public static class LoginGlassLayout
    {
        const string Root = "Assets/BattleCities/UI/";

        [MenuItem("Battle Cities/Login/Apply TV glass surfaces")]
        public static void Apply()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().name != "Login")
                throw new InvalidOperationException("Open Login outside Play Mode.");
            var layout = UnityEngine.Object.FindFirstObjectByType<LoginLayout>();
            var texts = layout.GetComponentsInChildren<UnityEngine.UI.Text>(true).ToDictionary(t => t, t => t.text);
            var buttons = layout.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            var sprites = buttons.ToDictionary(b => b, b => b.image.sprite);
            Undo.RegisterFullObjectHierarchyUndo(layout.gameObject, "Apply login TV glass");
            ApplyToLayout(layout);
            if (texts.Any(p => p.Key.name != "Login Status" && p.Key.text != p.Value) || buttons.Length != layout.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length ||
                sprites.Any(p => p.Key.image.sprite != p.Value))
                throw new InvalidOperationException("Login glass must preserve text, buttons and their artwork.");
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            EditorSceneManager.SaveScene(layout.gameObject.scene);
        }

        public static void ApplyToLayout(LoginLayout layout)
        {
            if (!layout) return;
            var theme = AssetDatabase.LoadAssetAtPath<MenuTheme>(Root + "Settings/ArcadeMenuTheme.asset");
            var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/reference-style-v2/shared/panels/navigation-container.png");
            var glassMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/shared/panels/TVBackdropBlur.mat");
            var sources = new[] { "PSG1 Battlefield", "Web Battlefield", "Blurred Battlefield" }
                .Select(n => layout.transform.Find(n)?.GetComponent<UnityEngine.UI.Image>()).Where(i => i).ToArray();
            if (!theme || !frameSprite || !glassMaterial || sources.Length == 0)
                throw new InvalidOperationException("The shared TV glass and login backdrops must be available.");
            var standard = layout.transform.Find("Login Layout (edit child positions freely)");
            var psg = layout.transform.Find("PSG1 Login Layout");
            foreach (var root in new[] { standard, psg })
            {
                if (!root) continue;
                var frame = root.Find("Main Menu Blue Container");
                if (frame)
                {
                    Surface(frame, frame.Find("Grey White TV Interior"), frameSprite, theme.Rounded, glassMaterial, sources, 1.2f);
                    var extraRim = frame.Find("Main Menu TV Frame")?.GetComponent<UnityEngine.UI.Image>();
                    if (extraRim) extraRim.enabled = false;
                }
                foreach (var name in new[] { "Web Solana Plate", "Web MagicBlock Plate", "PSG1 Solana Plate", "PSG1 MagicBlock Plate" })
                {
                    var plate = root.Find(name)?.GetComponent<UnityEngine.UI.Image>();
                    if (plate) plate.enabled = false;
                }
            }
            var dock = standard ? standard.Find("Web Partner Dock") : null;
            if (dock)
                Surface(dock, dock.Find("Navy Interior"), frameSprite, theme.Rounded, glassMaterial, sources, 1.4f);
            if (psg)
            {
                var psgDock = psg.Find("PSG1 Partner Dock");
                if (psgDock) psgDock.gameObject.SetActive(false);
                // Keep the partner marks inside the sign-in card's continuous TV glass.
                Put(psg.Find("Main Menu Blue Container"), 0, -190, 900, 440);
                psg.Find("Continue as Guest").gameObject.SetActive(false);
                Put(psg.Find("PSG1 Heading Plate"), 0, -40, 830, 88);
                var psgTitle = psg.Find("PSG1 Heading Plate")?.GetComponent<UnityEngine.UI.Image>();
                if (psgTitle)
                {
                    psgTitle.type = UnityEngine.UI.Image.Type.Sliced;
                    psgTitle.preserveAspect = false;
                    psgTitle.pixelsPerUnitMultiplier = 4;
                }
                Put(psg.Find("Heading"), 0, -40, 770, 76);
                Put(psg.Find("Description"), 0, -100, 840, 32);
                Put(psg.Find("Login Status"), 0, -270, 840, 28);
                psg.Find("Login Status").GetComponent<UnityEngine.UI.Text>().text = LoginScene.WalletOnlyStatus;
                Put(psg.Find("Built on Solana"), -173, -338, 320, 96);
                Put(psg.Find("Powered by MagicBlock"), 173, -338, 320, 96);
                Put(psg.Find("Partner Divider"), 0, -338, 1, 48);
                psg.Find("Partner Divider").gameObject.SetActive(true);
                var legend = psg.Find("PSG1 Controller Legend");
                Put(legend, 0, -513, 1240, 54);
                if (legend)
                {
                    Put(legend.Find("Legend Interior"), 0, 0, 1220, 40);
                    foreach (var name in new[] { "NAVIGATE", "SELECT", "BACK" })
                    {
                        var icon = legend.Find(name + " Icon") as RectTransform;
                        if (icon) icon.sizeDelta = new Vector2(40, 40);
                        var label = legend.Find(name) as RectTransform;
                        if (label) label.sizeDelta = new Vector2(184, 44);
                    }
                }
            }
            var serialized = new SerializedObject(layout);
            // The title skin contains no lettering; slice it at the shared title scale.
            var title = standard ? standard.Find("Web Heading Plate")?.GetComponent<UnityEngine.UI.Image>() : null;
            if (title)
            {
                title.type = UnityEngine.UI.Image.Type.Sliced;
                title.preserveAspect = false;
                title.pixelsPerUnitMultiplier = 4;
            }
            Place(serialized, "webElements", "Web Heading Plate", 462, 184, 504, 76);
            Place(serialized, "webElements", "Heading", 462, 184, 404, 62);
            Place(serialized, "webElements", "Web Partner Dock", 312, -387, 850, 100);
            Place(serialized, "webElements", "Built on Solana", 20, -387, 260, 78);
            Place(serialized, "webElements", "Powered by MagicBlock", 276, -387, 260, 78);
            Place(serialized, "webElements", "Solana dApp Store", 566, -387, 288, 288 * ArtAspect(standard, "Solana dApp Store"));
            Place(serialized, "webElements", "Partner Divider", 148, -387, 1, 48);
            Place(serialized, "webElements", "Store Divider", 400, -387, 1, 48);
            Place(serialized, "landscapeElements", "Main Menu Blue Container", 420, 56, 700, 400);
            Place(serialized, "landscapeElements", "Login Status", 420, -96, 634, 40);
            Place(serialized, "landscapeElements", "Web Heading Plate", 420, 191, 646, 88);
            Place(serialized, "landscapeElements", "Heading", 420, 191, 574, 70);
            Place(serialized, "landscapeElements", "Web Partner Dock", 617, -352, 540, 86);
            Place(serialized, "landscapeElements", "Built on Solana", 482, -352, 245, 72);
            Place(serialized, "landscapeElements", "Powered by MagicBlock", 752, -352, 245, 72);
            Place(serialized, "landscapeElements", "Partner Divider", 617, -352, 1, 42);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var portrait = serialized.FindProperty("portraitElements");
            var mobilePortrait = new LoginElementPlacement[portrait.arraySize];
            for (int i = 0; i < portrait.arraySize; i++)
            {
                var item = portrait.GetArrayElementAtIndex(i);
                var target = item.FindPropertyRelative("target").objectReferenceValue as RectTransform;
                mobilePortrait[i] = new LoginElementPlacement
                {
                    target = target,
                    position = item.FindPropertyRelative("position").vector2Value,
                    size = item.FindPropertyRelative("size").vector2Value,
                    visible = item.FindPropertyRelative("visible").boolValue,
                    alignment = (TextAnchor)item.FindPropertyRelative("alignment").enumValueIndex,
                    fontSize = item.FindPropertyRelative("fontSize").intValue,
                    minFontSize = item.FindPropertyRelative("minFontSize").intValue,
                    maxFontSize = item.FindPropertyRelative("maxFontSize").intValue,
                    bestFit = item.FindPropertyRelative("bestFit").boolValue
                };
                if (!target) continue;
                ref var entry = ref mobilePortrait[i];
                switch (target.name)
                {
                    case "Main Menu Blue Container": entry.position = new Vector2(0, 95); entry.size = new Vector2(720, 930); break;
                    case "Solana dApp Store": entry.position = new Vector2(0, -104); break;
                    case "Built on Solana": entry.position = new Vector2(-151, -222); break;
                    case "Powered by MagicBlock": entry.position = new Vector2(151, -222); break;
                    case "Partner Divider": entry.position = new Vector2(0, -222); break;
                    case "Login Status": entry.position = new Vector2(0, -312); break;
                    case "Continue as Guest": case "Or": case "Divider": case "Golden Eagle": case "Choose Entry": entry.visible = false; break;
                }
            }
            layout.ConfigureMobilePortrait(mobilePortrait);
            layout.Preview(MainMenuPlatform.Psg1);
            layout.Preview(MainMenuPlatform.Auto);
            Canvas.ForceUpdateCanvases();
            foreach (var surface in layout.GetComponentsInChildren<LoginGlassSurface>(true)) surface.Refresh();
            EditorUtility.SetDirty(layout);
        }

        static void Surface(Transform frame, Transform interior, Sprite skin, Sprite rounded, Material material,
            UnityEngine.UI.Image[] sources, float scale)
        {
            var image = frame.GetComponent<UnityEngine.UI.Image>();
            image.sprite = skin; image.type = UnityEngine.UI.Image.Type.Sliced; image.fillCenter = false;
            image.pixelsPerUnitMultiplier = scale; image.color = Color.white; image.raycastTarget = false;
            if (!interior)
            {
                interior = new GameObject("Glass Interior", typeof(RectTransform), typeof(UnityEngine.UI.Image)).transform;
                interior.SetParent(frame, false);
            }
            var rect = (RectTransform)interior;
            float ppu = Mathf.Max(1, image.pixelsPerUnit * scale);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(114, 138) / ppu;
            rect.offsetMax = -new Vector2(113, 126) / ppu;
            var glass = interior.GetComponent<LoginGlassSurface>();
            if (!glass) glass = interior.gameObject.AddComponent<LoginGlassSurface>();
            glass.Configure(rounded, material, sources, 46 / ppu);
            EditorUtility.SetDirty(glass);
        }

        static float ArtAspect(Transform root, string name)
        {
            var image = root ? root.Find(name)?.GetComponent<UnityEngine.UI.Image>() : null;
            return image && image.sprite ? image.sprite.rect.height / image.sprite.rect.width : .25f;
        }

        static void Put(Transform target, float x, float y, float width, float height)
        {
            if (!target) return;
            var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        }

        static void Place(SerializedObject serialized, string listName, string targetName, float x, float y, float width, float height)
        {
            var list = serialized.FindProperty(listName);
            for (int i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                var target = item.FindPropertyRelative("target").objectReferenceValue;
                if (!target || target.name != targetName) continue;
                item.FindPropertyRelative("position").vector2Value = new Vector2(x, y);
                item.FindPropertyRelative("size").vector2Value = new Vector2(width, height);
                break;
            }
        }
    }
}
