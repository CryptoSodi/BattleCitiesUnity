using UnityEngine;

namespace BattleCities.UI
{
    public sealed class ShopArt : ScriptableObject
    {
        public Sprite supplyCrate,skr,solana,swap;
        // ShopCatalog order; inventory rows reuse matching power-up artwork with an atlas fallback.
        public Sprite[] products=new Sprite[12];
        // Normalized visible bounds, measured from the PNG's top-left corner.
        public Rect[] productVisibleBounds=new Rect[12];
    }
}
