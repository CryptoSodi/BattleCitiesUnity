using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        PlayerProfileScreen profileScreen;
        [SerializeField] string profileReturnScreen;
        [SerializeField] int profileNavigationIndex;
        [SerializeField] GameObject profileReturnFocus;
        public bool IsPlayerProfileOpen {get{var p=mainFrame?mainFrame.Find("Player profile screen"):null;return p&&p.gameObject.activeSelf;}}
        void EnsurePlayerProfile()
        {
            if(!profileScreen)profileScreen=GetComponent<PlayerProfileScreen>();
            if(!profileScreen)profileScreen=gameObject.AddComponent<PlayerProfileScreen>();
            if(!profileScreen.IsConfigured)profileScreen.Configure(this,theme,apiClient,mainFrame);
        }
        public void OpenOwnProfile()=>OpenPlayerProfile();
        public void OpenPlayerProfile(string playerId=null)
        {
            EnsureApiClient();
            if(!IsPlayerProfileOpen)
            {
                profileReturnScreen=null;profileReturnFocus=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
                profileNavigationIndex=IsSettingsOpen?settingsNavigationIndex:IsOperationsOpen?(IsSocialsOpen?4:3):IsRankingOpen?2:IsShopOpen?1:0;
                foreach(var name in new[]{"Settings screen","Shop screen","Ranking screen","Operations screen","Pre-battle screens"})
                {var other=mainFrame.Find(name);if(other&&other.gameObject.activeSelf){profileReturnScreen=name;other.gameObject.SetActive(false);}}
            }
            EnsurePlayerProfile();profileScreen.Open(playerId);
        }
        public void ClosePlayerProfile(bool restore=true)
        {
            if(!IsPlayerProfileOpen)return;
            EnsurePlayerProfile();profileScreen.Close();
            if(restore&&!string.IsNullOrEmpty(profileReturnScreen)){var other=mainFrame.Find(profileReturnScreen);if(other)other.gameObject.SetActive(true);}
            profileReturnScreen=null;SetTankSelectorBackdrop(IsTvScreenOpen);RefreshLayout();
            if(restore&&EventSystem.current)EventSystem.current.SetSelectedGameObject(profileReturnFocus&&profileReturnFocus.activeInHierarchy?profileReturnFocus:walletLoginButton?walletLoginButton.gameObject:startButton.gameObject);
            profileReturnFocus=null;
        }
        void LayoutPlayerProfile()
        {
            var p=mainFrame?mainFrame.Find("Player profile screen") as RectTransform:null;if(!p)return;
            FitPreBattleScreen(p);if(!profileScreen)profileScreen=GetComponent<PlayerProfileScreen>();
            if(profileScreen&&profileScreen.IsConfigured)profileScreen.ApplyLayout(lastPlatform);
        }
    }
}
