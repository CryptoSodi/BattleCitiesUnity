using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using BattleCities.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class Psg1BattleGuideChecks
    {
        const string ArtDirectory="Assets/BattleCities/Art/controls-guide/psg1/";
        public static void PrepareArtwork()
        {
            var art=Resources.Load<Psg1BattleGuideArt>("Psg1BattleGuideArt");
            if(!art){art=ScriptableObject.CreateInstance<Psg1BattleGuideArt>();AssetDatabase.CreateAsset(art,"Assets/BattleCities/Resources/Psg1BattleGuideArt.asset");}
            foreach(var field in typeof(Psg1BattleGuideArt).GetFields())
            {
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(ArtDirectory+field.Name+".png");
                if(!sprite)throw new Exception("PSG1 guide artwork is not ready: "+field.Name);
                field.SetValue(art,sprite);
            }
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
            var doc=JObject.Parse(File.ReadAllText("Content/ScreenUpdates/android-v1.json"));
            var images=(JArray)doc["images"];var screens=(JArray)doc["screens"];
            foreach(var item in images.OfType<JObject>().Where(x=>((string)x["id"]).StartsWith("psg-guide-")).ToArray())item.Remove();
            foreach(var item in screens.OfType<JObject>().Where(x=>(string)x["id"]==BattleGuideScreen.Psg1ScreenId).ToArray())item.Remove();
            var bindings=new JObject();
            foreach(var key in BattleGuideScreen.Psg1ArtKeys)
            {
                string source;
                if(new[]{"stick","dpad","shoulder","face","device"}.Contains(key))source=ArtDirectory+key+".png";
                else source="Assets/BattleCities/Art/controls-guide/"+(key=="tap"?"hand-tap-release":key=="charge"?"hand-charge-release":key=="normal-shell"?"shell-normal":"shell-power")+".png";
                string id="psg-guide-"+key;images.Add(new JObject{{"id",id},{"source",source}});bindings[key]=id;
            }
            screens.Add(new JObject{{"id",BattleGuideScreen.Psg1ScreenId},{"platform","psg1"},{"copy",JObject.FromObject(BattleGuideScreen.Psg1Copy)},
                {"art",bindings},{"regions",new JObject()},{"layout",new JObject()},{"patches",new JArray()},{"flows",new JArray()}});
            File.WriteAllText("Content/ScreenUpdates/android-v1.json",doc.ToString()+Environment.NewLine);
        }
        public static void Run()
        {
            PrepareArtwork();BattleGuideChecks.Run();
            int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception("PSG1 GUIDE: "+message);checks++;}
            var oldPlatform=ScreenContent.EditorPlatform;
            var oldEvents=EventSystem.current;
            int oldWeb=PlayerPrefs.GetInt(BattleGuideScreen.PreferenceKey,-1),oldPsg=PlayerPrefs.GetInt(BattleGuideScreen.Psg1PreferenceKey,-1);
            GameObject canvasObject=null,events=null;
            try
            {
                ScreenContent.EditorPlatform="psg1";PlayerPrefs.DeleteKey(BattleGuideScreen.Psg1PreferenceKey);PlayerPrefs.SetInt(BattleGuideScreen.PreferenceKey,1);
                Check(BattleGuideScreen.ShouldShow,"Web opt-out does not suppress PSG1 guide");
                BattleGuideChecks.VerifyOta("Content/ScreenUpdates/android-v1.json",false,"psg-guide-check");
                Check(ScreenUpdates.ReleaseId=="psg-guide-check","Android signed OTA and cached fallback accept guide");
                foreach(string key in BattleGuideScreen.Psg1ArtKeys)Check(ScreenUpdates.Image("psg-guide-"+key),"verified OTA artwork "+key);
                var theme=AssetDatabase.LoadAssetAtPath<MenuTheme>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:MenuTheme").First()));
                var skin=Resources.Load<PreBattleArt>("PreBattleArt");
                canvasObject=new GameObject("PSG1 Guide preview",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
                var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                events=new GameObject("Guide events",typeof(EventSystem));
                // EventSystem lifecycle does not run automatically in batch Edit mode.
                typeof(EventSystem).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events.GetComponent<EventSystem>(),null);
                EventSystem.current=events.GetComponent<EventSystem>();
                var frame=new GameObject("TV frame",typeof(RectTransform),typeof(Image));frame.transform.SetParent(canvasObject.transform,false);
                var fr=(RectTransform)frame.transform;fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;
                frame.GetComponent<Image>().sprite=theme.BlueFrame;frame.GetComponent<Image>().type=Image.Type.Sliced;
                var go=new GameObject("Battle station",typeof(RectTransform));go.transform.SetParent(frame.transform,false);
                var root=(RectTransform)go.transform;root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=Vector2.one*12;root.offsetMax=-Vector2.one*12;
                var guide=go.AddComponent<BattleGuideScreen>();guide.Configure(theme,skin);int continued=0;
                foreach(var size in new[]{new Vector2(1240,1080),new Vector2(1100,780),new Vector2(844,600),new Vector2(620,720)})
                {
                    ((RectTransform)canvasObject.transform).sizeDelta=size;Canvas.ForceUpdateCanvases();
                    guide.Open(0,()=>continued++);Canvas.ForceUpdateCanvases();
                    typeof(BattleGuideScreen).GetMethod("Layout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(guide,null);Canvas.ForceUpdateCanvases();
                    Check(continued==0,"opening does not launch or charge");
                    var scroll=go.GetComponentInChildren<ScrollRect>();
                    Check(!scroll.enabled&&!scroll.vertical&&scroll.content.rect.height*scroll.content.localScale.y<=scroll.viewport.rect.height+1,"complete PSG1 guide fits without scrolling");
                    Check(Mathf.Abs(go.transform.Find("Title").GetComponent<RectTransform>().rect.height-Mathf.Clamp(root.rect.height*.083f,46,64))<.1f,"PSG1 shared title height");
                    Check(Mathf.Abs(go.transform.Find("Footer").GetComponent<RectTransform>().rect.height-Mathf.Clamp(root.rect.height*.078f,50,64))<.1f,"PSG1 shared footer height");
                    var labels=go.GetComponentsInChildren<TMP_Text>();
                    foreach(var label in labels)label.ForceMeshUpdate();
                    Check(!labels.Any(t=>t.isTextOverflowing),"all instruction text fits: "+string.Join(", ",labels.Where(t=>t.isTextOverflowing).Select(t=>t.transform.parent.name+"/"+t.name)));
                    Check(labels.Count(t=>t.text=="R1")==2&&labels.Count(t=>t.text=="L1")==2,"shoulder bindings are visible");
                    Check(!labels.Any(t=>t.text.Contains("Space")||t.text=="Q"||t.text=="E"),"no Web control instructions leak into PSG1");
                    Check(labels.Any(t=>t.text.Contains("+2 power"))&&labels.Any(t=>t.text.Contains("multiple special")),"power and conditional switching copy");
                    foreach(string key in new[]{"A","B","X","Y"})Check(scroll.content.Find("Powerups/Key "+key),"direct power-up "+key);
                    var first=go.GetComponentsInChildren<Button>().Single();
                    var visited=new HashSet<Selectable>();var queue=new Queue<Selectable>();queue.Enqueue(first);
                    while(queue.Count>0)
                    {
                        var item=queue.Dequeue();if(!visited.Add(item))continue;
                        foreach(var link in new[]{item.navigation.selectOnUp,item.navigation.selectOnDown,item.navigation.selectOnLeft,item.navigation.selectOnRight})
                            if(link&&!visited.Contains(link))queue.Enqueue(link);
                    }
                    Check(visited.Count==2&&visited.All(s=>s.transform.IsChildOf(go.transform)),"only Continue and opt-out are reachable");
                    Check(EventSystem.current.currentSelectedGameObject==first.gameObject,"guide opens with Continue focused");
                    Check(!scroll.content.GetComponentsInChildren<Selectable>().Any(),"information cards are not selectable");
                    BattleGuideChecks.Capture(canvas,(int)size.x,(int)size.y,prefix:"psg1");
                    Check(scroll.content.anchoredPosition==Vector2.zero,"information sheet stays fixed");
                    Check(!labels.Any(t=>t.text.Contains("SCROLL")),"footer does not advertise scrolling");
                    BattleGuideChecks.Capture(canvas,(int)size.x,(int)size.y,"-bottom","psg1");
                    guide.Cancel();Check(continued==0,"Back/cancel never launches");
                }
                guide.Open(0,()=>continued++);guide.GetComponentInChildren<Toggle>().OnSubmit(new BaseEventData(EventSystem.current));
                Check(!BattleGuideScreen.ShouldShow,"A on Don't show again saves PSG1 preference");
                guide.Accept();guide.Accept();Check(continued==1,"PSG1 Continue launches exactly once");
                ScreenContent.EditorPlatform="web";Check(!BattleGuideScreen.ShouldShow,"PSG1 preference did not alter original Web opt-out");
                ScreenContent.EditorPlatform="seeker";Check(!BattleGuideScreen.IsSupportedPlatform,"Seeker remains excluded");
                Debug.Log("PSG1 BATTLE GUIDE CHECKS PASSED: "+checks);
            }
            finally
            {
                if(canvasObject)UnityEngine.Object.DestroyImmediate(canvasObject);
                if(events){typeof(EventSystem).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(events.GetComponent<EventSystem>(),null);UnityEngine.Object.DestroyImmediate(events);}
                if(oldEvents)EventSystem.current=oldEvents;
                ScreenContent.EditorPlatform=oldPlatform;
                Restore(BattleGuideScreen.PreferenceKey,oldWeb);Restore(BattleGuideScreen.Psg1PreferenceKey,oldPsg);PlayerPrefs.Save();ScreenUpdates.EditorReset();
            }
        }
        static void Restore(string key,int value){if(value<0)PlayerPrefs.DeleteKey(key);else PlayerPrefs.SetInt(key,value);}
    }
}
