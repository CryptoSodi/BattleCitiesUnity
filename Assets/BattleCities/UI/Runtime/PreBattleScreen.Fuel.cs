using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class PreBattleScreen
    {
        string fuelReceiptKey;
        int launchStage=1;
        public void OpenRestart(int tier,int stage)
        {
            Open();selected=Mathf.Clamp(tier,0,3);launchStage=Mathf.Clamp(stage,1,35);
            if(pendingFuel!=null)selected=(int)pendingFuel["tankTier"];
            ShowPage(true);Focus(proceed);
        }
        bool WalletNeedsAccount => api && (api.IsWalletAuthenticated ||
            (!string.IsNullOrEmpty(api.LastPlayer?.provider) && api.LastPlayer.provider != "guest"));

        void RestoreFuelReceipt()
        {
            var owner=(string)account?["playerId"];
            if(string.IsNullOrEmpty(owner))return;
            string key=BattleFuelReceipt.Key(api.BaseUrl,owner);
            if(key==fuelReceiptKey)return;
            fuelReceiptKey=key;pendingFuel=null;paidTier=-1;
            if(EditorFixtureActive||!PlayerPrefs.HasKey(key))return;
            try
            {
                var receipt=JObject.Parse(PlayerPrefs.GetString(key));
                if(Guid.TryParseExact((string)receipt["requestId"],"D",out _) &&
                    receipt["tankTier"]?.Type==JTokenType.Integer && (int)receipt["tankTier"]>=0 && (int)receipt["tankTier"]<=3)
                {pendingFuel=receipt;selected=(int)receipt["tankTier"];}
            }
            catch(Exception){ /* An invalid local receipt cannot authorize a launch. */ }
        }

        void ClearFuelReceipt()
        {
            pendingFuel=null;paidTier=-1;
            if(!EditorFixtureActive&&!string.IsNullOrEmpty(fuelReceiptKey))
            {PlayerPrefs.DeleteKey(fuelReceiptKey);PlayerPrefs.Save();}
        }

        void LaunchLoadoutBattle()
        {
            if(launchRequested||busy)return;
            if(inventoryUnavailable || (account==null&&WalletNeedsAccount))
            {status.text="ACCOUNT UNAVAILABLE • REOPEN LOADOUT TO RETRY";UpdateView();return;}
            // Offline guests have no server balance. Connected accounts always use the server receipt.
            if(account==null){LaunchConfirmedBattle();return;}
            RestoreFuelReceipt();
            if(string.IsNullOrEmpty(fuelReceiptKey))
            {status.text="ACCOUNT UNAVAILABLE • REOPEN LOADOUT TO RETRY";UpdateView();return;}
            if(pendingFuel==null)
            {
                pendingFuel=new JObject{["requestId"]=Guid.NewGuid().ToString("D"),["tankTier"]=selected,["expectedPlayerId"]=(string)account["playerId"]};
                // Persist before dispatch: a timeout/restart must retry the same debit, not create another.
                if(!EditorFixtureActive){PlayerPrefs.SetString(fuelReceiptKey,pendingFuel.ToString(Newtonsoft.Json.Formatting.None));PlayerPrefs.Save();}
            }
            pendingFuel["expectedPlayerId"]=(string)account["playerId"];
            StartLoadoutRoutine(ConsumeFuelAndLaunch());
        }

        IEnumerator ConsumeFuelAndLaunch()
        {
            int generation=loadoutGeneration,tier=(int)pendingFuel["tankTier"];
            string owner=(string)account["playerId"],requestId=(string)pendingFuel["requestId"];
            busy=true;status.text="CONFIRMING FUEL...";UpdateView();bool confirmed=false,insufficient=false;
            yield return api.Request("POST","/api/economy/fuel/consume",(JObject)pendingFuel.DeepClone(),(code,body,error)=>
            {
                if(generation!=loadoutGeneration||!IsLoadout)return;
                bool sameReceipt=(string)body?["requestId"]==requestId;
                var returned=body?["account"] as JObject;
                bool validAccount=returned!=null&&ValidLoadoutAccount(returned)&&(string)returned["playerId"]==owner&&
                    returned["fuelBalance"]?.Type==JTokenType.Integer&&(long)returned["fuelBalance"]>=0;
                confirmed=code>=200&&code<300&&sameReceipt&&YesLoadout(body?["ok"])&&
                    body?["fuelConsumed"]?.Type==JTokenType.Integer&&(int)body["fuelConsumed"]==tier+1&&validAccount;
                insufficient=code==409&&sameReceipt&&(string)body?["error"]=="Insufficient fuel"&&validAccount;
                if(confirmed||insufficient)account=returned;
            });
            if(generation!=loadoutGeneration||!IsLoadout)yield break;
            busy=false;
            if(confirmed){selected=tier;LaunchConfirmedBattle();}
            else
            {
                if(insufficient)ClearFuelReceipt();
                status.text=insufficient?"NOT ENOUGH FUEL • VISIT SHOP TO REFUEL":"FUEL NOT CONFIRMED • RETRY START BATTLE";
                UpdateView();
            }
        }

        void LaunchConfirmedBattle()
        {
            if(launchRequested)return;
            launchRequested=true;
            BattlePreparation.Set(selected,api.BaseUrl,(string)account?["playerId"]??api.LastPlayer?.id,
                (string)account?["provider"]??api.LastPlayer?.provider,(string)pendingFuel?["requestId"],stage:launchStage);
            status.text="DEPLOYING...";UpdateView();
#if UNITY_EDITOR
            if(EditorLaunchOverride!=null){EditorLaunchOverride();return;}
#endif
            launch?.Invoke();
        }
    }
}
