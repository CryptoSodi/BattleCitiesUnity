using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BattleCities.UI
{
    public enum ArcadeTextTreatment { Gold, Navy, WhiteButton }

    /// <summary>Shared material treatments for the menu's Barlow Condensed Bold lettering.</summary>
    public sealed class ArcadeTextStyles : IDisposable
    {
        public static Color32 GoldTop => new Color32(255,235,112,255);
        public static Color32 GoldBottom => new Color32(255,183,12,255);
        public static Color32 GoldOutline => new Color32(52,31,9,255);
        public static Color32 WhiteOutline => new Color32(6,33,64,255);
        public static Color32 NavyFace => new Color32(7,43,94,255);
        public static Color32 TextShadow => new Color32(5,18,36,220);
        static ArcadeTypography typography;
        static ArcadeTypography Typography=>typography?typography:typography=Resources.Load<ArcadeTypography>("ArcadeTypography");
        public static TMP_FontAsset HeadingSdf=>Typography?Typography.headingSdf:null;
        public static Font HeadingFont=>Typography?Typography.headingFont:null;
        public static bool IsWhite(Color color)=>color.r>=.94f&&color.g>=.94f&&color.b>=.94f;

        readonly Dictionary<(TMP_FontAsset,ArcadeTextTreatment),Material> materials=
            new Dictionary<(TMP_FontAsset,ArcadeTextTreatment),Material>();

        public void Apply(TMP_Text label,ArcadeTextTreatment treatment)
        {
            if(label&&HeadingSdf)label.font=HeadingSdf;
            if(!label||!label.font||!label.font.material)return;
            bool gold=treatment==ArcadeTextTreatment.Gold;
            bool outlined=gold||treatment==ArcadeTextTreatment.WhiteButton;
            var key=(label.font,treatment);
            if(!materials.TryGetValue(key,out var material)||!material)
            {
                material=new Material(label.font.material)
                {
                    name="Battle Cities "+treatment+" Text",
                    hideFlags=HideFlags.DontSave
                };
                material.SetColor("_FaceColor",Color.white);
                material.SetFloat("_FaceDilate",gold?.035f:outlined?.045f:.025f);
                material.SetFloat("_OutlineWidth",gold?.28f:outlined?.24f:0f);
                material.SetFloat("_OutlineSoftness",0f);
                if(outlined)
                {
                    material.EnableKeyword("OUTLINE_ON");
                    material.EnableKeyword("UNDERLAY_ON");
                    material.SetColor("_OutlineColor",gold?GoldOutline:WhiteOutline);
                    material.SetColor("_UnderlayColor",TextShadow);
                    material.SetFloat("_UnderlayOffsetX",gold?.25f:.15f);
                    material.SetFloat("_UnderlayOffsetY",gold?-.65f:-.50f);
                    material.SetFloat("_UnderlayDilate",gold?.06f:.03f);
                    material.SetFloat("_UnderlaySoftness",gold?.06f:.04f);
                }
                else
                {
                    material.DisableKeyword("OUTLINE_ON");
                    material.DisableKeyword("UNDERLAY_ON");
                }
                materials[key]=material;
            }
            label.fontSharedMaterial=material;
            label.fontStyle|=FontStyles.Bold;
            label.color=treatment==ArcadeTextTreatment.Navy?NavyFace:Color.white;
            label.colorGradientPreset=null;
            label.enableVertexGradient=gold;
            if(gold)
            {
                Color top=GoldTop,bottom=GoldBottom;
                label.colorGradient=new VertexGradient(top,top,bottom,bottom);
            }
            label.extraPadding=true;
            label.UpdateMeshPadding();
        }

        public static void ApplyGold(UnityEngine.UI.Text label,Font headingFont)
        {
            if(!label)return;
            if(headingFont)label.font=headingFont;
            label.fontStyle=FontStyle.Bold;
            label.color=Color.white;
            // The shared effect supplies both the outline and shadow.
            foreach(var shadow in label.GetComponents<UnityEngine.UI.Shadow>())shadow.enabled=false;
            var white=label.GetComponent<ArcadeWhiteText>();
            if(white)white.enabled=false;
            var effect=label.GetComponent<ArcadeGoldText>();
            if(!effect)effect=label.gameObject.AddComponent<ArcadeGoldText>();
            effect.enabled=true;
        }

        public static void ApplyWhite(UnityEngine.UI.Text label,Font headingFont=null)
        {
            if(!label)return;
            if(!headingFont)headingFont=HeadingFont;
            if(headingFont)label.font=headingFont;
            label.fontStyle=FontStyle.Bold;
            label.color=new Color(1f,1f,1f,label.color.a);
            foreach(var shadow in label.GetComponents<UnityEngine.UI.Shadow>())shadow.enabled=false;
            var gold=label.GetComponent<ArcadeGoldText>();
            if(gold)gold.enabled=false;
            var effect=label.GetComponent<ArcadeWhiteText>();
            if(!effect)effect=label.gameObject.AddComponent<ArcadeWhiteText>();
            effect.enabled=true;
        }

        public static void ApplyNavy(UnityEngine.UI.Text label,Font headingFont=null)
        {
            if(!label)return;
            if(!headingFont)headingFont=HeadingFont;
            if(headingFont)label.font=headingFont;
            label.fontStyle=FontStyle.Bold;
            label.color=NavyFace;
            var gold=label.GetComponent<ArcadeGoldText>();
            if(gold)gold.enabled=false;
            var white=label.GetComponent<ArcadeWhiteText>();
            if(white)white.enabled=false;
            foreach(var shadow in label.GetComponents<UnityEngine.UI.Shadow>())shadow.enabled=false;
            var outline=label.GetComponent<UnityEngine.UI.Outline>();
            if(!outline)outline=label.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor=new Color32(6,33,64,210);
            outline.effectDistance=new Vector2(.4f,-.4f);
            outline.useGraphicAlpha=true;
            outline.enabled=true;
        }

        public static void ApplyWhiteLabels(Transform root,Font headingFont=null)
        {
            if(!root)return;
            foreach(var label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                var gold=label.GetComponent<ArcadeGoldText>();
                if(IsWhite(label.color)&&!(gold&&gold.enabled))ApplyWhite(label,headingFont);
            }
        }

        public void Dispose()
        {
            foreach(var material in materials.Values)
                if(material)
                {
                    if(Application.isPlaying)UnityEngine.Object.Destroy(material);
                    else UnityEngine.Object.DestroyImmediate(material);
                }
            materials.Clear();
        }
    }
}
