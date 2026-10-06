using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    public sealed partial class OperationsScreen
    {
        JObject account,xStatus,discord;
        JArray ledger;
        bool treasuryAuthenticated,xAuthenticated,discordAuthenticated;
        string accountError,ledgerError,xError,discordError;
        bool xReady;string repostReady,commentReady;
        static string Amount(JToken value,string format="N0")
        {return value!=null&&decimal.TryParse(value.ToString(),NumberStyles.Any,CultureInfo.InvariantCulture,out var n)?n.ToString(format,CultureInfo.InvariantCulture):"—";}
        static bool Yes(JToken token)=>token?.Type==JTokenType.Boolean&&token.Value<bool>();
        static string Text(JToken token)=>token==null||token.Type==JTokenType.Null?null:token.ToString();
        Sprite SectionIcon(int i)=>illustrations&&illustrations.quarters.Length>i?illustrations.quarters[i]:null;
        Sprite SocialIcon(int i)=>illustrations&&illustrations.socials.Length>i?illustrations.socials[i]:null;
        Sprite InventoryIcon(string id)
        {
            if(shopArt)for(int i=0;i<ShopCatalog.Products.Length;i++)if(ShopCatalog.Products[i].InventoryId==id&&i<shopArt.products.Length)return shopArt.products[i];
            return shopArt?shopArt.supplyCrate:null;
        }
        Sprite ManualIcon(FieldManualEntry entry)
        {
            if(entry==null)return null;
            if(entry.category=="powerups")return InventoryIcon(entry.slug);
            if(entry.category=="weapons")return entry.slug=="hull-plating"?InventoryIcon("shield"):illustrations?illustrations.cannon:null;
            string[] ids=entry.category=="tanks"?new[]{"vanguard","vanguard-mk2","vanguard-mk3","siegebreaker"}:new[]{"scout","rapid","armored","heavy"};
            int index=Array.IndexOf(ids,entry.slug);var sprites=illustrations?(entry.category=="tanks"?illustrations.playerTanks:illustrations.enemies):null;
            return sprites!=null&&index>=0&&index<sprites.Length?sprites[index]:null;
        }
        void Render()
        {
            if(!root)return;
            string heading=page==Page.Socials?"SOCIALS":page==Page.Treasury?"TREASURY":page==Page.Manual||page==Page.ManualDetail?"FIELD MANUAL":"QUARTERS";
            title.text=heading;
            titleIcon.sprite=page==Page.Socials?theme.NavigationIcons[4]:page==Page.Quarters?theme.NavigationIcons[1]:SectionIcon(page==Page.Treasury?0:1);
            titleIcon.color=Color.white;
            introTitle.text=page==Page.Socials?"JOIN THE BATTLE CITIES COMMUNITY":page==Page.Treasury?(history?"TRANSACTION HISTORY":"YOUR HOLDINGS"):page==Page.Quarters?"COMMAND CENTER":CategoryLabels[category]+" • FIELD INTELLIGENCE";
            introDescription.text=page==Page.Socials?"COMPLETE SOCIAL TASKS AND VERIFY AVAILABLE FUEL REWARDS":page==Page.Treasury?"BALANCES, OWNED ITEMS AND YOUR RECENT ACTIVITY":page==Page.Quarters?"ASSETS, OPERATIONS AND BATTLE INTELLIGENCE":"TANKS, WEAPONS, POWERUPS AND ENEMY INTELLIGENCE";
            BuildTabs();items.Clear();
            bool treasury=page==Page.Treasury;
            summary.gameObject.SetActive(treasury);detail.gameObject.SetActive(page==Page.ManualDetail);empty.gameObject.SetActive(false);
            scroll.gameObject.SetActive(page!=Page.ManualDetail);
            footerAction.gameObject.SetActive(treasury);
            footerCaption.text=treasury&&!treasuryAuthenticated&&!loading&&accountError==null?"CONNECT WALLET":"REFRESH";
            footerAction.interactable=!loading;
            if(page==Page.Quarters)QuartersItems();
            else if(page==Page.Socials)SocialItems();
            else if(page==Page.Manual)ManualItems();
            else if(page==Page.ManualDetail)
            {
                var entry=manual.FirstOrDefault(e=>e.slug==entryId);
                if(entry!=null){detailIcon.sprite=ManualIcon(entry);detailIcon.color=Color.white;detailTitle.text=entry.name;detailRole.text=entry.role;detailLore.text=entry.lore;detailEffect.text=entry.effect;detailSource.text="SOURCE: "+entry.source;}
            }
            else if(treasury)TreasuryItems();
            BuildCards();if(treasury&&history&&treasuryAuthenticated&&ledger!=null&&!loading&&ledgerError==null)BuildLedger();
            if(treasury)RenderTreasurySummary();
            if(page==Page.Manual&&items.Count==0)Message("MANUAL UNAVAILABLE","Field intelligence could not be loaded.",SectionIcon(1));
            SetStatus(statusMessage);ApplyLayout(platform);
        }
        void QuartersItems()
        {
            string[] names={"TREASURY","FIELD MANUAL","CAMPAIGNS","STAKING","TRADING","BOOSTS","AIRDROP"};
            string[] details={"BALANCES, ITEMS & HISTORY","TANKS, POWERUPS & ENEMIES","EVENTS, OPERATIONS & REWARDS","TOKEN STAKING & PERKS","SWAPS & MARKET TOOLS","TRAIT BOOSTS & PERKS","ALLOCATION & CLAIM STATUS"};
            for(int i=0;i<names.Length;i++)
                items.Add(new Item{Key="quarter-"+i,Title=names[i],Detail=details[i],Icon=SectionIcon(i),Action=i<2?"OPEN":"LOCKED",Locked=i>=2,Click=i==0?(UnityEngine.Events.UnityAction)OpenTreasury:i==1?OpenManual:null});
        }
        void ManualItems()
        {
            foreach(var entry in manual.Where(e=>e.category==Categories[category]))
            {string id=entry.slug;items.Add(new Item{Key=id,Title=entry.name,Detail=entry.role,Action="VIEW DETAILS",Icon=ManualIcon(entry),Click=()=>OpenManualEntry(id)});}
        }
        void TreasuryItems()
        {
            if(loading){Message("LOADING TREASURY","Fetching your account and recent transactions...",SectionIcon(0));return;}
            if(accountError!=null){Message("TREASURY UNAVAILABLE","Press Refresh to try again.",SectionIcon(0));return;}
            if(!treasuryAuthenticated){Message("CONNECT TO VIEW YOUR TREASURY","Connect your wallet to see your balances, items and history.",SectionIcon(0));return;}
            if(history)
            {
                if(ledgerError!=null)Message("HISTORY UNAVAILABLE","Your holdings are available. Refresh to retry the history.",SectionIcon(0));
                else if(ledger==null||ledger.Count==0)Message("NO TRANSACTIONS YET","Purchases and rewards will appear here.",SectionIcon(0));
                return;
            }
            if(account?["inventory"] is JObject inventory)
                foreach(var property in inventory.Properties())
                {
                    if(!decimal.TryParse(property.Value.ToString(),out var count)||count<=0)continue;
                    string id=property.Name,name=id=="upgrade"?"STAR":id.Replace('-',' ').ToUpperInvariant();
                    items.Add(new Item{Key=id,Title=name,Detail="OWNED  "+Amount(property.Value),Action="FIELD MANUAL",Icon=InventoryIcon(id),Click=()=>{category=2;OpenManualEntry(id);}});
                }
            if(items.Count==0)Message("NO ITEMS OWNED","Visit Shop to equip your treasury.",SectionIcon(0));
        }
        void RenderTreasurySummary()
        {
            stats.Clear();
            // Legacy tokenBalance is BATC. It must never be presented as SKR.
            string[] names={"BATC","SOL","FUEL","ITEMS"};
            decimal owned=0;if(account?["inventory"] is JObject inventory)foreach(var p in inventory.Properties())if(decimal.TryParse(p.Value.ToString(),out var count))owned+=Math.Max(0,count);
            string[] values={Amount(account?["tokenBalance"]),Amount(account?["solBalance"],"0.####"),Amount(account?["fuelBalance"]),account!=null?owned.ToString("N0"):"—"};
            Sprite[] icons={theme.PrizeCrates.Length>0?theme.PrizeCrates[0]:null,shopArt?shopArt.solana:null,art.fuelCan,shopArt?shopArt.supplyCrate:null};
            for(int i=0;i<4;i++)
            {
                var panel=Panel("Stat "+i,summary,theme.Rounded);panel.GetComponent<Image>().pixelsPerUnitMultiplier=5;panel.GetComponent<Image>().color=new Color32(240,226,190,255);
                Icon("Icon",panel,icons[i],new Rect(.06f,.18f,.24f,.64f));
                Label("Label",panel,names[i],new Rect(.34f,.08f,.60f,.35f),23,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);
                var value=Label("Value",panel,loading?"—":values[i],new Rect(.34f,.44f,.60f,.48f),30,ArcadeTextTreatment.PrizeAmount,TextAlignmentOptions.MidlineLeft);stats.Add(new Stat{Rect=panel,Value=value});
            }
        }
        void Message(string heading,string description,Sprite icon)
        {empty.gameObject.SetActive(true);emptyTitle.text=heading;emptyDescription.text=description;emptyIcon.sprite=icon;}
        void BuildLedger()
        {
            if(ledger.Count==0)return;
            LedgerRow("Heading","CURRENCY","AMOUNT","REASON","DATE",true);
            for(int i=0;i<ledger.Count;i++)
            {
                var row=ledger[i];string amount=Amount(row["amount"],"#,0.#########");if(decimal.TryParse(Text(row["amount"]),NumberStyles.Any,CultureInfo.InvariantCulture,out var n)&&n>0)amount="+"+amount;
                DateTimeOffset date;string dateText="—";var dateToken=row["createdAt"];
                if(dateToken is JValue value&&value.Value is DateTime time)dateText=time.ToUniversalTime().ToString("yyyy-MM-dd");
                else if(DateTimeOffset.TryParse(Text(dateToken),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out date))dateText=date.UtcDateTime.ToString("yyyy-MM-dd");
                LedgerRow("Ledger "+i,Text(row["currency"])??"—",amount,Text(row["reason"])??"—",dateText,false);
            }
        }
        void LedgerRow(string name,string currency,string amount,string reason,string date,bool heading)
        {
            var rect=Panel(name,scroll.content,theme.Rounded);rect.gameObject.SetActive(true);rect.SetAsLastSibling();rect.GetComponent<Image>().pixelsPerUnitMultiplier=5;rect.GetComponent<Image>().color=heading?new Color32(3,49,77,255):new Color32(245,233,205,255);
            var layout=rect.GetComponent<LayoutElement>();if(!layout)layout=rect.gameObject.AddComponent<LayoutElement>();layout.preferredHeight=heading?40:58;
            var fields=new[]{currency,amount,reason,date};float[] left={.02f,.17f,.35f,.78f},width={.14f,.17f,.42f,.20f};
            for(int i=0;i<4;i++){var text=Label("Column "+i,rect,fields[i],new Rect(left[i],.07f,width[i],.86f),heading?22:25,ArcadeTextTreatment.PrizeAmount,i==2?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Center);if(heading)text.color=Color.white;if(i==2){text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;}}
            if(heading)return;
            var button=rect.GetComponent<Button>();if(!button)button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.image.raycastTarget=true;button.onClick.RemoveAllListeners();
            var colors=ColorBlock.defaultColorBlock;colors.highlightedColor=colors.selectedColor=new Color(.65f,.85f,1f);button.colors=colors;
            var focus=rect.GetComponent<RankingRowFocus>();if(!focus)focus=rect.gameObject.AddComponent<RankingRowFocus>();focus.Configure(scroll);ledgerRows.Add(button);
        }
        void SocialItems()
        {
            items.Add(new Item{Key="website",Title="WEBSITE",Detail="BATTLECITIES.COM",Action="VISIT WEBSITE",Icon=SocialIcon(0),Click=()=>OpenLink("https://battlecities.com","WEBSITE OPENED IN YOUR BROWSER")});
            bool connected=Yes(xStatus?["connected"]),follows=Yes(xStatus?["follows"]);
            items.Add(new Item{Key="x-follow",Title="X FOLLOW",Completed=xError==null&&xAuthenticated&&follows,Detail=loading?"CHECKING STATUS...":xError!=null?"STATUS UNAVAILABLE":!xAuthenticated?"CONNECT WALLET TO BEGIN":follows?"FOLLOW VERIFIED":!connected?"LINK X • +5 FUEL":xReady?"VERIFY FOLLOW • +5 FUEL":"FOLLOW @BATTLECITIESHQ • +5 FUEL",Action=loading?"CHECKING...":xError!=null?"RETRY":!xAuthenticated?"CONNECT WALLET":follows?"FOLLOWED":!connected?"CONNECT X":xReady?"VERIFY FOLLOW":"FOLLOW ON X",Icon=SocialIcon(1),Click=ActFollow});
            items.Add(new Item{Key="instagram",Title="INSTAGRAM",Detail="@BATTLECITIESHQ",Action="FOLLOW INSTAGRAM",Icon=SocialIcon(2),Click=()=>OpenLink("https://www.instagram.com/battlecitieshq","INSTAGRAM OPENED IN YOUR BROWSER")});
            bool verified=Yes(discord?["verified"]),claimed=Yes(discord?["rewardClaimed"]);
            items.Add(new Item{Key="discord",Title="DISCORD",Completed=discordError==null&&discordAuthenticated&&claimed,Detail=loading?"CHECKING STATUS...":discordError!=null?"STATUS UNAVAILABLE":!discordAuthenticated?"CONNECT WALLET TO BEGIN":claimed?"COMPLETED • 5 FUEL SECURED":verified?"VERIFIED • CLAIM +5 FUEL":"JOIN AND VERIFY • +5 FUEL",Action=loading?"CHECKING...":discordError!=null?"RETRY":!discordAuthenticated?"CONNECT WALLET":claimed?"VERIFIED":verified?"CLAIM FUEL":"JOIN & VERIFY",Icon=SocialIcon(3),Click=ActDiscord});
            AddSocialTask("repost",4,xStatus?["repostTask"] as JObject,repostReady);
            AddSocialTask("comment",5,xStatus?["commentTask"] as JObject,commentReady);
        }
        void AddSocialTask(string kind,int icon,JObject task,string ready)
        {
            bool follows=xAuthenticated&&Yes(xStatus?["follows"]),claimed=Yes(task?["claimed"]),exists=task!=null;
            string verb=kind.ToUpperInvariant();bool verify=exists&&Text(task["id"])==ready;
            items.Add(new Item{Key="x-"+kind,Title="X "+verb,Icon=SocialIcon(icon),Locked=xError==null&&(!follows||!exists),Completed=xError==null&&follows&&claimed,
                Detail=loading?"CHECKING STATUS...":xError!=null?"STATUS UNAVAILABLE":!follows?"COMPLETE X FOLLOW FIRST":!exists?"NO ACTIVE TASK":claimed?"COMPLETED • "+Amount(task["rewardFuel"])+" FUEL SECURED":verify?"VERIFY • +"+Amount(task["rewardFuel"])+" FUEL":"ACTIVE TASK • +"+Amount(task["rewardFuel"])+" FUEL",
                Action=xError!=null?"RETRY":claimed?"COMPLETED":!follows||!exists?"LOCKED":verify?"VERIFY "+verb:verb,Click=()=>ActTask(kind)});
        }
    }
}
