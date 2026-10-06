using System;
using System.Collections;
using System.Linq;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class LoadoutChecks
    {
        public static string Status {get;private set;}="Not started";
        public static void Run()=>new SocialFixturePump().Run(Test);
        static Button Proceed(PreBattleScreen screen)=>screen.Root.GetComponentsInChildren<Button>(true).First(button=>button.name=="Continue");
        static IEnumerator Wait(PreBattleScreen screen){for(int i=0;i<100&&(screen.IsBusy||screen.IsInventoryLoading);i++)yield return null;for(int i=0;i<8;i++)yield return null;if(screen.IsBusy||screen.IsInventoryLoading)throw new Exception("Loadout fixture timed out.");}
        static IEnumerator Test(SocialFixturePump pump)
        {
            Status="Running";int checks=0,launched=0;
            void Check(bool okay,string label){if(!okay)throw new Exception("Loadout: "+label);checks++;}
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();menu.StartBattle();var screen=menu.GetComponent<PreBattleScreen>();var api=menu.GetComponent<MainMenuApiClient>();
            var previousRequest=api.EditorRequestOverride;var previousStart=screen.EditorStartRoutineOverride;var previousLaunch=screen.EditorLaunchOverride;
            var input=UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();bool enabled=input&&input.enabled;if(input)input.enabled=false;
            var fixture=new Fixture();api.EditorRequestOverride=fixture.Request;screen.EditorStartRoutineOverride=pump.Start;screen.EditorLaunchOverride=()=>launched++;
            try
            {
                screen.EditorResetLoadoutFixture();screen.Open();yield return Wait(screen);var proceed=Proceed(screen);proceed.onClick.Invoke();
                Check(screen.IsLoadout&&launched==0,"Continue opens Loadout without launching");
                Check(screen.InventoryScroll.content.childCount==8,"all inventory powers represented");
                string[] inventoryOrder={"shield","base-defence","freeze","speed","upgrade","zoom-out","wipeout","extra-life"};for(int i=0;i<inventoryOrder.Length;i++)Check(screen.InventoryScroll.content.GetChild(i).name==inventoryOrder[i],"inventory row order "+i);
                Check(screen.Root.Find("Loadout/Workspace/Slot 1/Item name").GetComponent<TMP_Text>().text=="SLOT 1","empty slot displays its numbered slot caption");
                screen.ChooseLoadoutSlot(0);screen.EquipLoadoutPower("shield");Check(screen.EquippedPower(0)=="shield","owned Shield equips selected slot");
                screen.ChooseLoadoutSlot(1);screen.EquipLoadoutPower("shield");Check(screen.EquippedPower(1)==null,"duplicate power rejected");screen.EquipLoadoutPower("freeze");
                screen.ChooseLoadoutSlot(2);screen.EquipLoadoutPower("upgrade");Check(screen.EquippedPower(2)==null,"unowned power rejected");screen.EquipLoadoutPower("extra-life");Check(screen.EquippedPower(2)==null,"unsupported active Extra Life rejected");
                screen.ChooseLoadoutSlot(0);screen.Root.Find("Fuel summary/Clear slot").GetComponent<Button>().onClick.Invoke();Check(screen.EquippedPower(0)==null,"footer Clear Slot removes selected power");screen.EquipLoadoutPower("shield");
                screen.Back();Check(!screen.IsLoadout&&screen.SelectedTankTier==0,"Back returns to same selected tank");Check(!screen.Root.Find("Fuel summary/Clear slot").gameObject.activeSelf,"Clear Slot hidden on Select Tank");proceed.onClick.Invoke();Check(screen.IsLoadout&&screen.EquippedPower(0)=="shield"&&screen.EquippedPower(1)=="freeze","Continue preserves draft after Back");
                foreach(var v in new[]{(MainMenuPlatform.Web,1583,924,"web"),(MainMenuPlatform.AndroidLandscape,844,390,"seeker"),(MainMenuPlatform.Psg1,1240,1080,"psg1")})
                {
                    menu.ApplyLayout(v.Item1,new Vector2(v.Item2,v.Item3));Canvas.ForceUpdateCanvases();screen.ChooseLoadoutSlot(3);
                    var inventoryPanel=(RectTransform)screen.Root.Find("Loadout/Inventory");var workspace=(RectTransform)screen.Root.Find("Loadout/Workspace");Check(inventoryPanel.anchoredPosition.x<workspace.anchoredPosition.x,v.Item4+" inventory on left, loadout on right");
                    var start=(RectTransform)proceed.transform;Check(start.parent.name=="Fuel summary"&&Mathf.Approximately(start.anchorMin.x,.765f)&&Mathf.Approximately(start.anchorMax.x,.985f),v.Item4+" Start in shared white footer action slot");
                    var preview=(RectTransform)workspace.Find("Tank preview");var tankBounds=(RectTransform)preview.Find("Selected tank bounds");var pedestal=(RectTransform)preview.Find("Deployment platform");Check(tankBounds.rect.width<=pedestal.rect.width*.73f,v.Item4+" tank smaller than platform");
                    foreach(var label in screen.Root.GetComponentsInChildren<TMP_Text>().Where(t=>t.transform.IsChildOf(screen.Root.Find("Loadout"))||t.transform.IsChildOf(screen.Root.Find("Fuel summary")))){label.ForceMeshUpdate();Check(!label.isTextOverflowing,v.Item4+" text fits "+label.name);}
                    foreach(var button in screen.Root.GetComponentsInChildren<Button>().Where(b=>b.IsInteractable()))Check(button.navigation.mode==Navigation.Mode.Explicit,v.Item4+" navigation "+button.name);
                    var last=screen.InventoryScroll.content.Find("extra-life") as RectTransform;if(EventSystem.current)EventSystem.current.SetSelectedGameObject(last.gameObject);screen.InventoryScroll.Reveal(last);Canvas.ForceUpdateCanvases();
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(screen.InventoryScroll.viewport,last);Check(bounds.min.y>=screen.InventoryScroll.viewport.rect.yMin-1&&bounds.max.y<=screen.InventoryScroll.viewport.rect.yMax+1,v.Item4+" full inventory row reveal");
                    screen.InventoryScroll.verticalNormalizedPosition=1;MainMenuChecks.Capture(v.Item1,v.Item2,v.Item3,"loadout-"+v.Item4);
                }
                fixture.FailSave=true;proceed.onClick.Invoke();proceed.onClick.Invoke();yield return Wait(screen);Check(launched==0&&fixture.Saves==1&&screen.LoadoutStatus.Contains("NOT SAVED"),"failed save retains screen and prevents duplicate writes");
                fixture.FailSave=false;proceed.onClick.Invoke();yield return Wait(screen);Check(launched==1&&fixture.Saves==2&&BattlePreparation.TankTier==0,"successful save launches selected tank once");proceed.onClick.Invoke();Check(launched==1,"repeat Start cannot launch twice");
                Check(fixture.OnlyLoadoutWritten&&fixture.Fuel==7,"loadout saving does not spend Fuel or inventory");
                screen.EditorResetLoadoutFixture();fixture.Anonymous=true;screen.Open();yield return Wait(screen);proceed.onClick.Invoke();proceed.onClick.Invoke();Check(launched==2,"anonymous empty loadout remains playable");
                screen.EditorResetLoadoutFixture();fixture.Anonymous=false;fixture.FailRead=true;screen.Open();yield return Wait(screen);proceed.onClick.Invoke();yield return Wait(screen);Check(screen.LoadoutStatus.Contains("UNAVAILABLE"),"failed inventory is distinct from valid empty");
                fixture.FailRead=false;screen.Back();proceed.onClick.Invoke();yield return Wait(screen);screen.ChooseLoadoutSlot(0);screen.EquipLoadoutPower("shield");Check(screen.EquippedPower(0)=="shield","reopening Loadout recovers failed inventory without Refresh control");
                screen.ClearLoadoutSlot();screen.ChooseLoadoutSlot(1);screen.ClearLoadoutSlot();proceed.onClick.Invoke();screen.Close(false);yield return Wait(screen);Check(launched==2,"closing cancels a pending launch response");
                Status="PASS: "+checks+" Loadout checks";Debug.Log(Status);
            }
            finally
            {
                screen.Close(false);api.EditorRequestOverride=previousRequest;screen.EditorStartRoutineOverride=previousStart;screen.EditorLaunchOverride=previousLaunch;
                screen.EditorResetLoadoutFixture();screen.Open();Proceed(screen).onClick.Invoke();menu.RefreshLayout();if(input)input.enabled=enabled;
                if(Status=="Running")Status="FAILED; see fixture error";
            }
        }
        sealed class Fixture
        {
            public bool FailSave,FailRead,Anonymous,OnlyLoadoutWritten=true;public int Saves,Fuel=7;JObject loadout=new JObject();
            JObject Account()=>new JObject{["fuelBalance"]=Fuel,["inventory"]=new JObject{["shield"]=2,["base-defence"]=1,["freeze"]=3,["extra-life"]=1,["upgrade"]=0,["zoom-out"]=1,["wipeout"]=1,["speed"]=1},["loadout"]=loadout.DeepClone()};
            public IEnumerator Request(string method,string path,JObject payload,Action<long,JObject,string> reply)
            {
                yield return null;yield return null;
                if(path!="/api/economy/account"){reply(404,new JObject(),"Unsupported fixture");yield break;}
                if(Anonymous){reply(401,new JObject{["authenticated"]=false},null);yield break;}
                if(method=="GET"&&FailRead){reply(503,new JObject(),"Offline fixture");yield break;}
                if(method=="PUT")
                {Saves++;OnlyLoadoutWritten&=payload?["account"] is JObject data&&data.Properties().Count()==1&&data["loadout"] is JObject;if(FailSave){reply(503,new JObject(),"Save rejected");yield break;}loadout=(JObject)payload["account"]["loadout"].DeepClone();}
                reply(200,new JObject{["authenticated"]=true,["account"]=Account()},null);
            }
        }
    }
}
