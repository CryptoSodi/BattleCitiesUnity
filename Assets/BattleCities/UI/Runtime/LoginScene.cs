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
        private bool contentRetry;
        private CanvasGroup loginVisibility;
        private GameObject startupView;
        private UnityEngine.UI.Text startupStatus;
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
            try { await ScreenUpdates.EnsureReady(value=>{if(this)SetStatus(value);},destroyCancellationToken); }
            catch(System.OperationCanceledException){return;}
            if(!this||!isActiveAndEnabled)return;
            var screenRoot=layout?layout.transform as RectTransform:phantomButton?phantomButton.GetComponentInParent<Canvas>().transform as RectTransform:null;
            ScreenContent.BindAuthored(screenRoot,"login");
            RemoteScreenView.Bind("login",screenRoot);
            if(apiClient)StartCoroutine(RestoreSignIn());else{restoringSession=false;SetButtons(true);ShowSignIn();}
        }
        private System.Collections.IEnumerator RestoreSignIn()
        {
            restoringSession = true; SetButtons(false);
            yield return apiClient.RestoreWalletSession(_ => { });
            restoringSession = false;
            if (loading || contentRetry) yield break;
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
        }
        private void ShowSignIn()
        {
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
            if (loading || pendingWallet || restoringSession || !apiClient) return;
            if (contentRetry) { EnterGame(); return; }
            pendingWallet = true; SetButtons(false); apiClient.ConnectWallet();
        }
        public void ContinueAsGuest()
        {
            if (!GuestLoginAllowed || loading || pendingWallet || restoringSession || !apiClient) return;
            SetButtons(false); apiClient.ContinueAsGuest();
        }
        private void OnPlayerLoaded(MainMenuApiClient.PlayerSnapshot player)
        {
            if (loading || player == null) return;
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
                await ScreenUpdates.EnsureReady(value=>{if(this)SetStatus(value);},destroyCancellationToken);
                await RemoteGameArt.EnsureReady(value => { if (this) SetStatus(value); }, destroyCancellationToken);
                if (!this || !isActiveAndEnabled) return;
                contentRetry = false;
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
                if (this) { contentRetry = true; SetButtons(true); ShowSignIn(); SetStatus("Game content unavailable. Select Connect Wallet to retry the download."); }
            }
            finally { if (this) loading = false; }
        }
        private void OnStatusChanged(string title, string detail)
        {
            if (loading) return;
            if (title != null && (title.Contains("FAILED") || title.Contains("UNAVAILABLE")))
            { pendingWallet = false; SetButtons(true); }
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
