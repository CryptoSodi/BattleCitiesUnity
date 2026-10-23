using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed class LoginScene : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private MainMenuApiClient apiClient;
        [Header("Controls")]
        [SerializeField] private Button phantomButton;
        [SerializeField] private Button guestButton;
        [SerializeField] private Button storeButton;
        [SerializeField] private Text statusText;
        [Header("PSG1 controls (separate layout, same login flow)")]
        [SerializeField] private Button psgPhantomButton;
        [SerializeField] private Button psgGuestButton;
        [SerializeField] private Button psgStoreButton;
        [SerializeField] private Text psgStatusText;
        [SerializeField] private LoginLayout layout;
        [SerializeField] private MenuTheme walletButtonTheme;
        [SerializeField] private Sprite mobileWalletButtonSkin;
        [SerializeField] private Sprite mobileWalletIconSource;
        [Header("Published links (leave empty until available)")]
        [SerializeField] private string dappStoreUrl;
        private bool pendingWallet;
        private bool restoringSession;
        private bool loading;
        private bool contentReady;
        private CanvasGroup loginVisibility;
        private GameObject startupView;
        private UnityEngine.UI.Text startupStatus;
        private Image startupProgress;
        private Text startupPercentage;
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void BattleCitiesLoadingFullscreenVisible(int visible);
#endif
        static void ShowLoadingFullscreen(bool visible)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BattleCitiesLoadingFullscreenVisible(visible?1:0);
#endif
        }
        private Psg1UiInput controllerInput;
        private bool? previousGuestAccess;
        private bool showingDefaultStatus;
        public bool GuestLoginAllowed => !RuntimePlatformInfo.IsAndroid && (!layout || layout.AllowsGuestLogin);
        public const string WalletOnlyStatus = "Wallet sign-in enables verified play.";

        public void Configure(MainMenuApiClient client, Button phantom, Button guest, Button store, Text status, MenuTheme theme = null)
        { apiClient = client; phantomButton = phantom; guestButton = guest; storeButton = store; statusText = status; walletButtonTheme = theme; }
        public void ConfigurePsg1(LoginLayout view, Button phantom, Button guest, Button store, Text status)
        { layout = view; psgPhantomButton = phantom; psgGuestButton = guest; psgStoreButton = store; psgStatusText = status; }

        public void ConfigureMobileWalletSkin(Sprite skin) => mobileWalletButtonSkin = skin;
        public void ConfigureMobileWalletIcon(Sprite source) => mobileWalletIconSource = source;

        private void Awake()
        {
            if (!apiClient) apiClient = GetComponent<MainMenuApiClient>();
            if (apiClient) { apiClient.ConfigureGuestFallback(false); apiClient.ConfigureAutomaticRefresh(false); }
            // Gate the authored Canvas before the first rendered frame.
            restoringSession = true;
            SetButtons(false);
            CreateStartupView();
        }
        private void OnEnable()
        {
            if (apiClient)
            {
                apiClient.PlayerLoaded += OnPlayerLoaded;
                apiClient.StatusChanged += OnStatusChanged;
                apiClient.enabled = true;
            }
            phantomButton.onClick.AddListener(ConnectWallet);
            guestButton.onClick.AddListener(ContinueAsGuest);
            storeButton.onClick.AddListener(OpenStore);
            if (psgPhantomButton) psgPhantomButton.onClick.AddListener(ConnectWallet);
            if (psgGuestButton) psgGuestButton.onClick.AddListener(ContinueAsGuest);
            if (psgStoreButton) psgStoreButton.onClick.AddListener(OpenStore);
        }
        private void Start()
        {
            ArcadeTextStyles.ApplyWhiteLabels(transform,walletButtonTheme?walletButtonTheme.HeadingFont:null);
            if (RuntimePlatformInfo.IsAndroid)
            {
                ConfigureMobileWalletButton(phantomButton, "CONNECT WALLET");
                ConfigureMobileWalletButton(psgPhantomButton, "CONNECT JUPITER");
            }
            RefreshGuestAccess();
            ShowDefaultStatus();
            PrepareSignIn();
        }
        private async void PrepareSignIn()
        {
            restoringSession=true;SetButtons(false);
            int retrySeconds = 3;
            while (this && isActiveAndEnabled && !contentReady)
            {
                try
                {
                    SetStartupProgress(0);
                    // Finish startup content before restoring a wallet or exposing sign-in.
                    await ScreenUpdates.EnsureReady(value=>{if(this)SetStatus(value);},destroyCancellationToken);
                    await RemoteGameArt.EnsureReady(value=>{if(this)SetStatus(value);},destroyCancellationToken,
                        value=>{if(this)SetStartupProgress(value);});
                    if (!this || !isActiveAndEnabled) return;
                    contentReady = true;
                    SetStartupProgress(1);
                    SetStatus("Loading player...");
                    // Present completion before continuing even when content was already cached.
                    await System.Threading.Tasks.Task.Yield();
                }
                catch(System.OperationCanceledException){return;}
                catch(System.Exception error)
                {
                    Debug.LogWarning("Startup game content unavailable: " + error.Message);
                    if (!this || !isActiveAndEnabled) return;
                    bool connectionRetry = error is RemoteGameArt.DownloadException download && download.IsTransient;
                    SetStatus(connectionRetry
                        ? $"Connecting to game content. Retrying in {retrySeconds}s..."
                        : error is System.IO.InvalidDataException
                            ? $"Game content verification failed. Retrying in {retrySeconds}s..."
                            : $"Unable to load game content. Retrying in {retrySeconds}s...");
                    try { await System.Threading.Tasks.Task.Delay(retrySeconds * 1000, destroyCancellationToken); }
                    catch(System.OperationCanceledException){return;}
                    retrySeconds = Mathf.Min(30, retrySeconds * 2);
                }
            }
            if(!this||!isActiveAndEnabled)return;
            var screenRoot=layout?layout.transform as RectTransform:phantomButton?phantomButton.GetComponentInParent<Canvas>().transform as RectTransform:null;
            ScreenContent.BindAuthored(screenRoot,"login");
            RemoteScreenView.Bind("login",screenRoot);
            ShowDefaultStatus();
            if(apiClient)StartCoroutine(RestoreSignIn());else{restoringSession=false;SetButtons(true);ShowSignIn();}
        }
        private System.Collections.IEnumerator RestoreSignIn()
        {
            restoringSession = true; SetButtons(false);
            yield return apiClient.RestoreWalletSession(_ => { });
            restoringSession = false;
            if (loading) yield break;
            SetButtons(true);
            if (apiClient.LastStatusTitle == "RESTORING SESSION") ShowDefaultStatus();
            ShowSignIn();
        }
        private void CreateStartupView()
        {
            var root = layout ? layout.transform : phantomButton ? phantomButton.GetComponentInParent<Canvas>()?.transform : null;
            if (root)
            {
                loginVisibility = root.GetComponent<CanvasGroup>();
                if (!loginVisibility) loginVisibility = root.gameObject.AddComponent<CanvasGroup>();
                loginVisibility.alpha = 0;
                loginVisibility.interactable = loginVisibility.blocksRaycasts = false;
            }

            startupView = new GameObject("Session Startup", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            startupView.transform.SetParent(transform, false);
            var canvas = startupView.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = startupView.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;

            var background = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(startupView.transform, false);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<UnityEngine.UI.Image>().color = new Color32(6, 29, 54, 255);

            if (walletButtonTheme && walletButtonTheme.Logo)
            {
                var logo = new GameObject("Battle Cities Logo", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                logo.transform.SetParent(startupView.transform, false);
                var logoRect = (RectTransform)logo.transform;
                logoRect.anchorMin = logoRect.anchorMax = new Vector2(.5f, .58f);
                logoRect.sizeDelta = new Vector2(320, 320);
                var image = logo.GetComponent<UnityEngine.UI.Image>();
                image.sprite = walletButtonTheme.Logo; image.preserveAspect = true; image.raycastTarget = false;
            }
            var status = new GameObject("Loading Status", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            status.transform.SetParent(startupView.transform, false);
            var statusRect = (RectTransform)status.transform;
            statusRect.anchorMin = new Vector2(.1f, .25f); statusRect.anchorMax = new Vector2(.9f, .36f);
            statusRect.offsetMin = statusRect.offsetMax = Vector2.zero;
            startupStatus = status.GetComponent<UnityEngine.UI.Text>();
            startupStatus.font = walletButtonTheme ? walletButtonTheme.HeadingFont : statusText ? statusText.font : null;
            startupStatus.text = "Loading Battle Cities...";
            startupStatus.color = Color.white; startupStatus.alignment = TextAnchor.MiddleCenter;
            startupStatus.fontSize = 36; startupStatus.resizeTextForBestFit = true;
            startupStatus.resizeTextMinSize = 20; startupStatus.resizeTextMaxSize = 36;
            startupStatus.raycastTarget = false;
            ArcadeTextStyles.ApplyWhite(startupStatus, startupStatus.font);

            var track = new GameObject("Loading Progress Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(startupView.transform, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(.2f, .2f); trackRect.anchorMax = new Vector2(.8f, .2f);
            trackRect.sizeDelta = new Vector2(0, 24);
            track.GetComponent<Image>().color = new Color32(36, 64, 86, 255);
            track.GetComponent<Image>().raycastTarget = false;
            var fill = new GameObject("Loading Progress Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = new Vector2(0, 1);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            startupProgress = fill.GetComponent<Image>();
            startupProgress.color = new Color32(255, 196, 30, 255); startupProgress.raycastTarget = false;
            var percentage = new GameObject("Loading Percentage", typeof(RectTransform), typeof(Text));
            percentage.transform.SetParent(startupView.transform, false);
            var percentageRect = (RectTransform)percentage.transform;
            percentageRect.anchorMin = new Vector2(.2f, .12f); percentageRect.anchorMax = new Vector2(.8f, .18f);
            percentageRect.offsetMin = percentageRect.offsetMax = Vector2.zero;
            startupPercentage = percentage.GetComponent<Text>();
            startupPercentage.font = startupStatus.font; startupPercentage.fontSize = 30;
            startupPercentage.alignment = TextAnchor.MiddleCenter; startupPercentage.raycastTarget = false;
            ArcadeTextStyles.ApplyWhite(startupPercentage, startupStatus.font);
            SetStartupProgress(0);
            if(RuntimePlatformInfo.IsMobileBrowser&&WebFullscreenButton.Supported){
                // Leave a real touch-sized button below the percentage on short phone viewports.
                var logo=startupView.transform.Find("Battle Cities Logo") as RectTransform;
                if(logo)logo.anchorMin=logo.anchorMax=new Vector2(.5f,.66f);
                statusRect.anchorMin=new Vector2(.1f,.30f);statusRect.anchorMax=new Vector2(.9f,.40f);
                trackRect.anchorMin=new Vector2(.2f,.26f);trackRect.anchorMax=new Vector2(.8f,.26f);
                percentageRect.anchorMin=new Vector2(.2f,.17f);percentageRect.anchorMax=new Vector2(.8f,.23f);
                ShowLoadingFullscreen(true);
            }
        }
        private void SetStartupProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (startupProgress)
            {
                startupProgress.rectTransform.anchorMax = new Vector2(value, 1);
                startupProgress.enabled = value > 0;
            }
            if (startupPercentage) startupPercentage.text = $"{Mathf.FloorToInt(value * 100)}%";
        }
        private void ShowSignIn()
        {
            ShowLoadingFullscreen(false);
            if (loginVisibility)
            {
                loginVisibility.alpha = 1;
                loginVisibility.interactable = loginVisibility.blocksRaycasts = true;
            }
            if (startupView) { startupView.SetActive(false); Destroy(startupView); }
            startupStatus = null;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(
                layout && layout.UsesPsg1 && psgPhantomButton ? psgPhantomButton.gameObject : phantomButton.gameObject);
        }
        private void OnDisable()
        {
            ShowLoadingFullscreen(false);
            StopAllCoroutines(); restoringSession = false;
            if (startupView) { startupView.SetActive(false); Destroy(startupView); }
            controllerInput?.Dispose(); controllerInput = null;
            if (apiClient) { apiClient.PlayerLoaded -= OnPlayerLoaded; apiClient.StatusChanged -= OnStatusChanged; }
            if (apiClient) apiClient.CancelWalletLogin();
            pendingWallet = false;
            if (phantomButton) phantomButton.onClick.RemoveListener(ConnectWallet);
            if (guestButton) guestButton.onClick.RemoveListener(ContinueAsGuest);
            if (storeButton) storeButton.onClick.RemoveListener(OpenStore);
            if (psgPhantomButton) psgPhantomButton.onClick.RemoveListener(ConnectWallet);
            if (psgGuestButton) psgGuestButton.onClick.RemoveListener(ContinueAsGuest);
            if (psgStoreButton) psgStoreButton.onClick.RemoveListener(OpenStore);
        }
        private void LateUpdate()
        {
            RefreshGuestAccess();
            if (restoringSession || loading) return;
            if (!layout || !layout.UsesPsg1) { controllerInput?.Dispose(); controllerInput = null; return; }
            if (controllerInput == null || !controllerInput.MatchesCurrent)
            { controllerInput?.Dispose(); controllerInput = new Psg1UiInput(); }
            if (controllerInput.CancelPressed && pendingWallet && apiClient) apiClient.CancelWalletLogin();
            Psg1UiNavigation.Rows(new Selectable[] { psgPhantomButton });
            if (controllerInput.CancelPressed && Psg1UiNavigation.Available(psgPhantomButton))
                EventSystem.current.SetSelectedGameObject(psgPhantomButton.gameObject);
            if (psgPhantomButton) Psg1UiNavigation.KeepFocus(psgPhantomButton.transform.parent, psgPhantomButton);
        }
        // Keep the old entry point for any serialized UnityEvent references.
        public void ConnectPhantom() => ConnectWallet();
        public void ConnectWallet()
        {
            if (!contentReady || loading || pendingWallet || restoringSession || !apiClient) return;
            pendingWallet = true; SetButtons(false); apiClient.ConnectWallet();
        }
        public void ContinueAsGuest()
        {
            if (!contentReady || !GuestLoginAllowed || loading || pendingWallet || restoringSession || !apiClient) return;
            SetButtons(false); apiClient.ContinueAsGuest();
        }
        private void OnPlayerLoaded(MainMenuApiClient.PlayerSnapshot player)
        {
            if (!contentReady || loading || player == null) return;
            bool wallet = player.provider == "wallet";
            if (!wallet && (player.provider != "guest" || !GuestLoginAllowed)) return;
            if (pendingWallet && !wallet) return;
            PlayerPrefs.SetString("battlecities.loginMode", wallet ? "wallet" : "guest");
            PlayerPrefs.SetString("battlecities.playerName", player.displayName ?? "COMMANDER");
            PlayerPrefs.Save();
            EnterGame();
        }
        private async void EnterGame()
        {
            if (loading) return;
            loading = true; pendingWallet = false; SetButtons(false);
            try
            {
                if (!this || !isActiveAndEnabled) return;
                if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
                    throw new System.InvalidOperationException("MainMenu is missing from Build Settings.");
                SetStatus("Entering Battle Cities...");
                var sceneLoad = SceneManager.LoadSceneAsync(mainMenuScene);
                while (sceneLoad != null && !sceneLoad.isDone) await System.Threading.Tasks.Task.Yield();
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception error)
            {
                Debug.LogWarning("Game content unavailable: " + error.Message);
                if (this) { SetButtons(true); ShowSignIn(); SetStatus("Unable to enter Battle Cities. Please try again."); }
            }
            finally { if (this) loading = false; }
        }
        private void OnStatusChanged(string title, string detail)
        {
            if (loading) return;
            if (title != null && (title.Contains("FAILED") || title.Contains("UNAVAILABLE")))
            { pendingWallet = false; if (contentReady && !restoringSession) SetButtons(true); }
            if (!contentReady) return;
            SetStatus(detail);
        }
        private void OpenStore()
        {
            if (System.Uri.TryCreate(dappStoreUrl, System.UriKind.Absolute, out var uri) && uri.Scheme == "https")
                Application.OpenURL(dappStoreUrl);
            else SetStatus("The Battle Cities dApp Store listing is not published yet.");
        }
        private void SetButtons(bool value)
        {
            SetButtonAvailable(phantomButton,value); SetButtonAvailable(guestButton,value && GuestLoginAllowed); SetButtonAvailable(storeButton,value);
            SetButtonAvailable(psgPhantomButton,value); SetButtonAvailable(psgGuestButton,false); SetButtonAvailable(psgStoreButton,value);
        }
        private static void SetButtonAvailable(UnityEngine.UI.Button button,bool value)
        {
            if(!button)return;
            var visual=button.GetComponent<LoginButtonState>();
            if(visual)visual.SetInteractionLocked(!value);
            else button.interactable=value;
        }
        private void RefreshGuestAccess()
        {
            bool allowed = GuestLoginAllowed;
            if (previousGuestAccess == allowed) return;
            previousGuestAccess = allowed;
            SetButtonAvailable(guestButton, allowed && !loading && !pendingWallet && !restoringSession);
            SetButtonAvailable(psgGuestButton, false);
            if (showingDefaultStatus) ShowDefaultStatus();
        }
        public void ShowDefaultStatus()
        {
            showingDefaultStatus = true;
            if (statusText) statusText.text = GuestLoginAllowed
                ? "Guest progress stays on this device. Wallet sign-in enables verified play." : WalletOnlyStatus;
            if (psgStatusText) psgStatusText.text = WalletOnlyStatus;
        }
        private void SetStatus(string value)
        {
            showingDefaultStatus = false;
            if (startupStatus) startupStatus.text = value ?? "";
            if (statusText) statusText.text = value ?? "";
            if (psgStatusText) psgStatusText.text = value ?? "";
        }

        public void ConfigureMobileWalletButton(Button button, string title)
        {
            if (!button || !mobileWalletButtonSkin) return;
            // Keep the original green/gold treatment with separately rendered native labels.
            var art = button.GetComponent<Image>();
            if (art)
            {
                art.sprite = mobileWalletButtonSkin;
                art.type = Image.Type.Simple;
                art.preserveAspect = true;
            }
            var existingLabel = button.transform.Find("Wallet Provider Label");
            var labelObject = existingLabel ? existingLabel.gameObject :
                new GameObject("Wallet Provider Label", typeof(RectTransform), typeof(Text));
            if (!existingLabel) labelObject.transform.SetParent(button.transform, false);
            var hasIcon = mobileWalletIconSource != null;
            if (hasIcon)
            {
                var existingIcon = button.transform.Find("Wallet Provider Icon");
                var iconObject = existingIcon ? existingIcon.gameObject :
                    new GameObject("Wallet Provider Icon", typeof(RectTransform), typeof(LoginWalletIcon));
                if (!existingIcon) iconObject.transform.SetParent(button.transform, false);
                var iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = new Vector2(.11f, .13f);
                iconRect.anchorMax = new Vector2(.27f, .83f);
                iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
                var icon = iconObject.GetComponent<LoginWalletIcon>();
                icon.Configure(mobileWalletIconSource); icon.raycastTarget = false;
            }
            var rect = (RectTransform)labelObject.transform;
            rect.anchorMin = new Vector2(hasIcon ? .31f : 0, 0);
            rect.anchorMax = new Vector2(hasIcon ? .92f : 1, 1);
            rect.offsetMin = new Vector2(hasIcon ? 0 : 20, 10);
            rect.offsetMax = new Vector2(hasIcon ? 0 : -20, -10);
            var label = labelObject.GetComponent<Text>();
            label.font = walletButtonTheme && walletButtonTheme.HeadingFont ? walletButtonTheme.HeadingFont : statusText.font;
            label.text = title; label.color = Color.white; label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter; label.fontSize = 42;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 22; label.resizeTextMaxSize = 42;
            label.raycastTarget = false;
        }
    }
}
