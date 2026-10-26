using System.Collections.Generic;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    /// <summary>Player identity colours and local-team NPC recognition on painted tank surfaces.</summary>
    public static class BattleTankAppearance
    {
        static readonly Color32[] players = {
            new Color32(0x35,0xC7,0x59,255), new Color32(0xF5,0xB8,0x2E,255),
            new Color32(0x16,0x8B,0xFF,255), new Color32(0xA8,0x55,0xF7,255)
        };
        public static Color PlayerPrimary(int slot) => players[Mathf.Clamp(slot,0,players.Length-1)];
        static int NpcRelation(TankState tank,BattleSimulation simulation)
        {
            if(tank.Player||simulation==null||!simulation.IsPvp)return 0;
            return tank.Slot>=0&&simulation.AreAllies(tank.Slot,simulation.LocalPlayerSlot)?1:2;
        }
        public static Color Primary(TankState tank,BattleSimulation simulation=null)
        {
            if(tank.Player)return PlayerPrimary(tank.Slot);
            int relation=NpcRelation(tank,simulation);
            if(tank.Tier==3)return relation==2?new Color32(0x7F,0x30,0x2A,255):new Color32(0x1C,0x22,0x2C,255);
            return relation==2?new Color32(0xC5,0x53,0x43,255):new Color32(0x34,0x3A,0x46,255);
        }
        public static Color Secondary(TankState tank,BattleSimulation simulation=null)
        {
            if(!tank.Player)
            {
                int relation=NpcRelation(tank,simulation);
                if(tank.Tier==3)return relation==2?new Color32(0xA8,0x52,0x43,255):new Color32(0x3E,0x48,0x57,255);
                return relation==2?new Color32(0xE8,0x89,0x70,255):new Color32(0x68,0x73,0x84,255);
            }
            var color=Primary(tank,simulation)*.68f;color.a=1;return color;
        }
        public static Color Accent(TankState tank) => tank.Drop ? new Color32(0xFF,0x4B,0x32,255) :
            tank.Tier==1 ? new Color32(0xFF,0x9A,0x32,255) : new Color32(0xFF,0x4B,0x32,255);
        public static bool GlowingArmour(TankState tank,string material) => !tank.Player&&tank.Drop&&(material=="PrimaryPaint"||material=="SecondaryArmor");
        public static string EmissionKeyword(Material material) => material.HasProperty("emissiveFactor")?"_EMISSIVE":"_EMISSION";

        // Rebuild NPC presentation if its local ally/enemy relationship changes.
        public static int Identity(TankState tank,BattleSimulation simulation=null) => tank.Player ? 1+Mathf.Clamp(tank.Slot,0,3) : -1-Mathf.Clamp(tank.Tier,0,3)-(tank.Drop?4:0)-16*NpcRelation(tank,simulation);

        public static string Part(Renderer renderer)
        {
            // glTF may split a multi-material mesh into primitive child renderers.
            for(var node=renderer.transform;node;node=node.parent)
                if(node.name=="Barrel"||node.name=="Hatch")return node.name;
            return renderer.gameObject.name;
        }

        public static bool TrySurface(TankState tank,string part,string material,out Color color,out bool lens,BattleSimulation simulation=null)
        {
            lens=false;color=default;
            if(material=="PrimaryPaint")
            {
                // Fast-shell NPCs reuse the barrel's existing painted sleeve for a readable weapon accent.
                // DarkMetal/BareSteel mechanics and all player barrels retain their authored treatment.
                color=!tank.Player&&tank.Tier==2&&part=="Barrel"?Accent(tank):Primary(tank,simulation);return true;
            }
            if(material=="SecondaryArmor"){color=Secondary(tank,simulation);return true;}
            if(tank.Player)return false;
            if(material=="HeadlightLens")
            {
                // Existing lens geometry/emission only; the scene's spot lights remain unchanged.
                color=Accent(tank);lens=true;return true;
            }
            if(material=="AmberSignal" ||
                (material=="BareSteel" && ((tank.Drop&&part=="Hatch") || ((tank.Tier==1||tank.Tier==2)&&part=="Barrel"))))
            {color=Accent(tank);return true;}
            return false;
        }

        public static Material Surface(Material source,string part,TankState tank,Dictionary<string,Material> cache,List<Material> owned,BattleSimulation simulation=null)
        {
            if(!source||!TrySurface(tank,part,source.name,out var color,out var lens,simulation))return source;
            bool glow=GlowingArmour(tank,source.name);
            string key=source.GetEntityId()+":"+ColorUtility.ToHtmlStringRGBA(color)+":"+lens+":"+glow;
            if(cache.TryGetValue(key,out var existing)&&existing)return existing;
            // Retain the authored shader, textures, metalness, roughness, normals and keywords.
            var material=new Material(source){name=source.name};material.color=color;
            if(glow)material.EnableKeyword(EmissionKeyword(material));
            if(lens)
            {
                foreach(string property in new[]{"_EmissionColor","_EmissiveFactor","emissiveFactor"})
                {
                    if(!source.HasProperty(property))continue;
                    var original=source.GetColor(property);
                    material.SetColor(property,color*original.maxColorComponent);
                }
            }
            cache[key]=material;owned.Add(material);return material;
        }

        public static void Apply(GameObject model,TankState tank,Dictionary<string,Material> cache,List<Material> owned,BattleSimulation simulation=null)
        {
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<materials.Length;i++)
                {
                    var result=Surface(materials[i],Part(renderer),tank,cache,owned,simulation);
                    changed|=result!=materials[i];materials[i]=result;
                }
                if(changed)renderer.sharedMaterials=materials;
            }
        }
    }
}
