using System.Collections.Generic;
using BattleCities.Core;
using TMPro;
using UnityEngine;
using BattleCities.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using MobileScreen = UnityEngine.Device.Screen;

namespace BattleCities
{
    // Shared by Android phones and mobile browsers; uses gameplay actions and the guarded special command queue.
    public sealed partial class BattleTouchControls : MonoBehaviour
    {
        private BattleGame game;
        private Canvas canvas;
        private RectTransform safeRoot;
        private CanvasGroup visibility;
        private readonly List<BattleTouchControl> controls = new List<BattleTouchControl>();
        private readonly UI.ArcadeTextStyles textStyles=new UI.ArcadeTextStyles();
        private BattleTouchControl move, fire;
        private readonly BattleTouchControl[] slots = new BattleTouchControl[4];
        private readonly UnityEngine.UI.RawImage[] slotIcons = new UnityEngine.UI.RawImage[4];
        private readonly TMP_Text[] slotCounts = new TMP_Text[4];
        private readonly GameObject[] slotBadges=new GameObject[4];
        private RectTransform leftZone,rightZone;
        private TMP_Text fireHint, feedback;
        private Texture2D circleTexture;
        private Sprite circle;
        private bool ownsOrientation;
        private readonly Color navy = new Color(.025f, .10f, .18f, .8f);
        private readonly Color blue = new Color(.06f, .38f, .65f, .88f);
        private readonly Color gold = new Color(1f, .73f, .10f, .95f);
        internal Facing? TurretAim => fire && fire.Held ? BattleGamepadBindings.Direction(fire.LastAim) : null;
        internal Facing? TakeReleasedTurretAim() => fire ? BattleGamepadBindings.Direction(fire.TakeReleasedAim()) : null;

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
            if(UI.ArcadeTextStyles.IsWhite(color))textStyles.Apply(label,UI.ArcadeTextTreatment.WhiteButton);
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

            fire = Action("Fire", "fire", Vector2.right, new Vector2(-272, 148), 148, gold);
            var fireThumb = Rect("Turret Thumb", fire.transform, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * 84);
            Surface(fireThumb, blue);
            Label("Title", fireThumb, "FIRE", Vector2.zero, new Vector2(80, 40), 25, Color.white);
            fire.ConfigureTurret(fireThumb);
            fireHint = Label("Charge", fire.transform, "HOLD TO CHARGE", new Vector2(0, -94), new Vector2(154, 30), 12, gold);
            move.ConfigureFloating();fire.ConfigureFloating();
            leftZone=Zone("Movement touch area",move,0,.5f);
            rightZone=Zone("Turret touch area",fire,.5f,1);
            BuildSpecialButtons();

            for (int i = 0; i < 4; i++)
            {
                var slot=Rect("Powerup "+(i+1),safeRoot,new Vector2(.5f,0),new Vector2((i-1.5f)*84,64),Vector2.one*74);
                Surface(slot,Color.clear,false,true);
                slots[i]=slot.gameObject.AddComponent<BattleTouchControl>();slots[i].Configure("slot"+(i+1));controls.Add(slots[i]);
                var iconRect = Rect("Icon", slots[i].transform, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * 64);
                slotIcons[i] = iconRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                slotIcons[i].raycastTarget = false;
                var badge=Rect("Count badge",slot,new Vector2(.5f,.5f),new Vector2(24,-24),new Vector2(28,28));
                Surface(badge,navy);slotBadges[i]=badge.gameObject;
                slotCounts[i] = Label("Count", badge, "0",Vector2.zero,new Vector2(30,28),20,Color.white);
            }
            feedback = Label("Action Feedback", safeRoot, "", Vector2.zero, new Vector2(590, 36), 17, gold);
            feedback.rectTransform.anchorMin = feedback.rectTransform.anchorMax = new Vector2(.5f, 0);
            feedback.rectTransform.anchoredPosition = new Vector2(0, 150);
            feedback.textWrappingMode = TextWrappingModes.Normal;

        }

        private RectTransform Zone(string name,BattleTouchControl target,float left,float right)
        {
            var zone=Rect(name,safeRoot,Vector2.zero,Vector2.zero,Vector2.zero);
            zone.anchorMin=new Vector2(left,0);zone.anchorMax=new Vector2(right,1);zone.offsetMin=zone.offsetMax=Vector2.zero;
            Surface(zone,Color.clear,false,true);zone.gameObject.AddComponent<BattleTouchZone>().Configure(target);
            zone.SetAsFirstSibling();return zone;
        }

        private void LateUpdate() { if (game) Refresh(); }

        private void Refresh()
        {
            float width = Mathf.Max(1, MobileScreen.width), height = Mathf.Max(1, MobileScreen.height);
            var safe = MobileScreen.safeArea;
            safeRoot.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            safeRoot.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            float topReserve=Mathf.Max(0,BattleHud.TopHeightPixels-(height-safe.yMax))/Mathf.Max(.001f,canvas.scaleFactor);
            leftZone.offsetMax=rightZone.offsetMax=new Vector2(0,-topReserve);
            bool visible = (width > height || RuntimePlatformInfo.IsMobileBrowser) && !game.TouchLobbyVisible;
            visibility.alpha = visible ? 1 : 0;
            visibility.blocksRaycasts = visible;
            bool active = visible && game.TouchActionsAvailable;
            LayoutPowerupsInHud(width,height,safe);
            move.Interactable = fire.Interactable = active;
            var state = game.Simulation;
            RefreshSpecialButtons(width,height,safe,visible);
            if (!visible) CancelInput();
            if (state == null) return;
            feedback.text = game.TouchConsumePending ? "USING POWER-UP…" : state.SecondaryStatus;
            fireHint.text = game.TouchChargeProgress >= 1 ? "RELEASE!" : fire.Held ? Mathf.RoundToInt(game.TouchChargeProgress * 100) + "%" : "HOLD TO CHARGE";
            for (int i = 0; i < 4; i++)
            {
                string type = game.TouchSlotType(i);
                bool showSlots=!state.IsMultiplayer&&!game.IsReplaying;
                if(slots[i].gameObject.activeSelf!=showSlots)slots[i].gameObject.SetActive(showSlots);
                bool hasIcon = showSlots&&ExtendedPowerupArt.TryImage(game.PowerupAtlas,type,out _,out _);
                slots[i].Interactable = visible && game.TouchSlotAvailable(i);
                slotIcons[i].enabled = hasIcon;
                slots[i].GetComponent<UnityEngine.UI.Image>().raycastTarget=hasIcon;
                slotBadges[i].SetActive(hasIcon);
                if (hasIcon)
                {
                    ExtendedPowerupArt.TryImage(game.PowerupAtlas,type,out var texture,out var uv);
                    slotIcons[i].texture=texture;slotIcons[i].uvRect=uv;
                    slotIcons[i].color = new Color(1,1,1,slots[i].Interactable?.3f:.15f);
                }
                slotCounts[i].text = game.TouchConsumePending ? "…" : game.TouchSlotCount(i).ToString();
            }
        }

        public void CancelInput() { foreach (var control in controls) if (control) control.CancelInput(); }
        private void LayoutPowerupsInHud(float screenWidth,float screenHeight,UnityEngine.Rect safe)
        {
            float pixelWidth=canvas.pixelRect.width>0?canvas.pixelRect.width:screenWidth;
            float pixelHeight=canvas.pixelRect.height>0?canvas.pixelRect.height:screenHeight;
            var layout=BattleHudLayout.ForViewport(pixelWidth,pixelHeight);
            float scale=layout.Scale/Mathf.Max(.001f,canvas.scaleFactor);
            float safeTop=(1-safe.yMax/screenHeight)*pixelHeight/Mathf.Max(.001f,canvas.scaleFactor);
            for(int i=0;i<4;i++)
            {
                var rect=(RectTransform)slots[i].transform;rect.anchorMin=rect.anchorMax=Vector2.one;
                rect.anchoredPosition=new Vector2(-(6+BattleHudLayout.AndroidPowerupSlotWidth*.5f+(3-i)*(BattleHudLayout.AndroidPowerupSlotWidth+BattleHudLayout.AndroidPowerupGap))*scale,
                    -layout.TopHeight*.5f*scale+safeTop);
                rect.sizeDelta=new Vector2(BattleHudLayout.AndroidPowerupSlotWidth,layout.TopHeight)*scale;
                slotIcons[i].rectTransform.sizeDelta=Vector2.one*BattleHudLayout.AndroidPowerupIconSize*scale;
                var badge=(RectTransform)slotBadges[i].transform;
                badge.anchoredPosition=new Vector2(16,13)*scale;badge.sizeDelta=Vector2.one*18*scale;
                slotCounts[i].rectTransform.sizeDelta=new Vector2(22,20)*scale;slotCounts[i].fontSize=14*scale;
            }
        }
        private void OnDisable() { CancelInput(); if (canvas) canvas.gameObject.SetActive(false); }
        private void OnEnable() { if (canvas) canvas.gameObject.SetActive(true); }
        private void OnDestroy()
        {
            DisposeSpecialButtons();
            textStyles.Dispose();
            if (canvas) Destroy(canvas.gameObject);
            if (circle) Destroy(circle);
            if (circleTexture) Destroy(circleTexture);
            if (ownsOrientation) MobileBattleOrientation.ShowMenu();
        }
    }
}
