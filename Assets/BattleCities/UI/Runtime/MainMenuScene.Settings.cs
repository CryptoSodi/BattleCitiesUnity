using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        SettingsScreen settingsScreen;
        [SerializeField] string settingsReturnScreen;
        [SerializeField] int settingsNavigationIndex;
        GameObject settingsReturnFocus;
        public bool IsSettingsOpen {get{var s=mainFrame?mainFrame.Find("Settings screen"):null;return s&&s.gameObject.activeSelf;}}
        void EnsureSettings()
        {
            if(!settingsScreen)settingsScreen=GetComponent<SettingsScreen>();
            if(!settingsScreen)settingsScreen=gameObject.AddComponent<SettingsScreen>();
            if(!settingsScreen.IsConfigured)settingsScreen.Configure(this,theme,apiClient,mainFrame);
        }
        public void OpenSettings()
        {
            if(IsPlayerProfileOpen&&profileReturnScreen=="Settings screen"){ClosePlayerProfile();return;}
            if(IsSettingsOpen)return;
            settingsReturnFocus=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            EnsureApiClient();settingsReturnScreen=null;
            settingsNavigationIndex=IsPlayerProfileOpen?profileNavigationIndex:IsOperationsOpen?(IsSocialsOpen?4:3):IsRankingOpen?2:IsShopOpen?1:0;
            foreach(var name in new[]{"Player profile screen","Shop screen","Ranking screen","Operations screen","Pre-battle screens"})
            {
                var other=mainFrame.Find(name);if(other&&other.gameObject.activeSelf){settingsReturnScreen=name;other.gameObject.SetActive(false);}
            }
            EnsureSettings();settingsScreen.Open();
        }
        public void CloseSettings(bool restore=true)
        {
            if(!IsSettingsOpen)return;
            EnsureSettings();settingsScreen.Close();
            if(restore&&!string.IsNullOrEmpty(settingsReturnScreen))
            {
                var other=mainFrame.Find(settingsReturnScreen);if(other)other.gameObject.SetActive(true);
            }
            settingsReturnScreen=null;SetTankSelectorBackdrop(IsTvScreenOpen);RefreshLayout();
            if(restore&&EventSystem.current){
                var control=settingsReturnFocus?settingsReturnFocus.GetComponent<UnityEngine.UI.Selectable>():null;
                if(Psg1UiNavigation.Available(control))EventSystem.current.SetSelectedGameObject(settingsReturnFocus);
                else if(Psg1UiNavigation.Available(ActiveSettingsButton))EventSystem.current.SetSelectedGameObject(ActiveSettingsButton.gameObject);
            }
            settingsReturnFocus=null;
        }
        void CloseSettingsForNavigation(){if(IsPlayerProfileOpen)ClosePlayerProfile(false);if(IsSettingsOpen)CloseSettings(false);}
        void LayoutSettingsScreen()
        {
            var screen=mainFrame?mainFrame.Find("Settings screen") as RectTransform:null;if(!screen)return;
            FitPreBattleScreen(screen);if(!settingsScreen)settingsScreen=GetComponent<SettingsScreen>();
            if(settingsScreen&&settingsScreen.IsConfigured)settingsScreen.ApplyLayout(lastPlatform);
        }
    }
}
