using System;
using System.IO;
using System.Reflection;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class ReferralChecks
    {
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/BattleCities/Scenes/MainMenu.unity",OpenSceneMode.Single);
            var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuScene>();
            if(!menu)throw new Exception("Main menu not found");
            var frame=(RectTransform)menu.Content.Find("Main Display");
            var api=menu.GetComponent<MainMenuApiClient>();if(!api)api=menu.gameObject.AddComponent<MainMenuApiClient>();
            var serialized=new SerializedObject(menu);serialized.FindProperty("apiClient").objectReferenceValue=api;serialized.ApplyModifiedPropertiesWithoutUndo();
            var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>("Assets/BattleCities/UI/Settings/ArcadeMenuTheme.asset");
            var settings=menu.GetComponent<SettingsScreen>();if(!settings)settings=menu.gameObject.AddComponent<SettingsScreen>();
            settings.Configure(menu,theme,api,frame);settings.Root.gameObject.SetActive(true);
            Directory.CreateDirectory("C:/repos/Unity/ui assets/main-menu/previews");
            menu.OpenReferralEntry();
            var screen=menu.GetComponent<ReferralScreen>();
            if(!screen||!screen.IsOpen)throw new Exception("Settings referral dialog did not open");
            var dialog=frame.Find("Referral dialog");
            var input=dialog.Find("Card/Referral link or code").GetComponent<TMP_InputField>();
            var paste=dialog.Find("Card/Paste").GetComponent<Button>();
            var apply=dialog.Find("Card/Primary").GetComponent<Button>();
            var close=dialog.Find("Card/Secondary").GetComponent<Button>();
            if(!input||!close||apply.interactable)throw new Exception("Unauthenticated referral must not be applied");
            if(!paste||input.navigation.selectOnRight!=paste||paste.navigation.selectOnLeft!=input)throw new Exception("Controller cannot navigate between the referral field and Paste");
            if(input.navigation.selectOnDown!=close)throw new Exception("Controller cannot reach Close from the input");
            string savedClipboard=GUIUtility.systemCopyBuffer;
            try
            {
                GUIUtility.systemCopyBuffer=" fedcba543210 ";paste.onClick.Invoke();
                if(input.text!="FEDCBA543210")throw new Exception("Paste must accept a standalone referral code");
                GUIUtility.systemCopyBuffer=" https://battlecities.com/?source=socials&ref=abcdef012345 ";paste.onClick.Invoke();
                if(input.text!="ABCDEF012345")throw new Exception("Paste must insert only the normalized referral code");
                GUIUtility.systemCopyBuffer="https://battlecities.com.example.com/?ref=000000000000";paste.onClick.Invoke();
                if(input.text!="ABCDEF012345")throw new Exception("A foreign link must not overwrite the existing referral code");
                GUIUtility.systemCopyBuffer=new string('X',2049);paste.onClick.Invoke();
                if(input.text!="ABCDEF012345")throw new Exception("Oversized clipboard contents should not replace the field");
            }
            finally{GUIUtility.systemCopyBuffer=savedClipboard;}
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var request=typeof(ReferralScreen).GetField("pasteRequestId",flags);
            var snapshot=typeof(ReferralScreen).GetField("pasteSnapshot",flags);
            request.SetValue(screen,"active");snapshot.SetValue(screen,input.text);
            screen.OnReferralPasteResult("{\"id\":\"stale\",\"ok\":true,\"text\":\"SHOULD NOT APPLY\"}");
            if(input.text!="ABCDEF012345")throw new Exception("Stale paste callback changed the field");
            screen.OnReferralPasteResult("{\"id\":\"active\",\"ok\":true,\"text\":\"https://battlecities.com/?ref=fedcba543210\"}");
            if(input.text!="FEDCBA543210")throw new Exception("Browser Paste must insert only the referral code");
            input.text="";
            var art=RemoteGameArt.Load<PreBattleArt>("PreBattleArt");
            if(apply.image.sprite!=art.tankCostButtonLocked)throw new Exception("Disabled action must use the shop's locked skin");
            var visual=apply.GetComponent<ReferralActionVisual>();
            apply.interactable=true;visual.Refresh();
            if(apply.image.sprite!=art.tankCostButton)throw new Exception("Resting action must use the shop's blue skin");
            visual.OnSelect(null);
            if(apply.image.sprite!=art.tankCostButtonSelected||close.image.sprite!=art.tankCostButton)throw new Exception("Only the focused action should be gold");
            dialog.Find("Card/Status").GetComponent<TMP_Text>().text="";
            foreach(var v in new[]{(MainMenuPlatform.Web,1583,924,"web"),(MainMenuPlatform.AndroidLandscape,844,390,"seeker"),(MainMenuPlatform.Psg1,1240,1080,"psg1")})
            {
                menu.ApplyLayout(v.Item1,new Vector2(v.Item2,v.Item3));Canvas.ForceUpdateCanvases();
                var first=apply.image.rectTransform.rect;var second=close.image.rectTransform.rect;
                if(Mathf.Abs(first.width-second.width)>.1f||Mathf.Abs(first.height-second.height)>.1f)throw new Exception(v.Item4+" referral buttons must match in size");
                var card=(RectTransform)dialog.Find("Card");
                var inputBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(card,input.transform);
                var pasteBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(card,paste.transform);
                if(inputBounds.max.x>=pasteBounds.min.x||Mathf.Abs(inputBounds.center.y-pasteBounds.center.y)>.1f)throw new Exception(v.Item4+" Paste must sit beside the input without overlap");
                var frameImage=input.GetComponent<Image>();float inputCorner=frameImage.sprite.border.x/(frameImage.pixelsPerUnit*frameImage.pixelsPerUnitMultiplier);
                if(inputCorner>frameImage.rectTransform.rect.height*.181f)throw new Exception(v.Item4+" input corners are too large (corner "+inputCorner+", height "+frameImage.rectTransform.rect.height+", multiplier "+frameImage.pixelsPerUnitMultiplier+")");
                foreach(var action in new[]{apply,close,paste})
                {
                    var image=action.image;float corner=image.sprite.border.x/(image.pixelsPerUnit*image.pixelsPerUnitMultiplier);
                    if(corner>image.rectTransform.rect.height*.221f)throw new Exception(v.Item4+" referral action has stretched pill corners");
                }
                foreach(var label in dialog.GetComponentsInChildren<TMP_Text>(true))
                {
                    if(!label.gameObject.activeInHierarchy)continue;
                    label.ForceMeshUpdate();
                    if(label.isTextOverflowing)throw new Exception(v.Item4+" referral text overflow: "+label.name);
                }
                MainMenuChecks.Capture(v.Item1,v.Item2,v.Item3,"referral-entry-"+v.Item4);
            }
            visual.OnDeselect(null);apply.interactable=false;visual.Refresh();
            menu.Back();if(screen.IsOpen||!menu.IsSettingsOpen)throw new Exception("Back must return to Settings");
            menu.OpenReferralShare();
            if(paste.gameObject.activeSelf)throw new Exception("Share link must not show the Paste action");
            typeof(MainMenuApiClient).GetProperty("LastPlayer").SetValue(api,new MainMenuApiClient.PlayerSnapshot{id="ply-check",provider="wallet"});
            typeof(ReferralScreen).GetField("loadedPlayer",flags).SetValue(screen,"ply-check");
            typeof(ReferralScreen).GetField("profile",flags).SetValue(screen,new JObject{
                ["link"]="https://battlecities.com/?ref=ABCDEF012345",["referred"]=false,
                ["rewards"]=new JArray(new JObject{["amount"]="0.001",["currency"]="sol"})});
            typeof(ReferralScreen).GetMethod("SetBusy",flags).Invoke(screen,new object[]{false});
            typeof(ReferralScreen).GetMethod("UpdateProfile",flags).Invoke(screen,null);
            if(!screen.IsOpen||!apply.interactable||input.text!="https://battlecities.com/?ref=ABCDEF012345")throw new Exception("Share link is unavailable");
            if(!dialog.Find("Card/Status").GetComponent<TMP_Text>().text.Contains("0.001 SOL"))throw new Exception("Earned balance is missing");
            string clipboardBeforeCopy=GUIUtility.systemCopyBuffer;
            try{apply.onClick.Invoke();if(GUIUtility.systemCopyBuffer!=input.text)throw new Exception("Copy link did not copy the referral link");}
            finally{GUIUtility.systemCopyBuffer=clipboardBeforeCopy;}
            menu.Back();
            CheckSettingsVisits(menu,api,settings,screen);
            Debug.Log("PASS: referral dialog layout, Settings visit limit and controller focus");
        }

        static void CheckSettingsVisits(MainMenuScene menu,MainMenuApiClient api,SettingsScreen settings,ReferralScreen screen)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var setPlayer=typeof(MainMenuApiClient).GetProperty("LastPlayer");
            var cache=typeof(ReferralScreen).GetMethod("CacheProfile",flags);
            var prefix=(string)typeof(SettingsScreen).GetField("ReferralVisitsKeyPrefix",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue();
            var first=Guid.NewGuid().ToString("N");
            var second=Guid.NewGuid().ToString("N");
            var phone=UnityEngine.Object.FindAnyObjectByType<PhoneControllerHost>();
            var fixturePhone=phone?null:new GameObject("Settings visit phone fixture",typeof(PhoneControllerHost));
            var referral=settings.Root.Find("Rows/Row 2/Referral").GetComponent<Button>();
            try
            {
                menu.CloseSettings(false);
                setPlayer.SetValue(api,new MainMenuApiClient.PlayerSnapshot{id=first,provider="wallet"});
                menu.OpenSettings();
                if(referral.gameObject.activeSelf||PlayerPrefs.HasKey(prefix+first))throw new Exception("Waiting for the referral profile must not use up a Settings visit");
                cache.Invoke(screen,new object[]{first,new JObject{["referred"]=false}});
                if(!referral.gameObject.activeSelf)throw new Exception("First eligible Settings visit must show referral entry");
                menu.OpenSettings();menu.RefreshLayout();
                if(PlayerPrefs.GetInt(prefix+first)!=1)throw new Exception("Refreshing an open Settings page counted another visit");
                MainMenuChecks.Capture(MainMenuPlatform.AndroidLandscape,844,390,"referral-settings-seeker");

                for(int visit=2;visit<=4;visit++)
                {
                    menu.CloseSettings(false);menu.OpenSettings();menu.RefreshLayout();
                    if(referral.gameObject.activeSelf!=(visit<=3))throw new Exception($"Incorrect referral visibility on Settings visit {visit}");
                }
                if(PlayerPrefs.GetInt(prefix+first)!=3||settings.RowButtons[1].navigation.selectOnDown!=settings.LogoutButton)
                    throw new Exception("After three visits, controller navigation must skip the hidden referral action");

                setPlayer.SetValue(api,new MainMenuApiClient.PlayerSnapshot{id=second,provider="wallet"});
                cache.Invoke(screen,new object[]{second,new JObject{["referred"]=false}});
                if(!referral.gameObject.activeSelf||PlayerPrefs.GetInt(prefix+second)!=1)throw new Exception("Settings visit limits must be independent per player");
                if(EventSystem.current)EventSystem.current.SetSelectedGameObject(referral.gameObject);
                cache.Invoke(screen,new object[]{second,new JObject{["referred"]=true}});
                if(referral.gameObject.activeSelf)throw new Exception("A confirmed referral must remove the Settings action immediately");
                if(EventSystem.current&&EventSystem.current.currentSelectedGameObject!=settings.LogoutButton.gameObject)
                    throw new Exception("Focus must move to Logout when the selected referral action disappears");

                cache.Invoke(screen,new object[]{second,new JObject{["referred"]=false}});
                menu.CloseSettings(false);menu.OpenSettings();
                if(referral.gameObject.activeSelf||PlayerPrefs.GetInt(prefix+second)!=1)
                    throw new Exception("A late profile response must not restore an applied referral or use up a visit");
            }
            finally
            {
                setPlayer.SetValue(api,null);
                PlayerPrefs.DeleteKey(prefix+first);PlayerPrefs.DeleteKey(prefix+second);PlayerPrefs.Save();
                if(fixturePhone)UnityEngine.Object.DestroyImmediate(fixturePhone);
            }
        }
    }
}
