using System.Collections.Generic;
using UnityEngine;

namespace BattleCities
{
    // Shared by deployed models and their isolated HUD previews.
    internal static class BattleDeployableAppearance
    {
        public static void Apply(GameObject root,int ownerSlot,Dictionary<string,Material> cache,List<Material> owned)
        {
            var color=BattleTankAppearance.PlayerPrimary(ownerSlot);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<materials.Length;i++)
                {
                    var source=materials[i];if(!source)continue;
                    bool shadow=source.name=="MAT_Armor_Yellow_Shadow"||source.name=="Armor_Shade";
                    bool painted=shadow||source.name=="Yellow enamel"||source.name=="Warm enamel"||
                        source.name=="Golden side armor"||source.name=="Upper yellow enamel"||source.name=="Warm yellow enamel"||
                        source.name=="MAT_Armor_Yellow"||source.name=="Armor_Yellow";
                    if(!painted)continue;
                    string key=source.GetEntityId()+":"+ColorUtility.ToHtmlStringRGBA(color);
                    if(!cache.TryGetValue(key,out var material))
                    {
                        var paint=shadow?color*.68f:color;
                        material=new Material(source){color=new Color(paint.r,paint.g,paint.b,1)};
                        cache.Add(key,material);owned.Add(material);
                    }
                    materials[i]=material;changed=true;
                }
                if(changed)renderer.sharedMaterials=materials;
            }
        }
    }
}
