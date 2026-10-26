using BattleCities.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities
{
    public sealed partial class BattleTouchControls
    {
        sealed class SpecialButton
        {
            public SecondaryAttack Ability;
            public RectTransform Rect;
            public Button Button;
            public RawImage Icon;
            public Image Halo, Ring;
        }
        readonly SpecialButton[] specialButtons=new SpecialButton[4];
        readonly BattleSpecialIconCache specialIcons=new BattleSpecialIconCache();
        Texture2D specialRingTexture;
        Sprite specialRing;

        void BuildSpecialButtons()
        {
            specialRingTexture=new Texture2D(128,128,TextureFormat.RGBA32,false){name="Android special cooldown ring",filterMode=FilterMode.Bilinear};
            var pixels=new Color32[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float distance=Vector2.Distance(new Vector2(x,y),new Vector2(63.5f,63.5f));
                pixels[y*128+x]=new Color32(255,255,255,(byte)(255*Mathf.Clamp01(2.5f-Mathf.Abs(distance-58))));
            }
            specialRingTexture.SetPixels32(pixels);specialRingTexture.Apply(false,true);
            specialRing=Sprite.Create(specialRingTexture,new UnityEngine.Rect(0,0,128,128),new Vector2(.5f,.5f));
            for(int i=0;i<specialButtons.Length;i++)
            {
                var ability=(SecondaryAttack)((int)SecondaryAttack.Mine+i);
                var rect=Rect("Special "+ability,safeRoot,Vector2.one,Vector2.zero,Vector2.one*60);
                var surface=Surface(rect,blue,true,true);
                Surface(Rect("Inset",rect,Vector2.one*.5f,Vector2.zero,Vector2.one*56),navy);
                var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=surface;
                button.navigation=new Navigation{mode=Navigation.Mode.None};button.transition=Selectable.Transition.None;
                button.onClick.AddListener(()=>game.DeployTouchSpecial(ability));
                var halo=SpecialRing("Glow",rect,60,new Color(1,.73f,.1f,.2f));
                SpecialRing("Track",rect,54,new Color(.12f,.22f,.32f));
                var iconRect=Rect("Icon",rect,Vector2.one*.5f,Vector2.zero,Vector2.one*50);
                var icon=iconRect.gameObject.AddComponent<RawImage>();icon.raycastTarget=false;
                var ring=SpecialRing("Cooldown",rect,54,gold);
                specialButtons[i]=new SpecialButton{Ability=ability,Rect=rect,Button=button,Icon=icon,Halo=halo,Ring=ring};
                rect.gameObject.SetActive(false);
            }
        }
        Image SpecialRing(string name,Transform parent,float size,Color color)
        {
            var image=Rect(name,parent,Vector2.one*.5f,Vector2.zero,Vector2.one*size).gameObject.AddComponent<Image>();
            image.sprite=specialRing;image.color=color;image.raycastTarget=false;
            image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Radial360;
            image.fillOrigin=(int)Image.Origin360.Top;image.fillClockwise=true;return image;
        }
        void RefreshSpecialButtons(float screenWidth,float screenHeight,UnityEngine.Rect safe,bool visible)
        {
            float pixelWidth=canvas.pixelRect.width>0?canvas.pixelRect.width:screenWidth;
            float pixelHeight=canvas.pixelRect.height>0?canvas.pixelRect.height:screenHeight;
            var layout=BattleHudLayout.ForViewport(pixelWidth,pixelHeight);
            float scale=layout.Scale/Mathf.Max(.001f,canvas.scaleFactor),overlay=layout.OverlayScale;
            float safeTop=(1-safe.yMax/screenHeight)*pixelHeight/Mathf.Max(.001f,canvas.scaleFactor);
            var state=game.Simulation;
            int index=0;
            foreach(var item in specialButtons)
            {
                bool shown=state!=null&&!game.IsReplaying&&game.TouchOwnsSpecial(item.Ability);
                item.Rect.gameObject.SetActive(shown);if(!shown)continue;
                // Share the minimap's right inset; two owned abilities form a horizontal row below it.
                float size=60;
                item.Rect.anchoredPosition=new Vector2(-(14*overlay+size*.5f+index*(size+8))*scale,
                    -(layout.TopHeight+12*overlay+142*overlay+12+size*.5f)*scale+safeTop);
                item.Rect.localScale=Vector3.one*scale;
                bool cooling=state.SecondaryCooldown>0;
                bool limited=state.SecondaryCountFor(item.Ability)>=state.SecondaryLimitFor(item.Ability);
                bool ready=visible&&game.TouchSpecialAvailable(item.Ability);
                item.Button.interactable=ready;
                item.Icon.texture=specialIcons.Get(item.Ability,state.LocalPlayerSlot);
                item.Icon.color=!item.Icon.texture?Color.clear:cooling||limited?new Color(.6f,.65f,.72f,.7f):Color.white;
                Color accent=limited?new Color(1,.32f,.16f):cooling?new Color(.1f,.84f,1):gold;
                item.Ring.color=accent;
                // Cooldown is shared between specials; use the last deployed ability's duration.
                float duration=game.SelectedSpecial==SecondaryAttack.Mine?BattleSimulation.SecondaryCooldownSeconds:4;
                item.Ring.fillAmount=limited?1:Mathf.Clamp01(1-state.SecondaryCooldown/duration);
                float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*3);
                item.Halo.color=new Color(accent.r,accent.g,accent.b,cooling?.1f:.12f+pulse*.16f);
                index++;
            }
        }
        void DisposeSpecialButtons()
        {
            specialIcons.Dispose();
            if(specialRing)BattleVisualLifetime.Release(specialRing);
            if(specialRingTexture)BattleVisualLifetime.Release(specialRingTexture);
        }
    }
}
