using System.Collections.Generic;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    /// <summary>Smooth red armour/glow pulse; never animates cached/shared materials.</summary>
    public sealed class BattleTankDropperBlink
    {
        static readonly string[] ColourProperties={"_BaseColor","baseColorFactor","_Color"};
        static readonly string[] EmissionProperties={"_EmissionColor","_EmissiveFactor","emissiveFactor"};
        sealed class Surface
        {
            public Renderer Renderer;
            public int Index;
            public readonly List<int> Properties=new List<int>();
            public readonly List<Color> Bright=new List<Color>(),Dim=new List<Color>();
        }
        readonly List<Surface> surfaces=new List<Surface>();
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        public const float Period=.6f,GlowStrength=3f;
        // Keep HDR emission predominantly red; the paint's sRGB green/blue channels wash out under tone mapping.
        public static Color GlowColour=>new Color(1f,.015f,.003f,1)*GlowStrength;
        public static float Brightness(float seconds)=>.5f+.5f*Mathf.Cos(Mathf.Repeat(seconds,Period)/Period*Mathf.PI*2);

        public BattleTankDropperBlink(GameObject model,TankState tank,BattleSimulation simulation=null)
        {
            if(tank.Player||!tank.Drop)return;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var material=materials[i];
                    if(!material||!BattleTankAppearance.TrySurface(tank,BattleTankAppearance.Part(renderer),material.name,out var colour,out var lens,simulation))continue;
                    bool armour=material.name=="PrimaryPaint"||material.name=="SecondaryArmor";
                    if(!armour&&colour!=BattleTankAppearance.Accent(tank))continue;
                    var surface=new Surface{Renderer=renderer,Index=i};
                    foreach(string property in ColourProperties)Add(property,false);
                    if(lens||armour)foreach(string property in EmissionProperties)Add(property,true);
                    if(surface.Properties.Count>0)surfaces.Add(surface);
                    void Add(string property,bool emission)
                    {
                        if(!material.HasProperty(property))return;
                        var original=material.GetColor(property);
                        Color bright=armour?(emission?GlowColour:BattleTankAppearance.Accent(tank))*(material.name=="SecondaryArmor"?.68f:1):original;
                        // Always pass through neutral armour, including red-painted PvP carriers.
                        Color dim=armour?(emission?Color.black:material.name=="SecondaryArmor"?
                            BattleTankAppearance.Secondary(tank):BattleTankAppearance.Primary(tank)):original*.15f;
                        bright.a=dim.a=original.a;
                        surface.Properties.Add(Shader.PropertyToID(property));surface.Bright.Add(bright);surface.Dim.Add(dim);
                    }
                }
            }
        }

        public void Tick(float simulationSeconds)
        {
            float brightness=Brightness(simulationSeconds);
            foreach(var surface in surfaces)
            {
                if(!surface.Renderer)continue;
                // Preserve unrelated per-renderer overrides; no new materials or scene lights.
                surface.Renderer.GetPropertyBlock(block,surface.Index);
                for(int i=0;i<surface.Properties.Count;i++)
                {
                    block.SetColor(surface.Properties[i],Color.LerpUnclamped(surface.Dim[i],surface.Bright[i],brightness));
                }
                surface.Renderer.SetPropertyBlock(block,surface.Index);
            }
        }
    }
}
