using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>Renders the imported reward chest into the How It Works icon.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RawImage))]
    public sealed class RewardChestMenuPreview : MonoBehaviour
    {
        [SerializeField] private GameObject chestPrefab;
        [SerializeField] private AnimationClip openClip;
        [SerializeField] private AnimationClip closeClip;
        [SerializeField] private AnimationClip burstClip;
        [SerializeField] private string[] ranks = { "1st", "2nd", "3rd", "4th - 10th" };
        [SerializeField] private string[] prizes = { "100 SKR", "50 SKR", "25 SKR", "15 SKR" };

        private const int PreviewLayer = 30;
        // Matches reward-sequence.json and sequence-player.js in the supplied asset folder.
        private const float ArriveTime = 1.1f;
        private const float OpenTime = 2f;
        private const float HoldTime = 2.8f;
        private const float BurstTime = 1.1f;
        private const float GapTime = 0.3f;
        private const int StageCount = 4;
        private static int nextSlot;

        private readonly List<Transform> animatedParts = new List<Transform>();
        private readonly List<Vector3> originalPositions = new List<Vector3>();
        private readonly List<Quaternion> originalRotations = new List<Quaternion>();
        private readonly List<Vector3> originalScales = new List<Vector3>();
        private readonly List<Material> materialCopies = new List<Material>();

        private GameObject rig;
        private GameObject model;
        private Camera previewCamera;
        private RenderTexture target;
        private RawImage output;
        private TextMeshPro rankLabel;
        private TextMeshPro prizeLabel;
        private float startTime;
        private float lastFrameTime = -100f;
        private int currentStage = -1;

        public void Configure(GameObject prefab, AnimationClip open, AnimationClip close, AnimationClip burst)
        {
            chestPrefab = prefab;
            openClip = open;
            closeClip = close;
            burstClip = burst;
            TearDown();
            Build();
            RenderAt(1.75f);
        }

        /// <summary>Updates one stage's visual label without changing game payout data.</summary>
        public void SetPrizeLabel(int stage, string rank, string prize)
        {
            if (stage < 0 || stage >= StageCount)
                throw new System.ArgumentOutOfRangeException(nameof(stage));
            EnsurePrizeLabels();
            ranks[stage] = rank ?? string.Empty;
            prizes[stage] = prize ?? string.Empty;
            if (currentStage == stage)
                UpdatePrizeText(stage);
        }

        private void OnEnable()
        {
            Build();
            RenderAt(1.75f);
        }

        private void OnDisable() => TearDown();
        private void OnDestroy() => TearDown();

        private void Update()
        {
            if (!isActiveAndEnabled || !chestPrefab || !openClip || !closeClip || !burstClip)
                return;
            if (!rig)
                Build();
            if (!rig)
                return;

            float now = Time.realtimeSinceStartup;
            if (now - lastFrameTime < 1f / 24f)
                return;
            lastFrameTime = now;
            RenderAt(now - startTime);
        }

        private void Build()
        {
            if (rig || !chestPrefab || !openClip || !closeClip || !burstClip)
                return;

            output = GetComponent<RawImage>();
            output.raycastTarget = false;
            var origin = new Vector3(5000f + (++nextSlot * 8f), 5000f, 5000f);
            rig = new GameObject("Reward Chest UI Preview");
            rig.hideFlags = HideFlags.HideAndDontSave;
            rig.transform.position = origin;

            model = Instantiate(chestPrefab, rig.transform, false);
            model.name = "Animated Reward Chest";
            model.hideFlags = HideFlags.HideAndDontSave;
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;
            foreach (var part in model.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = PreviewLayer;
                animatedParts.Add(part);
                originalPositions.Add(part.localPosition);
                originalRotations.Add(part.localRotation);
                originalScales.Add(part.localScale);
                if (part.name == "RewardText_Default")
                    part.gameObject.SetActive(false); // The GLB's demo label uses a different currency.
            }

            var replacements = new Dictionary<Material, Material>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (!source)
                        continue;
                    if (!replacements.TryGetValue(source, out var copy))
                    {
                        copy = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
                        replacements.Add(source, copy);
                        materialCopies.Add(copy);
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }

            var anchor = FindModelPart(model.transform, "RewardDisplayAnchor");
            if (anchor)
            {
                rankLabel = CreatePrizeText(anchor, "Rank", .13f);
                prizeLabel = CreatePrizeText(anchor, "Prize", -.075f);
            }

            target = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                hideFlags = HideFlags.HideAndDontSave,
                name = "Reward Chest UI"
            };
            target.Create();

            var cameraObject = new GameObject("Reward Chest UI Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.56f, 1.1f);
            cameraObject.transform.LookAt(origin + new Vector3(0f, 0.20f, 0f));
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 0.43f;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 5f;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.targetTexture = target;
            previewCamera.allowHDR = false;

            AddLight("Key", Quaternion.Euler(30f, -35f, 0f), 2.4f);
            AddLight("Fill", Quaternion.Euler(40f, 140f, 0f), 1.2f);
            output.texture = target;
            startTime = Time.realtimeSinceStartup;
        }

        private void AddLight(string label, Quaternion rotation, float intensity)
        {
            var child = new GameObject(label);
            child.hideFlags = HideFlags.HideAndDontSave;
            child.transform.SetParent(rig.transform, false);
            child.transform.rotation = rotation;
            var light = child.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.cullingMask = 1 << PreviewLayer;
        }

        private static Transform FindModelPart(Transform root, string name)
        {
            foreach (var part in root.GetComponentsInChildren<Transform>(true))
                if (part.name == name)
                    return part;
            return null;
        }

        private static TextMeshPro CreatePrizeText(Transform anchor, string name, float y)
        {
            var go = new GameObject("Runtime " + name, typeof(RectTransform), typeof(TextMeshPro));
            go.hideFlags = HideFlags.HideAndDontSave;
            go.layer = PreviewLayer;
            go.transform.SetParent(anchor, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(.84f, .23f);
            rect.localPosition = new Vector3(0f, y, .015f);
            rect.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var label = go.GetComponent<TextMeshPro>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 1.6f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = new Color(1f, .91f, .65f, 1f);
            return label;
        }

        private void EnsurePrizeLabels()
        {
            if (ranks == null || ranks.Length != StageCount)
                ranks = new[] { "1st", "2nd", "3rd", "4th - 10th" };
            if (prizes == null || prizes.Length != StageCount)
                prizes = new[] { "100 SKR", "50 SKR", "25 SKR", "15 SKR" };
        }

        private void UpdatePrizeText(int stage)
        {
            EnsurePrizeLabels();
            string neon = new[] { "#FFD34D", "#7EE9FF", "#FFAB57", "#8DDCFF" }[stage];
            ColorUtility.TryParseHtmlString(neon, out var neonColor);
            var textColor = Color.Lerp(neonColor, Color.white, .28f);
            if (rankLabel)
            {
                rankLabel.text = ranks[stage];
                rankLabel.color = textColor;
                rankLabel.ForceMeshUpdate();
            }
            if (prizeLabel)
            {
                prizeLabel.text = prizes[stage];
                prizeLabel.color = textColor;
                prizeLabel.ForceMeshUpdate();
            }
        }

        private void SetStagePalette(int stage)
        {
            if (stage == currentStage)
                return;
            currentStage = stage;
            UpdatePrizeText(stage);
            foreach (var material in materialCopies)
            {
                string name = material.name.Replace(" (Instance)", "");
                string color = name switch
                {
                    "Chest_Paint" => new[] { "#95600B", "#788DA6", "#8F2412", "#101621" }[stage],
                    "Chest_Trim" => new[] { "#FFC122", "#D5E3EE", "#F3691D", "#424D5E" }[stage],
                    "Chest_Gem" => new[] { "#FFD135", "#70DDFF", "#FF8128", "#04C7FF" }[stage],
                    "Chest_Gem_Highlight" => new[] { "#FFF2A9", "#E0FAFF", "#FFCF94", "#B4F0FF" }[stage],
                    "Chest_Coins" => new[] { "#FFC321", "#CCD8E2", "#F47C26", "#FFC321" }[stage],
                    "Chest_Neon" => new[] { "#FFD34D", "#7EE9FF", "#FFAB57", "#8DDCFF" }[stage],
                    _ => null
                };
                if (color == null || !ColorUtility.TryParseHtmlString(color, out var value))
                    continue;
                if (material.HasProperty("baseColorFactor"))
                    material.SetColor("baseColorFactor", value);
                if (name == "Chest_Neon" && material.HasProperty("emissiveFactor"))
                    material.SetColor("emissiveFactor", value * 0.6f);
            }
        }

        public void RenderAt(float elapsed)
        {
            if (!rig || !model || !previewCamera)
                return;

            for (int i = 0; i < animatedParts.Count; i++)
            {
                if (!animatedParts[i])
                    continue;
                animatedParts[i].localPosition = originalPositions[i];
                animatedParts[i].localRotation = originalRotations[i];
                animatedParts[i].localScale = originalScales[i];
            }

            float stageTime = ArriveTime + OpenTime + HoldTime + BurstTime + GapTime;
            float sequenceTime = Mathf.Repeat(Mathf.Max(0f, elapsed), stageTime * StageCount);
            int stage = Mathf.Min(StageCount - 1, Mathf.FloorToInt(sequenceTime / stageTime));
            float phase = sequenceTime - stage * stageTime;
            SetStagePalette(stage);
            bool showText = phase >= ArriveTime + .15f && phase < ArriveTime + OpenTime + HoldTime + BurstTime;
            if (rankLabel && rankLabel.gameObject.activeSelf != showText)
                rankLabel.gameObject.SetActive(showText);
            if (prizeLabel && prizeLabel.gameObject.activeSelf != showText)
                prizeLabel.gameObject.SetActive(showText);
            model.transform.localScale = Vector3.one;
            model.transform.localRotation = Quaternion.identity;

            if (phase < ArriveTime)
            {
                float progress = Mathf.Clamp01(phase / ArriveTime);
                float easeOut = 1f - Mathf.Pow(1f - progress, 3f);
                float turn = progress * progress * (3f - 2f * progress);
                model.transform.localScale = Vector3.one * (0.15f + 0.85f * easeOut);
                model.transform.localRotation = Quaternion.Euler(0f, 360f * turn, 0f);
            }
            else if (phase < ArriveTime + OpenTime)
                openClip.SampleAnimation(model, Mathf.Min(openClip.length, phase - ArriveTime));
            else if (phase < ArriveTime + OpenTime + HoldTime)
                openClip.SampleAnimation(model, openClip.length);
            else
            {
                openClip.SampleAnimation(model, openClip.length);
                float burstElapsed = phase - ArriveTime - OpenTime - HoldTime;
                burstClip.SampleAnimation(model, Mathf.Min(burstClip.length, Mathf.Max(0f, burstElapsed)));
            }

            previewCamera.Render();
        }

        private void TearDown()
        {
            if (output && output.texture == target)
                output.texture = null;
            if (rig)
                DestroyPreviewObject(rig);
            if (target)
            {
                target.Release();
                DestroyPreviewObject(target);
            }
            foreach (var material in materialCopies)
                if (material)
                    DestroyPreviewObject(material);
            materialCopies.Clear();
            currentStage = -1;
            animatedParts.Clear();
            originalPositions.Clear();
            originalRotations.Clear();
            originalScales.Clear();
            rig = null;
            model = null;
            previewCamera = null;
            target = null;
            rankLabel = null;
            prizeLabel = null;
        }

        private static void DestroyPreviewObject(Object item)
        {
            if (Application.isPlaying)
                Destroy(item);
            else
                DestroyImmediate(item);
        }
    }
}
