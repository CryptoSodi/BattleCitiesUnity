using System.Collections.Generic;
using UnityEngine;

namespace BattleCities
{
    // Preserve embedded artwork/material assets; tint intact and destroyed eagle instances.
    public sealed class EagleNightTint : MonoBehaviour
    {
        sealed class Surface {public Renderer Renderer;public int Slot;public Color Emission;}
        readonly List<Surface> surfaces=new List<Surface>();
        MaterialPropertyBlock properties;
        static readonly int Emission=Shader.PropertyToID("emissiveFactor");
        void Awake()
        {
            foreach(var renderer in GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                    if(materials[i].HasProperty(Emission))surfaces.Add(new Surface{Renderer=renderer,Slot=i,Emission=materials[i].GetColor(Emission)});
            }
        }
        public void SetDarkness(float darkness)
        {
            if(properties==null)properties=new MaterialPropertyBlock();
            float brightness=Mathf.Lerp(1,.28f,Mathf.Clamp01(darkness));
            foreach(var surface in surfaces)
            {
                surface.Renderer.GetPropertyBlock(properties,surface.Slot);
                Color color=surface.Emission;float alpha=color.a;color*=brightness;color.a=alpha;
                properties.SetColor(Emission,color);surface.Renderer.SetPropertyBlock(properties,surface.Slot);
            }
        }
    }
}

