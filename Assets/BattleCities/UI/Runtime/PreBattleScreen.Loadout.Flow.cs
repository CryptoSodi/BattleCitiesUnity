using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class PreBattleScreen
    {
        bool accountLoading,inventoryUnavailable,loadoutDirty,launchRequested;
        int loadoutGeneration;
        string loadoutIdentity;
        Coroutine inventoryRoutine;
#if UNITY_EDITOR
        public Action<IEnumerator> EditorStartRoutineOverride;
        public Action EditorLaunchOverride;
        public void EditorResetLoadoutFixture(){loadoutIdentity=null;BindLoadoutIdentity();UpdateView();}
#endif
        Coroutine StartLoadoutRoutine(IEnumerator routine)
        {
#if UNITY_EDITOR
            if(EditorStartRoutineOverride!=null){EditorStartRoutineOverride(routine);return null;}
#endif
            return StartCoroutine(routine);
        }
        void BindLoadoutIdentity()
        {
            string identity=api?api.BaseUrl+"|"+(api.LastPlayer?.walletAddress??api.LastPlayer?.id??"anonymous"):null;
            if(identity==loadoutIdentity)return;
            loadoutIdentity=identity;loadoutGeneration++;if(inventoryRoutine!=null)StopCoroutine(inventoryRoutine);inventoryRoutine=null;
            fuelReceiptKey=null;pendingFuel=null;paidTier=-1;
            account=null;loadout=new JObject();loadoutDirty=accountLoading=inventoryUnavailable=busy=launchRequested=false;activeSlot=0;
        }
        void OnLoadoutPlayer(MainMenuApiClient.PlayerSnapshot player)
        {string previous=loadoutIdentity;BindLoadoutIdentity();if(previous!=loadoutIdentity&&IsOpen){RefreshLoadoutAccount();UpdateView();}}
        bool UsesLocalGuestLoadout => api && !api.IsAuthenticated && !WalletNeedsAccount &&
            (api.IsLocalGuest || api.LastPlayer?.provider == "guest");
        void PrepareLocalGuestLoadout()
        {
            loadoutGeneration++;if(inventoryRoutine!=null)StopCoroutine(inventoryRoutine);inventoryRoutine=null;
            account=null;loadout=new JObject();loadoutDirty=accountLoading=inventoryUnavailable=false;
            // Forget only in-memory wallet state; keep any persisted paid receipt for its owner.
            fuelReceiptKey=null;pendingFuel=null;paidTier=-1;
            status.text=inLoadout?"EMPTY LOADOUT IS READY • START WHEN YOU ARE READY":"";
        }
        public void RefreshLoadoutAccount()
        {
            if(!api||busy)return;
            BindLoadoutIdentity();
            if(UsesLocalGuestLoadout){PrepareLocalGuestLoadout();UpdateView();return;}
            loadoutGeneration++;if(inventoryRoutine!=null)StopCoroutine(inventoryRoutine);
            accountLoading=true;inventoryUnavailable=false;status.text="LOADING INVENTORY...";UpdateView();
            inventoryRoutine=StartLoadoutRoutine(LoadAccount());
        }
        int OwnedPowerCount(string item)
        {
            var inventory=account?["inventory"] as JObject;var token=inventory?[item];
            if(token?.Type!=JTokenType.Integer)return 0;
            return int.TryParse(token.ToString(),out int count)?Math.Max(0,count):0;
        }
        bool AccountMatchesPlayer(JObject data)=>string.IsNullOrEmpty(api.LastPlayer?.id)||(string)data?["playerId"]==api.LastPlayer.id;
        static bool ValidLoadoutAccount(JObject data)
        {
            if(!(data?["inventory"] is JObject inventory))return false;
            foreach(var property in inventory.Properties())if(Array.IndexOf(Items,property.Name)>=0&&
                (property.Value.Type!=JTokenType.Integer||!int.TryParse(property.Value.ToString(),out int count)||count<0))return false;
            return data["loadout"]==null||data["loadout"].Type==JTokenType.Null||data["loadout"] is JObject;
        }
        JObject OwnedDraft(JObject source)
        {
            var result=new JObject();var used=new HashSet<string>();
            for(int i=0;i<4;i++)
            {
                var token=source?[Slots[i]]??(i==3?source?["passive"]:null);
                string item=token?.Type==JTokenType.String?token.Value<string>():null;
                if(item!="extra-life"&&Array.IndexOf(Items,item)>=0&&OwnedPowerCount(item)>0&&used.Add(item))result[Slots[i]]=item;
            }
            return result;
        }
        public void ChooseLoadoutSlot(int index)
        {
            if(busy||!inLoadout||index<0||index>3)return;
            activeSlot=index;status.text="SLOT 0"+(index+1)+" • SELECT AN OWNED POWER FROM INVENTORY";RefreshLoadoutView();LoadoutNavigation();
            int first=Array.FindIndex(Items,item=>item!="extra-life"&&OwnedPowerCount(item)>0);
            if(first>=0)Focus(loadoutRows[first]);
        }
        public void EquipLoadoutPower(string item)
        {
            if(busy||accountLoading||!inLoadout)return;
            if(account==null||inventoryUnavailable){status.text=inventoryUnavailable?"INVENTORY UNAVAILABLE • REOPEN LOADOUT TO RETRY":"CONNECT YOUR ACCOUNT TO EQUIP OWNED POWERS";return;}
            if(item=="extra-life"){status.text="EXTRA LIVES CANNOT BE EQUIPPED IN ACTIVE SLOTS";return;}
            if(Array.IndexOf(Items,item)<0||OwnedPowerCount(item)<=0){status.text="POWER NOT OWNED • VISIT SHOP TO RESTOCK";return;}
            for(int i=0;i<4;i++)if(i!=activeSlot&&EquippedPower(i)==item){status.text=PowerName(item)+" IS ALREADY IN SLOT 0"+(i+1);return;}
            loadout[Slots[activeSlot]]=item;loadoutDirty=true;status.text=PowerName(item)+" EQUIPPED IN SLOT 0"+(activeSlot+1);UpdateView();
        }
        public void ClearLoadoutSlot()
        {
            if(busy||!inLoadout)return;
            if(loadout.Remove(Slots[activeSlot]))loadoutDirty=true;
            status.text="SLOT 0"+(activeSlot+1)+" CLEARED";UpdateView();
            if(UnityEngine.EventSystems.EventSystem.current&&UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==clearSlot.gameObject)Focus(slotButtons[activeSlot]);
        }
        void CancelLoadoutRequests()
        {loadoutGeneration++;StopAllCoroutines();inventoryRoutine=null;accountLoading=busy=launchRequested=false;}
        public void Close(bool restoreFocus=true)
        {
            CancelLoadoutRequests();
            if(root)root.gameObject.SetActive(false);var menu=GetComponent<MainMenuScene>();menu?.SetHeroVisible(true);menu?.SetTankSelectorBackdrop(false);if(restoreFocus)Focus(home);
        }
        void LoadoutNavigation()
        {
            if(!loadoutWorkspace)return;
            back.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnDown=slotButtons[1],selectOnUp=proceed,selectOnLeft=clearSlot.interactable?clearSlot:loadoutRows[0],selectOnRight=back};
            for(int i=0;i<4;i++)slotButtons[i].navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnUp=i<2?back:slotButtons[i-2],selectOnDown=i<2?slotButtons[i+2]:i==2&&clearSlot.interactable?clearSlot:proceed,
                selectOnLeft=i%2==0?loadoutRows[Math.Min(i*2,7)]:slotButtons[i-1],selectOnRight=i%2==0?slotButtons[i+1]:slotButtons[i]};
            for(int i=0;i<8;i++)loadoutRows[i].navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnUp=i>0?loadoutRows[i-1]:back,selectOnDown=i<7?loadoutRows[i+1]:clearSlot.interactable?clearSlot:proceed,
                selectOnLeft=back,selectOnRight=slotButtons[activeSlot]};
            clearSlot.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=slotButtons[2],selectOnDown=back,selectOnLeft=loadoutRows[7],selectOnRight=proceed};
            proceed.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=slotButtons[2],selectOnDown=back,selectOnLeft=clearSlot.interactable?clearSlot:loadoutRows[7],selectOnRight=slotButtons[3]};
        }
    }
}
