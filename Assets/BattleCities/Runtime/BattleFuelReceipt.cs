using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BattleCities
{
    public static class BattleFuelReceipt
    {
        public static string Key(string apiUrl,string playerId)=>"battlecities.pendingFuel."+Hash128.Compute(apiUrl+"|"+playerId);
        // Keep a paid attempt recoverable until tick one, including app restart or failed session setup.
        public static void MarkStarted(string apiUrl,string playerId,string requestId)
        {
            if(string.IsNullOrEmpty(playerId)||string.IsNullOrEmpty(requestId))return;
            string key=Key(apiUrl,playerId);
            if(!PlayerPrefs.HasKey(key))return;
            try
            {
                var receipt=JObject.Parse(PlayerPrefs.GetString(key));
                if((string)receipt["requestId"]!=requestId)return;
                PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
            }
            catch(Exception e){Debug.LogWarning("Could not finish fuel receipt: "+e.Message);}
        }
    }
}
