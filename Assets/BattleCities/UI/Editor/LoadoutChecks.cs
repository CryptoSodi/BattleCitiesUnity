using System;
using System.Collections;
using System.Collections.Generic;
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
        static void CheckFuelReceiptPersistence(Action<bool,string> check)
        {
            string apiUrl="https://loadout-"+Guid.NewGuid().ToString("N")+".invalid",owner="loadout-fixture",requestId=Guid.NewGuid().ToString("D");
            string key=BattleFuelReceipt.Key(apiUrl,owner);
            string receipt=new JObject{["requestId"]=requestId,["tankTier"]=0,["expectedPlayerId"]=owner}.ToString();
            try
            {
                check(key!=BattleFuelReceipt.Key(apiUrl,"other-fixture")&&key!=BattleFuelReceipt.Key(apiUrl+"/other",owner),"pending Fuel storage is isolated by account and API");
                PlayerPrefs.SetString(key,receipt);PlayerPrefs.Save();
                BattleFuelReceipt.MarkStarted(apiUrl,owner,Guid.NewGuid().ToString("D"));
                check(PlayerPrefs.GetString(key)==receipt,"starting an unrelated receipt leaves the paid attempt recoverable");
                BattleFuelReceipt.MarkStarted(apiUrl,"other-fixture",requestId);
                check(PlayerPrefs.GetString(key)==receipt,"another account cannot clear the paid attempt");
                BattleFuelReceipt.MarkStarted(apiUrl,owner,requestId);
                check(!PlayerPrefs.HasKey(key),"starting the matching paid attempt clears its persisted receipt");
            }
            finally {PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
        }
        static IEnumerator Test(SocialFixturePump pump)
        {
            Status="Running";int checks=0,launched=0;
            void Check(bool okay,string label){if(!okay)throw new Exception("Loadout: "+label);checks++;}
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();menu.StartBattle();var screen=menu.GetComponent<PreBattleScreen>();var api=menu.GetComponent<MainMenuApiClient>();
            var previousRequest=api.EditorRequestOverride;var previousStart=screen.EditorStartRoutineOverride;var previousLaunch=screen.EditorLaunchOverride;
            var previousPlayer=api.LastPlayer;bool previousWallet=api.IsWalletAuthenticated;
            void SetFixturePlayer(bool wallet)
            {
                typeof(MainMenuApiClient).GetProperty("LastPlayer").SetValue(api,new MainMenuApiClient.PlayerSnapshot{id="loadout-fixture",provider=wallet?"wallet":"guest",walletAddress=wallet?"loadout-fixture-wallet":null});
                typeof(MainMenuApiClient).GetProperty("IsWalletAuthenticated").SetValue(api,wallet);
            }
            var input=UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();bool enabled=input&&input.enabled;if(input)input.enabled=false;
            var fixture=new Fixture();api.EditorRequestOverride=fixture.Request;screen.EditorStartRoutineOverride=pump.Start;screen.EditorLaunchOverride=()=>launched++;
            try
            {
                SetFixturePlayer(true);
                screen.EditorResetLoadoutFixture();screen.Open();yield return Wait(screen);var proceed=Proceed(screen);proceed.onClick.Invoke();
                IEnumerator StartCase(Fixture next)
                {
                    screen.Close(false);fixture=next;api.EditorRequestOverride=fixture.Request;SetFixturePlayer(true);
                    screen.EditorResetLoadoutFixture();screen.Open();yield return Wait(screen);proceed.onClick.Invoke();yield return Wait(screen);
                }
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
                Check(fixture.Fuel==7&&fixture.FuelRequests==0,"opening Loadout and failed save never consume Fuel");
                fixture.FailSave=false;proceed.onClick.Invoke();yield return Wait(screen);Check(launched==1&&fixture.Saves==2&&BattlePreparation.TankTier==0,"successful save launches selected tank once");proceed.onClick.Invoke();Check(launched==1,"repeat Start cannot launch twice");
                Check(fixture.OnlyLoadoutWritten&&fixture.Fuel==6&&fixture.FuelDebits==1,"loadout save writes only loadout; confirmed Start separately consumes exactly one Fuel");
                SetFixturePlayer(false);screen.EditorResetLoadoutFixture();fixture.Anonymous=true;screen.Open();yield return Wait(screen);proceed.onClick.Invoke();proceed.onClick.Invoke();Check(launched==2&&fixture.FuelRequests==1,"anonymous empty loadout remains playable without a server Fuel request");
                SetFixturePlayer(true);screen.EditorResetLoadoutFixture();fixture.Anonymous=false;fixture.FailRead=true;screen.Open();yield return Wait(screen);proceed.onClick.Invoke();yield return Wait(screen);Check(screen.LoadoutStatus.Contains("UNAVAILABLE"),"failed inventory is distinct from valid empty");
                fixture.FailRead=false;screen.Back();proceed.onClick.Invoke();yield return Wait(screen);screen.ChooseLoadoutSlot(0);screen.EquipLoadoutPower("shield");Check(screen.EquippedPower(0)=="shield","reopening Loadout recovers failed inventory without Refresh control");
                screen.ClearLoadoutSlot();screen.ChooseLoadoutSlot(1);screen.ClearLoadoutSlot();proceed.onClick.Invoke();screen.Close(false);yield return Wait(screen);Check(launched==2,"closing cancels a pending launch response");

                yield return StartCase(new Fixture{LoseNextFuelReply=true});
                int launchBefore=launched;
                proceed.onClick.Invoke();proceed.onClick.Invoke();yield return Wait(screen);
                Check(launched==launchBefore&&fixture.Fuel==6&&fixture.FuelDebits==1&&fixture.FuelRequests==1&&screen.LoadoutStatus.Contains("NOT CONFIRMED"),"lost Fuel receipt retains the screen after exactly one server debit");
                string lostRequest=fixture.FuelRequestIds[0];
                proceed.onClick.Invoke();proceed.onClick.Invoke();yield return Wait(screen);proceed.onClick.Invoke();
                Check(launched==launchBefore+1&&fixture.Fuel==6&&fixture.FuelDebits==1&&fixture.FuelRequests==2&&fixture.FuelRequestIds[1]==lostRequest,"retry reuses receipt ID without double charge or duplicate launch");

                yield return StartCase(new Fixture{Fuel=0});launchBefore=launched;
                proceed.onClick.Invoke();yield return Wait(screen);
                Check(launched==launchBefore&&fixture.FuelDebits==0&&fixture.Fuel==0&&screen.LoadoutStatus.Contains("NOT ENOUGH FUEL"),"insufficient Fuel blocks launch without changing balance");
                string rejectedRequest=fixture.FuelRequestIds[0];fixture.Fuel=7;
                proceed.onClick.Invoke();yield return Wait(screen);
                Check(launched==launchBefore+1&&fixture.Fuel==6&&fixture.FuelDebits==1&&fixture.FuelRequestIds[1]!=rejectedRequest,"definitive insufficient result permits a new Start after refuelling");

                yield return StartCase(new Fixture{HoldFuelReply=true});launchBefore=launched;
                proceed.onClick.Invoke();
                for(int frame=0;frame<20&&fixture.FuelDebits==0;frame++)yield return null;
                Check(fixture.FuelDebits==1&&screen.IsBusy,"close fixture waits until server has debited Fuel before its response");
                string closedRequest=fixture.FuelRequestIds[0];screen.Close(false);fixture.HoldFuelReply=false;yield return Wait(screen);
                Check(launched==launchBefore,"late receipt after Close cannot launch a battle");
                screen.Open();yield return Wait(screen);proceed.onClick.Invoke();proceed.onClick.Invoke();yield return Wait(screen);
                Check(launched==launchBefore+1&&fixture.Fuel==6&&fixture.FuelDebits==1&&fixture.FuelRequests==2&&fixture.FuelRequestIds[1]==closedRequest,"reopening resolves the same pending charge and launches once");

                yield return StartCase(new Fixture{Anonymous=true});launchBefore=launched;
                proceed.onClick.Invoke();yield return Wait(screen);
                Check(launched==launchBefore&&fixture.FuelRequests==0&&screen.LoadoutStatus.Contains("ACCOUNT UNAVAILABLE"),"wallet with expired account session cannot fall back to unpaid guest play");
                yield return StartCase(new Fixture{FailRead=true});launchBefore=launched;
                proceed.onClick.Invoke();yield return Wait(screen);
                Check(launched==launchBefore&&fixture.FuelRequests==0&&screen.LoadoutStatus.Contains("UNAVAILABLE"),"unavailable wallet account with empty loadout cannot launch unpaid");
                CheckFuelReceiptPersistence(Check);
                Status="PASS: "+checks+" Loadout checks";Debug.Log(Status);
            }
            finally
            {
                screen.Close(false);api.EditorRequestOverride=previousRequest;screen.EditorStartRoutineOverride=previousStart;screen.EditorLaunchOverride=previousLaunch;
                typeof(MainMenuApiClient).GetProperty("LastPlayer").SetValue(api,previousPlayer);typeof(MainMenuApiClient).GetProperty("IsWalletAuthenticated").SetValue(api,previousWallet);
                screen.EditorResetLoadoutFixture();screen.Open();Proceed(screen).onClick.Invoke();menu.RefreshLayout();if(input)input.enabled=enabled;
                if(Status=="Running")Status="FAILED; see fixture error";
            }
        }
        sealed class Fixture
        {
            public bool FailSave,FailRead,Anonymous,LoseNextFuelReply,HoldFuelReply,OnlyLoadoutWritten=true;
            public int Saves,Fuel=7,FuelRequests,FuelDebits;
            public readonly List<string> FuelRequestIds=new List<string>();
            readonly Dictionary<string,int> consumed=new Dictionary<string,int>();
            JObject loadout=new JObject();
            JObject Account()=>new JObject{["playerId"]="loadout-fixture",["provider"]="wallet",["fuelBalance"]=Fuel,["inventory"]=new JObject{["shield"]=2,["base-defence"]=1,["freeze"]=3,["extra-life"]=1,["upgrade"]=0,["zoom-out"]=1,["wipeout"]=1,["speed"]=1},["loadout"]=loadout.DeepClone()};
            public IEnumerator Request(string method,string path,JObject payload,Action<long,JObject,string> reply)
            {
                yield return null;yield return null;
                if(path=="/api/economy/fuel/consume")
                {
                    FuelRequests++;
                    string requestId=(string)payload?["requestId"];
                    if(method!="POST"||!Guid.TryParseExact(requestId,"D",out _)||payload?["tankTier"]?.Type!=JTokenType.Integer||payload.Properties().Count()!=3||(string)payload["expectedPlayerId"]!="loadout-fixture")
                        throw new Exception("Fuel fixture received an invalid debit request.");
                    int tier=(int)payload["tankTier"];if(tier<0||tier>3)throw new Exception("Fuel fixture received invalid tank tier.");
                    FuelRequestIds.Add(requestId);
                    if(Anonymous){reply(401,new JObject{["authenticated"]=false},"Not signed in");yield break;}
                    bool idempotent=consumed.TryGetValue(requestId,out int originalTier);
                    if(idempotent&&originalTier!=tier)throw new Exception("A pending Fuel request changed tank tier.");
                    if(!idempotent&&Fuel<tier+1){reply(409,new JObject{["ok"]=false,["requestId"]=requestId,["fuelConsumed"]=0,["error"]="Insufficient fuel",["account"]=Account()},"Insufficient fuel");yield break;}
                    if(!idempotent){Fuel-=tier+1;FuelDebits++;consumed.Add(requestId,tier);}
                    while(HoldFuelReply)yield return null;
                    if(LoseNextFuelReply){LoseNextFuelReply=false;reply(0,null,"Fixture connection lost after debit");yield break;}
                    reply(200,new JObject{["ok"]=true,["requestId"]=requestId,["fuelConsumed"]=tier+1,["idempotent"]=idempotent,["account"]=Account()},null);yield break;
                }
                if(path!="/api/economy/account"){reply(404,new JObject(),"Unsupported fixture");yield break;}
                if(Anonymous){reply(401,new JObject{["authenticated"]=false},null);yield break;}
                if(method=="GET"&&FailRead){reply(503,new JObject(),"Offline fixture");yield break;}
                if(method=="PUT")
                {Saves++;OnlyLoadoutWritten&=payload?["account"] is JObject data&&data.Properties().Count()==1&&data["loadout"] is JObject&&payload.Properties().Count()==2&&(string)payload["expectedPlayerId"]=="loadout-fixture";if(FailSave){reply(503,new JObject(),"Save rejected");yield break;}loadout=(JObject)payload["account"]["loadout"].DeepClone();}
                reply(200,new JObject{["authenticated"]=true,["account"]=Account()},null);
            }
        }
    }
}
