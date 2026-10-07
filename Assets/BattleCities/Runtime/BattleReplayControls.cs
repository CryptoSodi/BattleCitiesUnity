using BattleCities.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BattleCities
{
    public sealed class BattleReplayControls : MonoBehaviour
    {
        private BattleGame game;
        private GameObject canvas;
        private RectTransform bar;
        private TMP_Text status;
        private UnityEngine.UI.Button library,pause,restart,speed,exit;
        private readonly ArcadeTextStyles styles=new ArcadeTextStyles();
        public void Initialize(BattleGame owner)
        {
            if(game)return;game=owner;
            canvas=ReplayUi.Canvas("Replay controls",90);canvas.transform.SetParent(transform,false);
            var art=Resources.Load<PreBattleArt>("PreBattleArt");
            bar=ReplayUi.Panel("Controls",canvas.transform,art.statusPanel);TvStatusFooterLayout.ApplySkin(bar.GetComponent<UnityEngine.UI.Image>(),art.statusPanel);
            ReplayUi.Fit(bar,new Rect(.13f,.88f,.74f,.085f));
            status=ReplayUi.Label("Status",bar,"",styles,ArcadeTextTreatment.PrizeAmount);ReplayUi.Fit(status.rectTransform,new Rect(.015f,.05f,.32f,.9f));
            pause=ReplayUi.Button("Pause",bar,"PAUSE",()=>game.Paused=!game.Paused,styles,art);
            restart=ReplayUi.Button("Restart",bar,"RESTART",()=>game.RestartReplay(),styles,art);
            speed=ReplayUi.Button("Speed",bar,"1x",()=>game.ReplaySpeed=game.ReplaySpeed>=4?.5f:game.ReplaySpeed*2,styles,art);
            exit=ReplayUi.Button("Exit",bar,"MENU",()=>SceneManager.LoadScene("MainMenu"),styles,art);
            ReplayUi.Fit((RectTransform)pause.transform,new Rect(.35f,.15f,.15f,.7f));ReplayUi.Fit((RectTransform)restart.transform,new Rect(.51f,.15f,.17f,.7f));ReplayUi.Fit((RectTransform)speed.transform,new Rect(.69f,.15f,.10f,.7f));ReplayUi.Fit((RectTransform)exit.transform,new Rect(.80f,.15f,.18f,.7f));
            library=ReplayUi.Button("Replays",canvas.transform,"REPLAYS",()=>ReplayBrowser.Open(game),styles,art);
            ReplayUi.Fit((RectTransform)library.transform,new Rect(.84f,.79f,.14f,.065f));
            Psg1UiNavigation.Rows(new UnityEngine.UI.Selectable[]{pause,restart,speed,exit});
        }
        private void Update()
        {
            if(!game)return;
            bool playback=game.IsReplaying;
            bar.gameObject.SetActive((playback||!game.ReplayReady)&&!ReplayBrowser.IsOpen);
            foreach(var control in new[]{pause,restart,speed,exit})control.gameObject.SetActive(playback);
            library.gameObject.SetActive(!game.IsOnline&&!ReplayBrowser.IsOpen&&(playback||game.Paused||game.Simulation.Won||game.Simulation.Lost));
            if(!game.ReplayReady){status.text="Preparing replay session...";return;}
            if(!playback)return;
            status.text=game.ReplayStatus+"\n"+game.Simulation.Tick/60+" / "+game.ReplayPlayback.Data.durationTicks/60+"s";
            pause.GetComponentInChildren<TMP_Text>().text=game.Paused?"RESUME":"PAUSE";
            speed.GetComponentInChildren<TMP_Text>().text=game.ReplaySpeed.ToString("0.#")+"x";
            if(ReplayBrowser.IsOpen)return;
            if(Keyboard.current?.pKey.wasPressedThisFrame==true||Gamepad.current?.startButton.wasPressedThisFrame==true)game.Paused=!game.Paused;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)ReplayBrowser.Open(game);
            Psg1UiNavigation.KeepFocus(bar,pause);
        }
        private void OnDestroy(){styles.Dispose();if(canvas)Destroy(canvas);}
    }
}
