using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        RankingScreen rankingScreen;
        public bool IsRankingOpen
        {get{var screen=mainFrame?mainFrame.Find("Ranking screen"):null;return screen&&screen.gameObject.activeSelf;}}
        void EnsureRanking()
        {
            if(!rankingScreen)rankingScreen=GetComponent<RankingScreen>();
            if(!rankingScreen)rankingScreen=gameObject.AddComponent<RankingScreen>();
            if(!rankingScreen.IsConfigured)rankingScreen.Configure(this,theme,apiClient,mainFrame);
        }
        public void OpenRanking()
        {
            CloseSettingsForNavigation();
            EnsureApiClient();
            CloseOperationsForNavigation();
            if(IsShopOpen){EnsureShop();shop.Close(false);}
            var tank=mainFrame.Find("Pre-battle screens");if(tank)tank.gameObject.SetActive(false);
            EnsureRanking();rankingScreen.Open();
        }
        void LayoutRankingScreen()
        {
            var screen=mainFrame?mainFrame.Find("Ranking screen") as RectTransform:null;if(!screen)return;
            FitPreBattleScreen(screen);
            if(!rankingScreen)rankingScreen=GetComponent<RankingScreen>();
            if(rankingScreen&&rankingScreen.IsConfigured)rankingScreen.ApplyLayout(lastPlatform);
        }
        void CloseRankingForNavigation()
        {if(IsRankingOpen){EnsureRanking();rankingScreen.Close(false);}}
    }
}
