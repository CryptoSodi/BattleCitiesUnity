using System;
using System.Reflection;
using BattleCities.Core;
using BattleCities.UI;
using TMPro;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class BattleEndTransitionChecks
    {
        static int checks;
        static void Check(bool ok,string name){checks++;if(!ok)throw new Exception("END TRANSITION: "+name);}
        public static void Run()
        {
            checks=0;
            var host=new GameObject("End transition checks");host.SetActive(false);
            var game=host.AddComponent<BattleGame>();
            var cameraHost=new GameObject("Transition camera");var camera=cameraHost.AddComponent<Camera>();
            camera.transform.position=new Vector3(0,20,-10);camera.transform.LookAt(Vector3.zero);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var type=typeof(BattleGame);
            type.GetField("gameCamera",flags).SetValue(game,camera);
            var update=type.GetMethod("UpdateResults",flags);
            var started=type.GetField("endTransitionStarted",flags);
            var simulation=type.GetProperty("Simulation");
            try
            {
                foreach(bool won in new[]{false,true})
                {
                    var state=new BattleSimulation(new MapData());simulation.SetValue(game,state);
                    update.Invoke(game,null);
                    BattleResultsChecks.EndMatch(game,won);
                    int tick=state.Tick;
                    Check((bool)update.Invoke(game,null)&&!game.ResultsVisible,"end message precedes results "+won);
                    var transition=host.GetComponentInChildren<BattleEndTransition>(true);
                    var label=transition.GetComponentInChildren<TextMeshProUGUI>(true);
                    Check(label.text==(won?"STAGE CLEAR":"GAME OVER"),"correct outcome text "+won);
                    Check(camera.enabled,"battle camera stays enabled during message");
                    transition.Show(new Vector2(.2f,.1f),won);
                    Vector2 origin=label.rectTransform.anchoredPosition;
                    Check(!transition.Tick(.4f),"travel does not open results");
                    Vector2 middle=label.rectTransform.anchoredPosition;
                    Check(!transition.Tick(.8f),"arrival starts two-second hold");
                    Vector2 center=label.rectTransform.anchoredPosition;
                    Check(Vector2.Distance(middle,center)<Vector2.Distance(origin,center),"message approaches center with eased motion");
                    Check(!transition.Tick(2.799f)&&transition.Tick(2.8f),"full two-second hold after arrival");
                    started.SetValue(game,Time.unscaledTimeAsDouble-2.75);
                    update.Invoke(game,null);
                    Check(!game.ResultsVisible&&camera.enabled,"results stay hidden until hold completes");
                    started.SetValue(game,Time.unscaledTimeAsDouble-2.81);
                    update.Invoke(game,null);
                    Check(game.ResultsVisible&&!camera.enabled&&!transition.gameObject.activeSelf,"results replace transition and camera after hold");
                    Check(state.Tick==tick,"transition does not advance simulation time");
                    simulation.SetValue(game,new BattleSimulation(new MapData()));update.Invoke(game,null);
                    Check(!game.ResultsVisible&&camera.enabled&&!transition.gameObject.activeSelf,"next stage clears transition and restores camera");
                }
                Check(host.GetComponentsInChildren<BattleEndTransition>(true).Length==1,"transition is reused across stages");
                Debug.Log("Battle end transition checks passed: "+checks);
            }
            finally
            {
                var screen=type.GetField("resultsScreen",flags).GetValue(game) as BattleResultsScreen;
                if(screen)UnityEngine.Object.DestroyImmediate(screen.gameObject);
                UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(cameraHost);
            }
        }
    }
}
