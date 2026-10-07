using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCities.Core
{
    // Permanent bridge decks cut only water. The authored water region is retained,
    // so moving or resizing a bridge cannot leave a stale crossing behind.
    public static class BattleBridgeLayout
    {
        public static IEnumerable<Region> TerrainRegions(MapData map)
        {
            var decks = (map.objects ?? Array.Empty<MapObjectData>()).Where(p => p.bridge && p.role == "groundDetail").ToArray();
            foreach (var region in map.terrain?.regions ?? Array.Empty<Region>())
            {
                if (region.type != "water" || decks.Length == 0) { yield return region; continue; }
                var parts = new List<Region> { region };
                foreach (var deck in decks)
                {
                    var remaining = new List<Region>();
                    foreach (var r in parts)
                    {
                        float l = Math.Max(r.x, deck.x), t = Math.Max(r.y, deck.y);
                        float right = Math.Min(r.x+r.width, deck.x+deck.width), bottom = Math.Min(r.y+r.height, deck.y+deck.height);
                        if (l >= right || t >= bottom) { remaining.Add(r); continue; }
                        void Add(float x, float y, float w, float h)
                        { if (w > 0 && h > 0) remaining.Add(new Region { type=r.type,x=x,y=y,width=w,height=h,damage=r.damage }); }
                        Add(r.x,r.y,r.width,t-r.y); Add(r.x,bottom,r.width,r.y+r.height-bottom);
                        Add(r.x,t,l-r.x,bottom-t); Add(right,t,r.x+r.width-right,bottom-t);
                    }
                    parts = remaining;
                }
                foreach (var part in parts) yield return part;
            }
        }
    }
}
