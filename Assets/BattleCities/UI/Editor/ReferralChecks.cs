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
            MainMenuChecks.Capture(MainMenuPlatform.AndroidLandscape,844,390,"referral-settings-seeker");
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
                GUIUtility.systemCopyBuffer=" https://battlecities.com/?ref=ABCDEF012345 ";paste.onClick.Invoke();
                if(input.text!="https://battlecities.com/?ref=ABCDEF012345")throw new Exception("Paste did not populate the field");
                GUIUtility.systemCopyBuffer=new string('X',2049);paste.onClick.Invoke();
                if(input.text!="https://battlecities.com/?ref=ABCDEF012345")throw new Exception("Oversized clipboard contents should not replace the field");
            }
            finally{GUIUtility.systemCopyBuffer=savedClipboard;}
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var request=typeof(ReferralScreen).GetField("pasteRequestId",flags);
            var snapshot=typeof(ReferralScreen).GetField("pasteSnapshot",flags);
            request.SetValue(screen,"active");snapshot.SetValue(screen,input.text);
            screen.OnReferralPasteResult("{\"id\":\"stale\",\"ok\":true,\"text\":\"SHOULD NOT APPLY\"}");
            if(input.text!="https://battlecities.com/?ref=ABCDEF012345")throw new Exception("Stale paste callback changed the field");
            screen.OnReferralPasteResult("{\"id\":\"active\",\"ok\":true,\"text\":\"ABCDEF012345\"}");
            if(input.text!="ABCDEF012345")throw new Exception("Browser paste callback did not populate the field");
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
            Debug.Log("PASS: referral dialog layout and controller focus");
        }
    }
}
