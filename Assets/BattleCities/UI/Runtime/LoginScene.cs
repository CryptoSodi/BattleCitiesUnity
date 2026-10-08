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
        private bool loading;
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
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(
                layout && layout.UsesPsg1 && psgPhantomButton ? psgPhantomButton.gameObject : phantomButton.gameObject);
        }
        private void OnDisable()
        {
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
            if (loading || pendingWallet || !apiClient) return;
            pendingWallet = true; SetButtons(false); apiClient.ConnectWallet();
        }
        public void ContinueAsGuest()
        {
            if (!GuestLoginAllowed || loading || pendingWallet || !apiClient) return;
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
            if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            { pendingWallet = false; SetButtons(true); SetStatus("MainMenu is missing from Build Settings."); return; }
            loading = true; SetStatus("Entering Battle Cities...");
            SceneManager.LoadSceneAsync(mainMenuScene);
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
            SetButtonAvailable(guestButton, allowed && !loading && !pendingWallet);
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
