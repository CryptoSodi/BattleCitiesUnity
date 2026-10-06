using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        OperationsScreen operations;
        public bool IsOperationsOpen { get { var screen=mainFrame?mainFrame.Find("Operations screen"):null;return screen&&screen.gameObject.activeSelf; } }
        public bool IsSocialsOpen=>IsOperationsOpen&&operations&&operations.IsSocials;
        void EnsureOperations()
        {
            if(!operations)operations=GetComponent<OperationsScreen>();
            if(!operations)operations=gameObject.AddComponent<OperationsScreen>();
            if(!operations.IsConfigured)operations.Configure(this,theme,apiClient,mainFrame);
        }
        void OpenOperations(bool socials)
        {
            CloseSettingsForNavigation();
            EnsureApiClient();CloseRankingForNavigation();
            if(IsShopOpen){EnsureShop();shop.Close(false);}
            var tank=mainFrame.Find("Pre-battle screens");if(tank)tank.gameObject.SetActive(false);
            EnsureOperations();operations.Open(socials);
        }
        void CloseOperationsForNavigation(){if(IsOperationsOpen){EnsureOperations();operations.Close(false);}}
        void LayoutOperationsScreen()
        {
            var screen=mainFrame?mainFrame.Find("Operations screen") as RectTransform:null;if(!screen)return;
            FitPreBattleScreen(screen);if(!operations)operations=GetComponent<OperationsScreen>();
            if(operations&&operations.IsConfigured)operations.ApplyLayout(lastPlatform);
        }
    }
}
