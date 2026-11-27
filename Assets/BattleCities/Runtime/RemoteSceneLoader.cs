using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities
{
    // Android OTA builds keep the scene shell local; its authored roots live in game-art.
    [UnityEngine.Scripting.Preserve]
    public sealed class RemoteSceneLoader : MonoBehaviour
    {
        [SerializeField] string sceneName;
        [SerializeField] Sprite startupLogo;
        [SerializeField] Font startupFont;
        Text status, percentage;
        Image progress;
        Sprite croppedStartupLogo;

#if UNITY_EDITOR
        public void Configure(string name, Sprite logo, Font font)
        { sceneName = name; startupLogo = logo; startupFont = font; }
#endif
        async void Awake()
        {
            if (string.IsNullOrWhiteSpace(sceneName)) { Debug.LogError("Remote scene name is missing."); return; }
            if (sceneName == "Login") CreateStartup();
            int retrySeconds = 3;
            while (this && isActiveAndEnabled)
            {
                try
                {
                    if (sceneName == "Login")
                        await UI.ScreenUpdates.EnsureReady(SetStatus, destroyCancellationToken);
                    await RemoteGameArt.EnsureReady(SetStatus, destroyCancellationToken, SetProgress);
                    if (!this) return;
                    var prefab = RemoteGameArt.Load<GameObject>("RemoteScene_" + sceneName);
                    if (!prefab) throw new System.IO.InvalidDataException("Downloaded scene is incomplete: " + sceneName);
                    SetProgress(1);
                    // Login shows 100% for a frame. Later scenes must be instantiated
                    // synchronously during Awake before sceneLoaded observers run.
                    if (sceneName == "Login") await System.Threading.Tasks.Task.Yield();
                    if (!this) return;
                    var created = Instantiate(prefab, transform, false);
                    created.transform.SetParent(null, true);
                    Destroy(gameObject);
                    return;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception error)
                {
                    Debug.LogWarning("Startup game content unavailable: " + error.Message);
                    if (!this || !isActiveAndEnabled) return;
                    bool connection = error is RemoteGameArt.DownloadException download && download.IsTransient;
                    SetStatus(connection ? $"Connecting to game content. Retrying in {retrySeconds}s..."
                        : error is System.IO.InvalidDataException ? $"Game content verification failed. Retrying in {retrySeconds}s..."
                        : $"Unable to load game content. Retrying in {retrySeconds}s...");
                    try { await System.Threading.Tasks.Task.Delay(retrySeconds * 1000, destroyCancellationToken); }
                    catch (OperationCanceledException) { return; }
                    retrySeconds = Mathf.Min(30, retrySeconds * 2);
                }
            }
        }

        void CreateStartup()
        {
            var root = new GameObject("Session Startup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = short.MaxValue;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var background = NewImage(root.transform, "Background", new Vector2(0, 0), new Vector2(1, 1));
            background.color = new Color32(6, 29, 54, 255);
            if (startupLogo)
            {
                var logo = NewImage(root.transform, "Battle Cities Logo", new Vector2(.5f, .58f), new Vector2(.5f, .58f));
                logo.rectTransform.sizeDelta = new Vector2(320, 320);
                // The splash image includes transparent padding for Unity's built-in
                // splash. Crop its artwork here so the content screen uses the same
                // visible logo without packing a second image into the APK.
                var texture = startupLogo.texture;
                var region = new Rect(texture.width * (689f / 2508f), texture.height * (690f / 2508f),
                    texture.width * (1131f / 2508f), texture.height * (1091f / 2508f));
                croppedStartupLogo = Sprite.Create(texture, region, new Vector2(.5f, .5f));
                logo.sprite = croppedStartupLogo; logo.preserveAspect = true;
            }
            status = NewText(root.transform, "Loading Status", new Vector2(.1f, .25f), new Vector2(.9f, .36f), 36);
            status.resizeTextForBestFit = true; status.resizeTextMinSize = 20; status.resizeTextMaxSize = 36;
            status.text = "Loading Battle Cities...";
            var track = NewImage(root.transform, "Loading Progress Track", new Vector2(.2f, .2f), new Vector2(.8f, .2f));
            track.rectTransform.sizeDelta = new Vector2(0, 24); track.color = new Color32(36, 64, 86, 255);
            progress = NewImage(track.transform, "Loading Progress Fill", Vector2.zero, new Vector2(0, 1));
            progress.color = new Color32(255, 196, 30, 255);
            percentage = NewText(root.transform, "Loading Percentage", new Vector2(.2f, .12f), new Vector2(.8f, .18f), 30);
            SetProgress(0);
        }
        Image NewImage(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image)); item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform; rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = item.GetComponent<Image>(); image.raycastTarget = false; return image;
        }
        Text NewText(Transform parent, string name, Vector2 min, Vector2 max, int size)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text)); item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform; rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = item.GetComponent<Text>(); text.font = startupFont; text.fontSize = size;
            text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
        void SetStatus(string value) { if (status) status.text = value; }
        void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (progress) { progress.rectTransform.anchorMax = new Vector2(value, 1); progress.enabled = value > 0; }
            if (percentage) percentage.text = $"{Mathf.FloorToInt(value * 100)}%";
        }
        void OnDestroy() { if (croppedStartupLogo) Destroy(croppedStartupLogo); }
    }
}
