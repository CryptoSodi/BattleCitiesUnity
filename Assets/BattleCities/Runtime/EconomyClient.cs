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
        private static readonly Dictionary<string,string> Types=new Dictionary<string,string>{{"shield","shield"},{"base-defence","defence"},{"freeze","freeze"},{"speed","speed"},{"upgrade","upgrade"},{"zoom-out","zoomout"},{"wipeout","wipeout"}};
        public sealed class Drop { public string Type,ClaimId; }
        private sealed class Response { public long Code;public JObject Body; }
        private async void Start()
        {
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
        public async Task<string> ConsumeSlot(int index)
        {
            string item=Item(index);if(!Authenticated||item==null||!Types.TryGetValue(item,out string type))return null;
            if(!uncertain.TryGetValue(item,out var id)){id=Guid.NewGuid().ToString();uncertain[item]=id;}
            try
            {
                var r=await Api("/api/economy/powerups/consume",new JObject{{"itemId",item},{"powerupType",type},{"requestId",id}});
                if(r.Body["account"] is JObject a)account=a;
                if(r.Code>=500){Status="Consumption uncertain; retry the same slot";return null;}
                if(r.Code>=200&&r.Code<300&&(bool?)r.Body["ok"]==true&&(string)r.Body["powerupType"]==type){uncertain.Remove(item);Status="Economy connected";return type;}
                if(r.Code<200||r.Code>=300||(bool?)r.Body["ok"]==false)uncertain.Remove(item);
                Status="Power-up consumption was not approved";return null;
            }
            catch {Status="Consumption uncertain; retry the same slot";return null;}
        }
        public async Task<Drop> Roll(int stage)
        {
            if(!Authenticated)return null;
            try {var r=await Api("/api/economy/drops/roll",new JObject{{"requestId",Guid.NewGuid().ToString()},{"levelNumber",stage}});if(r.Code<200||r.Code>=300)return null;var type=(string)r.Body["dropType"];if(Array.IndexOf(new[]{"shield","defence","freeze","life","speed","upgrade","zoomout","wipeout","batc100","batc200"},type)<0)return null;bool currency=type.StartsWith("batc");var claim=(string)r.Body["claimId"];if(currency&&string.IsNullOrEmpty(claim))return null;return new Drop{Type=type,ClaimId=currency?claim:null};}catch{return null;}
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
