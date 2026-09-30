using System.Collections.Generic;
using BattleCities.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using MobileScreen = UnityEngine.Device.Screen;

namespace BattleCities
{
    // Built only for Android phones. All combat buttons feed the existing Gameplay actions.
    public sealed class BattleTouchControls : MonoBehaviour
    {
        private BattleGame game;
        private Canvas canvas;
        private RectTransform safeRoot, pauseRect;
        private CanvasGroup visibility;
        private readonly List<BattleTouchControl> controls = new List<BattleTouchControl>();
        private BattleTouchControl move, fire, special, switchSpecial;
        private readonly BattleTouchControl[] slots = new BattleTouchControl[4];
        private readonly UnityEngine.UI.RawImage[] slotIcons = new UnityEngine.UI.RawImage[4];
        private readonly TMP_Text[] slotCounts = new TMP_Text[4], slotNames = new TMP_Text[4];
        private TMP_Text fireHint, specialName, specialHint, pauseLabel, stateLabel, feedback;
        private UnityEngine.UI.Button pauseButton, resumeButton, restartButton, nextButton;
        private GameObject statePanel;
        private Texture2D circleTexture;
        private Sprite circle;
        private bool ownsOrientation;
        private readonly Color navy = new Color(.025f, .10f, .18f, .8f);
        private readonly Color blue = new Color(.06f, .38f, .65f, .88f);
        private readonly Color gold = new Color(1f, .73f, .10f, .95f);

        public void Initialize(BattleGame owner)
        {
            if (game) return;
            game = owner;
            ownsOrientation = RuntimePlatformInfo.Current == GameRuntimePlatform.Android;
            if (ownsOrientation) MobileBattleOrientation.ShowBattle();
            Build();
            Refresh();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private UnityEngine.UI.Image Surface(RectTransform rect, Color color, bool round = true, bool interactive = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = round ? circle : null;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = round;
            image.color = color;
            image.raycastTarget = interactive;
            return image;
        }

        private TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var rect = Rect(name, parent, new Vector2(.5f, .5f), position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private void MakeCircle()
        {
            circleTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Touch control circle", filterMode = FilterMode.Bilinear };
            var pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(31.5f - distance));
            }
            circleTexture.SetPixels32(pixels); circleTexture.Apply(false, true);
            circle = Sprite.Create(circleTexture, new UnityEngine.Rect(0, 0, 64, 64), new Vector2(.5f, .5f));
        }

        private BattleTouchControl Action(string name, string binding, Vector2 anchor, Vector2 position, float size, Color color)
        {
            var rect = Rect(name, safeRoot, anchor, position, Vector2.one * size);
            Surface(rect, color, true, true);
            Surface(Rect("Inset", rect, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * (size - 6)), navy);
            var control = rect.gameObject.AddComponent<BattleTouchControl>();
            control.Configure(binding);
            controls.Add(control);
            return control;
        }

        private UnityEngine.UI.Button Button(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string caption, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(name, parent, anchor, position, size);
            var image = Surface(rect, blue, false, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.onClick.AddListener(action);
            Label("Label", rect, caption, Vector2.zero, size, 21, Color.white);
            return button;
        }

        private void Build()
        {
            MakeCircle();
            if (!EventSystem.current)
            {
                var events = new GameObject("Battle Touch EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            var root = Rect("Battle Touch Controls", transform, Vector2.zero, Vector2.zero, Vector2.zero);
            canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            var scaler = root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1;
            root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            visibility = root.gameObject.AddComponent<CanvasGroup>();
            safeRoot = Rect("Safe Area", root, Vector2.zero, Vector2.zero, Vector2.zero);

            var pad = Rect("Movement", safeRoot, Vector2.zero, new Vector2(145, 148), Vector2.one * 212);
            Surface(pad, blue, true, true);
            Surface(Rect("Inset", pad, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * 204), navy);
            Label("Up", pad, "▲", new Vector2(0, 76), new Vector2(30, 30), 24, Color.white);
            Label("Down", pad, "▼", new Vector2(0, -76), new Vector2(30, 30), 24, Color.white);
            Label("Left", pad, "<", new Vector2(-76, 0), new Vector2(30, 30), 30, Color.white);
            Label("Right", pad, ">", new Vector2(76, 0), new Vector2(30, 30), 30, Color.white);
            var thumb = Rect("Thumb", pad, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * 76);
            Surface(thumb, new Color(.32f, .75f, 1f, .95f));
            move = pad.gameObject.AddComponent<BattleTouchControl>();
            move.Configure("move", thumb); controls.Add(move);

            fire = Action("Fire", "fire", Vector2.right, new Vector2(-112, 153), 148, gold);
            Label("Title", fire.transform, "FIRE", new Vector2(0, 12), new Vector2(125, 40), 30, Color.white);
            fireHint = Label("Charge", fire.transform, "HOLD TO CHARGE", new Vector2(0, -25), new Vector2(140, 30), 12, gold);
            special = Action("Special", "special", Vector2.right, new Vector2(-272, 108), 116, blue);
            Label("Title", special.transform, "SPECIAL", new Vector2(0, 26), new Vector2(110, 24), 15, gold);
            specialName = Label("Attack", special.transform, "MINE", Vector2.zero, new Vector2(110, 28), 18, Color.white);
            specialHint = Label("Status", special.transform, "READY", new Vector2(0, -27), new Vector2(112, 24), 13, Color.white);
            switchSpecial = Action("Switch Special", "switchSpecial", Vector2.right, new Vector2(-275, 219), 66, blue);
            Label("Title", switchSpecial.transform, "SWAP", Vector2.zero, new Vector2(60, 30), 15, Color.white);

            for (int i = 0; i < 4; i++)
            {
                slots[i] = Action("Powerup " + (i + 1), "slot" + (i + 1), new Vector2(.5f, 0), new Vector2((i - 1.5f) * 84, 64), 74, blue);
                var iconRect = Rect("Icon", slots[i].transform, new Vector2(.5f, .5f), new Vector2(0, 5), Vector2.one * 46);
                slotIcons[i] = iconRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                slotIcons[i].raycastTarget = false;
                slotCounts[i] = Label("Count", slots[i].transform, "0", new Vector2(22, -22), new Vector2(34, 26), 18, gold);
                slotNames[i] = Label("Name", slots[i].transform, "EMPTY", new Vector2(0, -52), new Vector2(82, 22), 12, Color.white);
            }
            feedback = Label("Action Feedback", safeRoot, "", Vector2.zero, new Vector2(590, 36), 17, gold);
            feedback.rectTransform.anchorMin = feedback.rectTransform.anchorMax = new Vector2(.5f, 0);
            feedback.rectTransform.anchoredPosition = new Vector2(0, 150);
            feedback.textWrappingMode = TextWrappingModes.Normal;

            pauseButton = Button("Pause", safeRoot, new Vector2(.5f, 1), new Vector2(0, -86), new Vector2(98, 48), "PAUSE", () =>
            { game.CancelTouchGameplay(); game.Paused = !game.Paused; });
            pauseLabel = pauseButton.GetComponentInChildren<TMP_Text>();
            pauseRect = (RectTransform)pauseButton.transform;

            var panel = Rect("Match State", safeRoot, new Vector2(.5f, .5f), Vector2.zero, new Vector2(330, 235));
            statePanel = panel.gameObject;
            Surface(panel, new Color(.025f, .07f, .12f, .96f), false, true);
            stateLabel = Label("State", panel, "PAUSED", new Vector2(0, 74), new Vector2(310, 44), 30, gold);
            resumeButton = Button("Resume", panel, new Vector2(.5f, .5f), new Vector2(0, 15), new Vector2(256, 48), "RESUME", () => game.Paused = false);
            restartButton = Button("Restart", panel, new Vector2(.5f, .5f), new Vector2(0, -46), new Vector2(256, 48), "RESTART", () => game.LoadStage(game.Stage));
            nextButton = Button("Next Stage", panel, new Vector2(.5f, .5f), new Vector2(0, 15), new Vector2(256, 48), "NEXT STAGE", () => game.LoadStage(game.Stage + 1));
        }

        private void LateUpdate() { if (game) Refresh(); }

        private void Refresh()
        {
            float width = Mathf.Max(1, MobileScreen.width), height = Mathf.Max(1, MobileScreen.height);
            var safe = MobileScreen.safeArea;
            safeRoot.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            safeRoot.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            pauseRect.anchoredPosition = new Vector2(0, -BattleHud.TopHeightPixels / Mathf.Max(.01f, canvas.scaleFactor) - 36);
            bool visible = width > height && !game.TouchLobbyVisible;
            visibility.alpha = visible ? 1 : 0;
            visibility.blocksRaycasts = visible;
            bool active = visible && game.TouchActionsAvailable;
            move.Interactable = fire.Interactable = switchSpecial.Interactable = active;
            var state = game.Simulation;
            special.Interactable = active && state.CanUseSecondary;
            if (!visible) CancelInput();
            if (state == null) return;
            feedback.text = game.TouchConsumePending ? "USING POWER-UP…" : state.SecondaryStatus;
            specialName.text = state.EquippedSecondary == SecondaryAttack.LandDrone ? "LAND DRONE" : state.EquippedSecondary == SecondaryAttack.PatrolDrone ? "DRONE"
                : state.EquippedSecondary == SecondaryAttack.GroundTurret ? "TURRET" : state.EquippedSecondary == SecondaryAttack.None ? "NONE" : "MINE";
            specialHint.text = state.SecondaryCooldown > 0 ? state.SecondaryCooldown.ToString("0.0") + "s" : state.SecondaryCount >= state.SecondaryLimit ? "LIMIT" : "READY";
            fireHint.text = game.TouchChargeProgress >= 1 ? "RELEASE!" : fire.Held ? Mathf.RoundToInt(game.TouchChargeProgress * 100) + "%" : "HOLD TO CHARGE";
            for (int i = 0; i < 4; i++)
            {
                string type = game.TouchSlotType(i);
                bool hasIcon = game.PowerupAtlas && BattleHud.TryPowerupUv(type, out _);
                slots[i].Interactable = visible && game.TouchSlotAvailable(i);
                slotIcons[i].enabled = hasIcon;
                if (hasIcon)
                {
                    BattleHud.TryPowerupUv(type, out var uv);
                    slotIcons[i].texture = game.PowerupAtlas; slotIcons[i].uvRect = uv;
                    slotIcons[i].color = slots[i].Interactable ? Color.white : new Color(1, 1, 1, .4f);
                }
                slotCounts[i].text = game.TouchConsumePending ? "…" : game.TouchSlotCount(i).ToString();
                slotNames[i].text = string.IsNullOrEmpty(type) ? "EMPTY" : type.ToUpperInvariant();
            }
            pauseLabel.text = game.Paused ? "RESUME" : "PAUSE";
            bool ended = state.Lost || state.Won;
            pauseButton.interactable = !ended;
            statePanel.SetActive(!game.IsOnline && (game.Paused || ended));
            stateLabel.text = ended ? state.Won ? "STAGE CLEAR" : "GAME OVER" : "PAUSED";
            resumeButton.gameObject.SetActive(!ended);
            nextButton.gameObject.SetActive(state.Won && game.Stage < 35);
        }

        public void CancelInput() { foreach (var control in controls) if (control) control.CancelInput(); }
        private void OnDisable() { CancelInput(); if (canvas) canvas.gameObject.SetActive(false); }
        private void OnEnable() { if (canvas) canvas.gameObject.SetActive(true); }
        private void OnDestroy()
        {
            if (canvas) Destroy(canvas.gameObject);
            if (circle) Destroy(circle);
            if (circleTexture) Destroy(circleTexture);
            if (ownsOrientation) MobileBattleOrientation.ShowMenu();
        }
    }
}
