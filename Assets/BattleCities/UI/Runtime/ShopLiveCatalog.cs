using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace BattleCities.UI
{
    // Presentation data never authorizes a purchase. The API quote and confirmed
    // payment remain authoritative for price, entitlement and full-season scores.
    public sealed class ShopLiveCatalog
    {
        readonly Dictionary<string,JObject> items=new Dictionary<string,JObject>();
        public JObject Pass {get;private set;}
        public string SeasonId=>Text(Pass?["season"]?["id"]);
        public string SeasonName=>Text(Pass?["season"]?["name"]);
        public bool Owned=>(bool?)Pass?["owned"]==true;
        public bool PassAvailable=>(bool?)Pass?["enabled"]==true&&(bool?)Pass?["purchaseAvailable"]==true&&!Owned;
        public int SkrDecimals {get;private set;}
        public bool SkrConfigured {get;private set;}
        public static ShopLiveCatalog Parse(JObject body)
        {
            if(!(body?["items"] is JArray rows)||!(body["seasonPass"] is JObject pass)
                ||string.IsNullOrEmpty(Text(pass["season"]?["id"]))||Text(pass["scoringPolicy"])!="all_matches_in_season")
                throw new FormatException("Shop catalog is unavailable.");
            var token=(body["currency"] as JObject)?["skr"] as JObject;
            var result=new ShopLiveCatalog{Pass=pass,SkrDecimals=(int?)token?["decimals"]??0,
                SkrConfigured=token!=null};
            if(result.SkrDecimals<0||result.SkrDecimals>9)throw new FormatException("Invalid token precision.");
            foreach(var row in rows)if(row is JObject item&&!string.IsNullOrEmpty(Text(item["id"])))result.items[Text(item["id"])]=item;
            return result;
        }
        public string Price(string id,ShopCurrency currency)
        {
            if(currency==ShopCurrency.Swap||currency==ShopCurrency.Skr&&!SkrConfigured)return null;
            JToken amount;
            if(id=="season-pass")amount=Pass?["prices"]?[currency==ShopCurrency.Solana?"sol":"skr"];
            else amount=items.TryGetValue(id,out var item)?item[currency==ShopCurrency.Solana?"solPrice":"skrPrice"]:null;
            string text=Text(amount);
            return ValidPrice(text,currency==ShopCurrency.Solana?9:SkrDecimals)?text:null;
        }
        public bool Available(string id,ShopCurrency currency)=>Price(id,currency)!=null&&(id!="season-pass"||PassAvailable);
        public static bool ValidPrice(string value,int decimals)
        {
            if(string.IsNullOrEmpty(value)||!Regex.IsMatch(value,@"^(0|[1-9]\d{0,11})(\.\d{1,9})?$"))return false;
            int dot=value.IndexOf('.');
            return (dot<0||value.Length-dot-1<=decimals)&&decimal.TryParse(value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var amount)&&amount>0;
        }
        public static string Text(JToken token)
        {
            if(token==null||token.Type==JTokenType.Null)return null;
            if(token.Type==JTokenType.Date)return ((DateTime)token).ToUniversalTime().ToString("O",CultureInfo.InvariantCulture);
            return token.Type==JTokenType.String||token.Type==JTokenType.Integer||token.Type==JTokenType.Float?token.ToString():null;
        }
        public static string FormatAtomic(string value,int decimals)
        {
            if(decimals<0||decimals>9||string.IsNullOrEmpty(value)||!Regex.IsMatch(value,@"^\d{1,20}$"))throw new FormatException("Invalid payment amount.");
            string padded=value.PadLeft(decimals+1,'0');
            string whole=padded.Substring(0,padded.Length-decimals).TrimStart('0');if(whole.Length==0)whole="0";
            string tail=decimals>0?padded.Substring(padded.Length-decimals).TrimEnd('0'):"";
            return whole+(tail.Length>0?"."+tail:"");
        }
    }
}
