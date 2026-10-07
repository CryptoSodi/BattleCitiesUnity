namespace BattleCities.UI
{
    public enum ShopCurrency { Skr,Solana,Swap }
    public enum ShopCategory { All,Fuel,Power,Packs,SeasonPass }

    public sealed class ShopProduct
    {
        public readonly string Id,Title,InventoryId,Reward;
        public readonly int SkrPrice,Fuel;
        public readonly decimal SolPrice;
        public readonly ShopCategory Category;
        public ShopProduct(string id,string title,int skr,decimal sol,ShopCategory category,string reward,int fuel=0,string inventory=null)
        {Id=id;Title=title;SkrPrice=skr;SolPrice=sol;Category=category;Reward=reward;Fuel=fuel;InventoryId=inventory;}
    }

    public static class ShopCatalog
    {
        // Presentation catalog matches the approved shop reference and existing web catalog.
        // These displayed prices do not authorize or submit a wallet transaction.
        public static readonly ShopProduct[] Products={
            new ShopProduct("fuel-one","FUEL X1",150,.01m,ShopCategory.Fuel,"+1 FUEL",1),
            new ShopProduct("fuel-five","FUEL X5",600,.04m,ShopCategory.Fuel,"+5 FUEL",5),
            new ShopProduct("fuel-twenty","FUEL X20",1800,.12m,ShopCategory.Fuel,"+20 FUEL",20),
            new ShopProduct("shield","SHIELD",300,.02m,ShopCategory.Power,"+1 ITEM",inventory:"shield"),
            new ShopProduct("base-defence","BASE DEFENCE",375,.025m,ShopCategory.Power,"+1 ITEM",inventory:"base-defence"),
            new ShopProduct("freeze","FREEZE",450,.03m,ShopCategory.Power,"+1 ITEM",inventory:"freeze"),
            new ShopProduct("speed","SPEED",450,.03m,ShopCategory.Power,"+1 ITEM",inventory:"speed"),
            new ShopProduct("upgrade","STAR",675,.045m,ShopCategory.Power,"+1 ITEM",inventory:"upgrade"),
            new ShopProduct("zoom-out","ZOOM OUT",375,.025m,ShopCategory.Power,"+1 ITEM",inventory:"zoom-out"),
            new ShopProduct("wipeout","WIPEOUT",600,.04m,ShopCategory.Power,"+1 ITEM",inventory:"wipeout"),
            new ShopProduct("extra-life","EXTRA LIFE",525,.035m,ShopCategory.Power,"+1 ITEM",inventory:"extra-life"),
            new ShopProduct("starter-pack","STARTER PACK",1200,.08m,ShopCategory.Packs,"5 FUEL + 2 ITEMS",5),
            new ShopProduct("season-pass","SEASON PASS",0,0m,ShopCategory.SeasonPass,"CURRENT SEASON")
        };
        public static readonly string[] InventoryIds={"shield","base-defence","freeze","speed","upgrade","zoom-out","wipeout","extra-life"};
        public static string PowerupType(string id)=>id=="base-defence"?"defence":id=="zoom-out"?"zoomout":id=="extra-life"?"life":id;
    }
}
