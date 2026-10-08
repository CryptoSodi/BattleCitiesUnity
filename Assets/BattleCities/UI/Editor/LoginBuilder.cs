using System;
using System.Linq;
using BattleCities.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class LoginBuilder
    {
        private const string Root = "Assets/BattleCities/UI/";
        private static MenuTheme theme;
        private static readonly Color Ink = new Color32(5, 30, 52, 255);

        [MenuItem("Battle Cities/Login/Rebuild from supplied assets")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save your scene changes before rebuilding Login.");
            theme = AssetDatabase.LoadAssetAtPath<MenuTheme>(Root + "Settings/ArcadeMenuTheme.asset");
            foreach (var name in new[] { "phantom-gold-button", "phantom-gold", "guest", "store", "eagle", "solana", "magicblock" })
            {
                string path = Root + "Art/login/" + name + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = name.StartsWith("phantom-gold", StringComparison.Ordinal) ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvasGo = new GameObject("Login Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.matchWidthOrHeight = 0.5f;
            var background = Pic("Blurred Battlefield", canvasGo.transform, theme.Background); Stretch(background.rectTransform, 0);
            var cover = background.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = theme.Background.rect.width / theme.Background.rect.height;
            var veil = Pic("Background Soft Shade", canvasGo.transform, null); Stretch(veil.rectTransform, 0); veil.color = new Color(0, 0.04f, 0.1f, 0.12f);
            var design = Rect("Login Layout (edit child positions freely)", canvasGo.transform, 0, 0, 760, 1260);
            var outer = Pic("Main Menu Blue Container", design, AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/reference-style-v2/shared/panels/navigation-container.png"));
            Box(outer.rectTransform, 0, -26, 720, 1172); outer.type = Image.Type.Sliced;
            var fill = Pic("Grey White TV Interior", outer.transform, theme.Rounded);
            Stretch(fill.rectTransform, 18); fill.type = Image.Type.Sliced; fill.color = new Color32(243, 241, 233, 255);
            var silver = Pic("Main Menu TV Frame", outer.transform, theme.SilverFrame);
            Stretch(silver.rectTransform, 12); silver.type = Image.Type.Sliced; silver.fillCenter = false;
            var logo = Pic("Battle Cities Logo", design, theme.Logo); Box(logo.rectTransform, 0, 458, 500, 333); logo.preserveAspect = true;
            Label("Heading", design, "ENTER THE BATTLEFIELD", 0, 261, 650, 64, 49, true);
            Label("Description", design, "Sign in to record matches, protect replays, and\nprepare your scores for verified leaderboards.", 0, 180, 610, 78, 29);
            var phantom = ArtButton("Connect Phantom", design, "phantom-gold-button", 0, 63, 596);
            var guest = ArtButton("Continue as Guest", design, "guest", 0, -67, 596);
            Rule(design, -190, -159, 204); Rule(design, 190, -159, 204);
            Label("Or", design, "OR", 0, -159, 90, 40, 29, true);
            var store = ArtButton("Solana dApp Store", design, "store", 0, -244, 470);
            var solana = Pic("Built on Solana", design, Art("solana")); Box(solana.rectTransform, -151, -354, 276, 91); solana.preserveAspect = true;
            var magic = Pic("Powered by MagicBlock", design, Art("magicblock")); Box(magic.rectTransform, 151, -354, 276, 91); magic.preserveAspect = true;
            var divider = Pic("Partner Divider", design, null); Box(divider.rectTransform, 0, -354, 2, 52); divider.color = new Color32(111, 134, 147, 255);
            var eagle = Pic("Golden Eagle", design, Art("eagle")); Box(eagle.rectTransform, 0, -439, 142, 74); eagle.preserveAspect = true;
            Rule(design, -196, -439, 188); Rule(design, 196, -439, 188);
            Label("Choose Entry", design, "Choose how you want to enter Battle Cities.", 0, -491, 620, 38, 28);
            var status = Label("Login Status", design, "Guest progress stays on this device.\nConnect a wallet for verified play and rewards.", 0, -537, 602, 62, 24);
            var flowGo = new GameObject("Login Flow"); flowGo.SetActive(false);
            var api = flowGo.AddComponent<MainMenuApiClient>(); api.enabled = false;
            api.ConfigureGuestFallback(false); api.ConfigureAutomaticRefresh(false);
            var flow = flowGo.AddComponent<LoginScene>();
            flow.Configure(api, phantom, guest, store, status, theme);
            flow.ConfigureMobileWalletSkin(Art("mobile-wallet-green"));
            flow.ConfigureMobileWalletIcon(Art("phantom-gold"));
            flowGo.SetActive(true);
            var buttons = new[] { phantom, guest, store };
            for (int i = 0; i < buttons.Length; i++) buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i + 2) % 3], selectOnDown = buttons[(i + 1) % 3] };
            var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            Canvas.ForceUpdateCanvases(); canvasGo.AddComponent<LoginLayout>().Configure(design);
            LoginGlassLayout.ApplyToLayout(canvasGo.GetComponent<LoginLayout>());
            EditorSceneManager.SaveScene(scene, "Assets/BattleCities/Scenes/Login.unity");
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != scene.path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(scene.path, true)); EditorBuildSettings.scenes = scenes.ToArray();
            Selection.activeGameObject = canvasGo;
            Debug.Log("Rebuilt Login with the existing main-menu blue container and TV frame; supplied cutout assets only.");
        }
        private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/login/" + name + ".png");
        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var r = (RectTransform)go.transform; Box(r, x, y, w, h); return r; }
        private static void Box(RectTransform r, float x, float y, float w, float h)
        { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(x,y); r.sizeDelta = new Vector2(w,h); }
        private static void Stretch(RectTransform r, float inset)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.one * inset; r.offsetMax = -Vector2.one * inset; }
        private static Image Pic(string name, Transform parent, Sprite sprite)
        { var r = Rect(name, parent, 0,0,100,100); var image = r.gameObject.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false; return image; }
        private static Text Label(string name, Transform parent, string value, float x,float y,float w,float h,int size,bool bold=false)
        {
            var r = Rect(name,parent,x,y,w,h); var label = r.gameObject.AddComponent<Text>(); label.font = bold ? theme.HeadingFont : theme.BodyFont;
            label.text = value; label.fontSize = size; label.color = Ink; label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; return label;
        }
        private static Button ArtButton(string name, Transform parent, string art, float x,float y,float width)
        {
            var sprite = Art(art); var image = Pic(name, parent, sprite); Box(image.rectTransform,x,y,width,width*sprite.rect.height/sprite.rect.width);
            image.preserveAspect = true; image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1,.96f,.76f);
            colors.selectedColor = new Color(1,.96f,.76f); colors.pressedColor = new Color(.78f,.78f,.78f); colors.disabledColor = new Color(.5f,.5f,.5f); button.colors = colors;
            return button;
        }
        private static void Rule(Transform parent,float x,float y,float width)
        { var image = Pic("Divider",parent,null); Box(image.rectTransform,x,y,width,2); image.color = new Color32(111,134,147,255); }
    }
}
