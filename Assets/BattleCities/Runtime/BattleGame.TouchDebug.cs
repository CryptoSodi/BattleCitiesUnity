using System;
using System.Collections.Generic;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        private GameObject touchDebugRoot;
        private readonly List<Action> touchDebugRefresh = new List<Action>();
        private Font touchDebugFont;

        private void Start()
        {
            if (RuntimePlatformInfo.IsPsg1) CreateTouchDebugMenu();
        }

        private void LateUpdate()
        {
            if (!RuntimePlatformInfo.IsPsg1) return;
            if (!touchDebugRoot) CreateTouchDebugMenu();
            if (!touchDebugRoot) return;
            if (touchDebugRoot.activeSelf != showDebug) touchDebugRoot.SetActive(showDebug);
            if (!showDebug) return;
            foreach (var refresh in touchDebugRefresh) refresh();
        }

        private static RectTransform UiObject(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            return item.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = minimum;
            rect.offsetMax = maximum;
        }

        private UnityEngine.UI.Text UiText(string name, Transform parent, string value, int size, TextAnchor alignment)
        {
            var rect = UiObject(name, parent);
            var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = touchDebugFont;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = alignment;
            label.text = value;
            label.raycastTarget = false;
            return label;
        }

        private static void Height(GameObject item, float height)
        {
            item.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
        }

        private UnityEngine.UI.Text RowLabel(Transform parent, string value, float height = 29)
        {
            var text = UiText(value, parent, value, 19, TextAnchor.MiddleLeft);
            Height(text.gameObject, height);
            return text;
        }

        private UnityEngine.UI.Button RowButton(Transform parent, string value, Action onClick)
        {
            var rect = UiObject(value, parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.08f, .36f, .69f, .98f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.onClick.AddListener(() => onClick());
            Height(rect.gameObject, 46);
            var caption = UiText("Label", rect, value, 19, TextAnchor.MiddleCenter);
            Stretch(caption.rectTransform, new Vector2(5, 2), new Vector2(-5, -2));
            return button;
        }

        private void ToggleRow(Transform parent, string label, Func<bool> read, Action<bool> write)
        {
            var button = RowButton(parent, label, () => write(!read()));
            var caption = button.GetComponentInChildren<UnityEngine.UI.Text>();
            touchDebugRefresh.Add(() => caption.text = (read() ? "[ON]  " : "[OFF]  ") + label);
        }

        private void SliderRow(Transform parent, string label, Func<float> read, Action<float> write,
            float min, float max, Func<float> dynamicMin = null, string format = "0.00")
        {
            var caption = RowLabel(parent, label);
            var rect = UiObject(label + " slider", parent);
            Height(rect.gameObject, 34);
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = Mathf.Clamp(read(), min, max);
            slider.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };

            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(.035f, .12f, .2f, 1);
            var fillArea = UiObject("Fill Area", rect);
            Stretch(fillArea, new Vector2(10, 10), new Vector2(-10, -10));
            var fill = UiObject("Fill", fillArea);
            Stretch(fill, Vector2.zero, Vector2.zero);
            fill.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.14f, .72f, .98f, 1);
            var handleArea = UiObject("Handle Area", rect);
            Stretch(handleArea, new Vector2(10, 0), new Vector2(-10, 0));
            var handle = UiObject("Handle", handleArea);
            handle.sizeDelta = new Vector2(20, 30);
            handle.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            slider.onValueChanged.AddListener(write.Invoke);
            touchDebugRefresh.Add(() =>
            {
                if (dynamicMin != null) slider.minValue = dynamicMin();
                slider.SetValueWithoutNotify(Mathf.Clamp(read(), slider.minValue, max));
                caption.text = label + "  " + read().ToString(format);
            });
        }

        private void CreateTouchDebugMenu()
        {
            if (touchDebugRoot || Simulation == null) return;
            touchDebugFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!EventSystem.current)
            {
                var events = new GameObject("Touch Debug EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            var canvasRect = UiObject("Touch Debug Canvas", transform);
            touchDebugRoot = canvasRect.gameObject;
            var canvas = touchDebugRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = touchDebugRoot.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1240, 1080);
            scaler.matchWidthOrHeight = .5f;
            touchDebugRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var panel = UiObject("Debug Panel", canvasRect);
            panel.anchorMin = new Vector2(1, 0);
            panel.anchorMax = Vector2.one;
            panel.pivot = Vector2.one;
            panel.offsetMin = new Vector2(-344, 38);
            panel.offsetMax = new Vector2(-10, -88);
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.035f, .105f, .18f, .96f);

            var title = UiText("Title", panel, "BATTLE CITIES  DEBUG", 23, TextAnchor.MiddleLeft);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = Vector2.up;
            title.rectTransform.pivot = Vector2.up;
            title.rectTransform.anchoredPosition = new Vector2(14, -7);
            title.rectTransform.sizeDelta = new Vector2(260, 45);

            var closeRect = UiObject("Close", panel);
            closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-8, -8);
            closeRect.sizeDelta = new Vector2(40, 40);
            var closeImage = closeRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            closeImage.color = new Color(.13f, .39f, .64f, 1);
            var close = closeRect.gameObject.AddComponent<UnityEngine.UI.Button>();
            close.targetGraphic = closeImage;
            close.onClick.AddListener(() => showDebug = false);
            var closeText = UiText("X", closeRect, "X", 25, TextAnchor.MiddleCenter);
            Stretch(closeText.rectTransform, Vector2.zero, Vector2.zero);

            var scrollRoot = UiObject("Scroll View", panel);
            Stretch(scrollRoot, new Vector2(10, 10), new Vector2(-10, -58));
            var scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            var viewport = UiObject("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .01f);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            scroll.viewport = viewport;
            var content = UiObject("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(4, 4, 3, 8);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            RowLabel(content, "STAGE");
            SliderRow(content, "Stage", () => requestedStage, v => requestedStage = Mathf.RoundToInt(v), 0, 35, null, "0");
            RowButton(content, "Load selected stage", () => LoadStage(requestedStage));
            RowLabel(content, "WEATHER & CAMERA");
            ToggleRow(content, "Day/night cycle", () => weather.Cycle, v => weather.Cycle = v);
            ToggleRow(content, "Rain", () => weather.Rain, v => weather.Rain = v);
            ToggleRow(content, "Drifting clouds", () => weather.Clouds, v => weather.Clouds = v);
            SliderRow(content, "Time of day", () => weather.TimeOfDay, v => weather.TimeOfDay = v, 0, 1);
            SliderRow(content, "Rain intensity", () => weather.RainIntensity, v => weather.RainIntensity = v, 0, 1);
            ToggleRow(content, "Tank headlights", () => Headlights, v => Headlights = v);
            ToggleRow(content, "Tank trail dust", () => Dust, v => Dust = v);
            ToggleRow(content, "Enemy shooting", () => EnemyFire, v => EnemyFire = v);
            ToggleRow(content, "Automatic camera", () => AutomaticCamera, v => AutomaticCamera = v);
            ToggleRow(content, "Camera shake", () => CameraShake, v => CameraShake = v);
            SliderRow(content, "Elevation", () => CameraElevation, v => CameraElevation = v, 40, 89.9f, null, "0");
            SliderRow(content, "Zoom", () => Zoom, v => Zoom = v, .5f, 2, () => Stage == 0 ? .5f : .7f);

            RowLabel(content, "SECONDARY ATTACK");
            RowButton(content, "Equip mine", () => Simulation.EquippedSecondary = SecondaryAttack.Mine);
            RowButton(content, "Equip drone", () => Simulation.EquippedSecondary = SecondaryAttack.PatrolDrone);
            RowButton(content, "Equip turret", () => Simulation.EquippedSecondary = SecondaryAttack.GroundTurret);
            RowButton(content, "Equip land attack drone", () => Simulation.EquippedSecondary = SecondaryAttack.LandDrone);
            RowButton(content, "Deploy land drone for testing", () =>
            { Simulation.EquippedSecondary = SecondaryAttack.LandDrone; Simulation.UseSecondary(); });
            RowButton(content, "Place turret for testing", () =>
            { Simulation.EquippedSecondary = SecondaryAttack.GroundTurret; Simulation.UseSecondary(); });

            RowLabel(content, "TEST POWER-UPS  (FREE)");
            for (int i = 0; i < debugPowerupTypes.Length; i++)
            {
                string type = debugPowerupTypes[i];
                var button = RowButton(content, debugPowerupNames[i], () => DebugConsumePowerup(type));
                touchDebugRefresh.Add(() => button.interactable =
                    Simulation != null && Simulation.Player != null && !Simulation.Lost && !Simulation.Won && !consumePending);
            }
            var status = RowLabel(content, debugPowerupStatus, 48);
            touchDebugRefresh.Add(() => status.text = debugPowerupStatus);
            touchDebugRoot.SetActive(showDebug);
        }
    }
}
