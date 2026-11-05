using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace BattleCities
{
    // Transport adapter for the existing API. Credentials are supplied by the host
    // login flow at runtime, never serialized into scenes or source control.
    public sealed class EconomyClient : MonoBehaviour
    {
        [SerializeField] private string baseUrl="";
        private string sessionCookie;
        public string Status {get;private set;}="Inventory API not configured";
        public bool Authenticated {get;private set;}
        private JObject account=new JObject();
        private BattleCities.UI.MainMenuApiClient requestBridge;
        private readonly Dictionary<string,string> uncertain=new Dictionary<string,string>();
        private readonly HashSet<string> claiming=new HashSet<string>();
        private readonly string[] slots={"active-one","active-two","active-three","active-four"};
        private static readonly Dictionary<string,string> Types=new Dictionary<string,string>{{"shield","shield"},{"base-defence","defence"},{"freeze","freeze"},{"speed","speed"},{"upgrade","upgrade"},{"zoom-out","zoomout"},{"wipeout","wipeout"},{"extra-life","life"},{"max-healer","healmax"},{"health-healer","heal2"},{"rapid-shooter","rapid"},{"rapid-shooter-long","rapidlong"}};
        public sealed class Drop { public string Type,ClaimId; }
        [Serializable] public sealed class Consumption { public string Type,Item,PlayerId,RequestId,MatchId; }
        private sealed class Response { public long Code;public JObject Body; }
        private async void Start()
        {
            if(GetComponent<BattleGame>()?.IsReplaying==true){Status="Replay playback";enabled=false;return;}
            if(LevelEditor.LevelEditorPlaytest.IsActive){Status="Level editor sandbox";enabled=false;return;}
            if(BattlePreparation.Ready)
            {
                baseUrl=BattlePreparation.ApiUrl;
                var host=new GameObject("Battle inventory transport");host.SetActive(false);host.transform.SetParent(transform);
                requestBridge=host.AddComponent<BattleCities.UI.MainMenuApiClient>();
                requestBridge.ConfigureAutomaticRefresh(false);requestBridge.ConfigureGuestFallback(false);requestBridge.Configure(baseUrl);host.SetActive(true);
            }
            if(!string.IsNullOrWhiteSpace(baseUrl))await Refresh();
        }
        public async Task Configure(string url,string cookie=null)
        {if(!Uri.TryCreate(url,UriKind.Absolute,out var parsed)||(parsed.Scheme!="http"&&parsed.Scheme!="https"))throw new ArgumentException("Economy URL must be HTTP or HTTPS");baseUrl=url;sessionCookie=cookie;await Refresh();}
        private async Task<Response> Api(string path,JObject body=null)
        {
            if(requestBridge)
            {
                var completion=new TaskCompletionSource<Response>();
                requestBridge.StartCoroutine(requestBridge.Request(body==null?"GET":"POST",path,body,(code,json,error)=>
                {if(code==0||json==null)completion.TrySetException(new InvalidOperationException("Economy connection failed"));else completion.TrySetResult(new Response{Code=code,Body=json});}));
                return await completion.Task;
            }
            using(var request=new UnityWebRequest(new Uri(new Uri(baseUrl),path),body==null?"GET":"POST"))
            {
                request.downloadHandler=new DownloadHandlerBuffer();request.timeout=10;
                if(body!=null)request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
                request.SetRequestHeader("Content-Type","application/json");
                if(!string.IsNullOrEmpty(sessionCookie))request.SetRequestHeader("Cookie",sessionCookie);
                var operation=request.SendWebRequest();while(!operation.isDone)await Task.Yield();
                if(request.result==UnityWebRequest.Result.ConnectionError)throw new InvalidOperationException("Economy connection failed");
                return new Response{Code=request.responseCode,Body=JObject.Parse(request.downloadHandler.text)};
            }
        }
        public async Task Refresh()
        {
            Authenticated=false;account=new JObject();
            try {var r=await Api("/api/economy/account");if(r.Code>=200&&r.Code<300&&(bool?)r.Body["authenticated"]==true&&r.Body["account"] is JObject a){account=a;Authenticated=true;Status="Economy connected";foreach(var id in Pending())_=Claim(id);}else Status="Sign in through the host app to use inventory";}
            catch {Status="Economy unavailable";}
        }
        private string Item(int i)=>i>=0&&i<4?(string)account["loadout"]?[slots[i]]:null;
        public string SlotType(int i){var item=Item(i);return item!=null&&Types.TryGetValue(item,out var type)?type:null;}
        public int SlotCount(int i){var item=Item(i);return item==null?0:Math.Max(0,(int?)account["inventory"]?[item]??0);}
        public string SlotLabel(int i)
        {var item=Item(i);return item!=null&&Types.ContainsKey(item)?item+" ×"+SlotCount(i):"Empty";}
        private string PendingKey=>"battlecities.pending-online-powerup."+baseUrl+"."+(string)account["playerId"];
        public Consumption PendingOnline
        {get{if(!Authenticated)return null;try{return JsonUtility.FromJson<Consumption>(PlayerPrefs.GetString(PendingKey,""));}catch{return null;}}}
        private void SaveOnlinePending(Consumption value){PlayerPrefs.SetString(PendingKey,JsonUtility.ToJson(value));PlayerPrefs.Save();}
        public void ClearOnlinePending(string requestId)
        {if(PendingOnline?.RequestId!=requestId)return;PlayerPrefs.DeleteKey(PendingKey);PlayerPrefs.Save();}
        public async Task<Consumption> ConfirmOnlinePending()
        {
            var pending=PendingOnline;if(pending==null)return null;
            try
            {
                var result=await Api("/api/economy/powerups/consume",new JObject{{"itemId",pending.Item},{"powerupType",pending.Type},{"requestId",pending.RequestId},{"matchId",pending.MatchId}});
                if(result.Body["account"] is JObject a)account=a;
                if(result.Code>=200&&result.Code<300&&(bool?)result.Body["ok"]==true&&(string)result.Body["powerupType"]==pending.Type)return pending;
                if(result.Code>=400&&result.Code<500)ClearOnlinePending(pending.RequestId);
            }
            catch{}
            return null;
        }
        public async Task<bool> RefundOnlinePending(Consumption pending)
        {
            try
            {
                var result=await Api("/api/economy/powerups/refund",new JObject{{"itemId",pending.Item},{"requestId",pending.RequestId},{"matchId",pending.MatchId}});
                if(result.Body["account"] is JObject a)account=a;
                if((bool?)result.Body["ok"]==true||(string)result.Body["statusText"]=="ALREADY ACTIVATED")
                {ClearOnlinePending(pending.RequestId);return true;}
            }catch{}
            return false;
        }
        public async Task<Consumption> ConsumeSlot(int index,string matchId=null)
        {
            string item=Item(index);if(!Authenticated||item==null||!Types.TryGetValue(item,out string type))return null;
            string playerId=(string)account["playerId"];
            if(matchId!=null)
            {
                if(PendingOnline!=null){Status="Resolving previous power-up";return null;}
                try{var support=await Api("/api/economy/powerups/redeem");if(support.Code!=200||(bool?)support.Body["ok"]!=true||(int?)support.Body["version"]!=1){Status="Multiplayer power-ups unavailable";return null;}}
                catch{Status="Multiplayer power-ups unavailable";return null;}
            }
            string requestKey=item+"|"+(matchId??"");
            if(!uncertain.TryGetValue(requestKey,out var id)){id=Guid.NewGuid().ToString();uncertain[requestKey]=id;}
            if(matchId!=null)SaveOnlinePending(new Consumption{Type=type,Item=item,PlayerId=playerId,RequestId=id,MatchId=matchId});
            try
            {
                var payload=new JObject{{"itemId",item},{"powerupType",type},{"requestId",id}};if(matchId!=null)payload["matchId"]=matchId;
                var r=await Api("/api/economy/powerups/consume",payload);
                if(r.Body["account"] is JObject a)account=a;
                if(r.Code>=500){Status="Consumption uncertain; retry the same slot";return null;}
                if(r.Code>=200&&r.Code<300&&(bool?)r.Body["ok"]==true&&(string)r.Body["powerupType"]==type){uncertain.Remove(requestKey);Status="Economy connected";return new Consumption{Type=type,Item=item,PlayerId=playerId,RequestId=id,MatchId=matchId};}
                if(r.Code<200||r.Code>=300||(bool?)r.Body["ok"]==false){uncertain.Remove(requestKey);if(matchId!=null)ClearOnlinePending(id);}
                Status="Power-up consumption was not approved";return null;
            }
            catch {Status="Consumption uncertain; retry the same slot";return null;}
        }
        public async Task<Drop> Roll(int stage)
        {
            if(!Authenticated)return null;
            try {var r=await Api("/api/economy/drops/roll",new JObject{{"requestId",Guid.NewGuid().ToString()},{"levelNumber",stage},{"powerupRosterVersion",1}});if(r.Code<200||r.Code>=300)return null;var type=(string)r.Body["dropType"];if(Array.IndexOf(new[]{"shield","defence","freeze","life","speed","upgrade","zoomout","wipeout","healmax","heal2","rapid","rapidlong","batc100","batc200"},type)<0)return null;bool currency=type.StartsWith("batc");var claim=(string)r.Body["claimId"];if(currency&&string.IsNullOrEmpty(claim))return null;return new Drop{Type=type,ClaimId=currency?claim:null};}catch{return null;}
        }
        private List<string> Pending()
        {try{return JsonConvert.DeserializeObject<List<string>>(PlayerPrefs.GetString("battlecities.pendingBatcDropClaims","[]"))??new List<string>();}catch{return new List<string>();}}
        private void Save(List<string> ids){PlayerPrefs.SetString("battlecities.pendingBatcDropClaims",JsonConvert.SerializeObject(ids));PlayerPrefs.Save();}
        public async Task Claim(string id)
        {
            if(!Authenticated||!claiming.Add(id))return;
            var ids=Pending();if(!ids.Contains(id)){ids.Add(id);Save(ids);}
            try {var r=await Api("/api/economy/drops/claim",new JObject{{"claimId",id}});if(r.Code>=200&&r.Code<300&&(bool?)r.Body["delivered"]==true){ids=Pending();ids.Remove(id);Save(ids);}}
            catch {Status="Drop claim pending; retry after reconnecting";}
            finally {claiming.Remove(id);}
        }
    }
}
