using UnityEngine;

namespace BattleCities.UI
{
    static class BattleResultsShare
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void BattleCitiesResultsShare(string target,string text);
#endif
        public static void Share(string target,string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BattleCitiesResultsShare(target,text);
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var intent=new AndroidJavaObject("android.content.Intent","android.intent.action.SEND"))
                using(var intentClass=new AndroidJavaClass("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setType","text/plain");intent.Call<AndroidJavaObject>("putExtra","android.intent.extra.TEXT",text);
                    using(var chooser=intentClass.CallStatic<AndroidJavaObject>("createChooser",intent,"Share results"))activity.Call("startActivity",chooser);
                }
                GameObject.Find(target)?.SendMessage("OnResultsShared","SHARE MENU OPENED");
            }
            catch {GUIUtility.systemCopyBuffer=text;GameObject.Find(target)?.SendMessage("OnResultsShared","RESULTS COPIED");}
#else
            GUIUtility.systemCopyBuffer=text;GameObject.Find(target)?.SendMessage("OnResultsShared","RESULTS COPIED");
#endif
        }
    }
}
