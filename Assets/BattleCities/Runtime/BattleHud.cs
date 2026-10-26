using BattleCities.UI;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using BattleCities.Core;

namespace BattleCities
{
    // HUD skin is separate from gameplay and uses live simulation/economy values only.
    public sealed class BattleHud
    {
        public static float TopHeightPixels => BattleHudLayout.Current.TopHeightPixels;
        BattleHudLayout layout;
        float Width=>layout.Width;
        float Height=>layout.Height;
        float TopHeight=>layout.TopHeight;
        readonly Color navy=new Color(.025f,.045f,.075f,.98f),gold=new Color(1,.68f,.015f),cream=new Color(1,.94f,.78f),cyan=new Color(.3f,.9f,1);
        readonly string[] emptySlotTypes={"shield","freeze","speed","upgrade"};
        static readonly string[] psgPowerupButtons={"A","B","X","Y"};
        Canvas canvas;
        BattleFrameRateDisplay frameRate;
        readonly List<Graphic> elements=new List<Graphic>();
        int cursor;
        Font font;
        const int MinimapPixels=128;
        Texture2D player,enemy,roundedTexture,minimapTexture;
        Color32[] minimapPixels;
        int minimapTick=-1;
        Sprite roundedSprite,specialRing;
        Texture2D specialRingTexture;
        readonly BattleSpecialIconCache specialIcons=new BattleSpecialIconCache();
        void Initialize()
        {
            if(canvas)return;
            canvas=new GameObject("Battle HUD",typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            font=UI.ArcadeTextStyles.HeadingFont;
            if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            player=Resources.Load<Texture2D>("HudPlayer");enemy=Resources.Load<Texture2D>("HudEnemy");
            roundedTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="HUD rounded panel",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[64*64];
            const float radius=15f,inner=31f-radius;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float dx=Mathf.Max(Mathf.Abs(x-31.5f)-inner,0),dy=Mathf.Max(Mathf.Abs(y-31.5f)-inner,0);
                byte alpha=(byte)Mathf.RoundToInt(255*Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy)));
                pixels[y*64+x]=new Color32(255,255,255,alpha);
            }
            roundedTexture.SetPixels32(pixels);roundedTexture.Apply(false,true);
            roundedSprite=Sprite.Create(roundedTexture,new Rect(0,0,64,64),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
            specialRingTexture=new Texture2D(128,128,TextureFormat.RGBA32,false){name="Special cooldown ring",filterMode=FilterMode.Bilinear};
            var ringPixels=new Color32[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float distance=Vector2.Distance(new Vector2(x,y),new Vector2(63.5f,63.5f));
                byte alpha=(byte)(255*Mathf.Clamp01(2.5f-Mathf.Abs(distance-58)));
                ringPixels[y*128+x]=new Color32(255,255,255,alpha);
            }
            specialRingTexture.SetPixels32(ringPixels);specialRingTexture.Apply(false,true);
            specialRing=Sprite.Create(specialRingTexture,new Rect(0,0,128,128),new Vector2(.5f,.5f));
            minimapTexture=new Texture2D(MinimapPixels,MinimapPixels,TextureFormat.RGBA32,false){name="Battle tactical map",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            minimapPixels=new Color32[MinimapPixels*MinimapPixels];
            if(DevelopmentOverlays.Visible)
            {
                var fps=new GameObject("Frame rate",typeof(RectTransform),typeof(BattleFrameRateDisplay));
                fps.transform.SetParent(canvas.transform,false);
                frameRate=fps.GetComponent<BattleFrameRateDisplay>();frameRate.Initialize(roundedSprite,navy);
            }
        }
        T Element<T>(Rect rect) where T:Graphic
        {
            T element;
            // The economy panel and responsive layout can change the element order.
            // Reuse only a matching graphic type; an Image cannot be cast to Text.
            if(cursor<elements.Count&&!(elements[cursor] is T))
            {BattleVisualLifetime.Release(elements[cursor].gameObject);elements[cursor]=null;}
            if(cursor==elements.Count||!elements[cursor])
            {
                var go=new GameObject(typeof(T).Name,typeof(RectTransform));go.transform.SetParent(canvas.transform,false);
                element=go.AddComponent<T>();element.raycastTarget=false;
                if(cursor==elements.Count)elements.Add(element);else elements[cursor]=element;
            }
            else element=(T)elements[cursor];
            // Replaced pooled graphics are appended to the Canvas; restore draw order
            // so new backgrounds cannot cover labels after platform/layout changes.
            element.transform.SetSiblingIndex(cursor);
            cursor++;element.gameObject.SetActive(true);var rt=element.rectTransform;rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(rect.x,-rect.y);rt.sizeDelta=rect.size;return element;
        }
        void Fill(Rect rect,Color color){var image=Element<RawImage>(rect);image.texture=Texture2D.whiteTexture;image.uvRect=new Rect(0,0,1,1);image.color=color;}
        void Rounded(Rect rect,Color color)
        {
            var image=Element<Image>(rect);image.sprite=roundedSprite;image.type=Image.Type.Sliced;image.preserveAspect=false;image.color=color;
        }
        void RoundedFrame(Rect rect,Color border,Color fill,float thickness)
        {
            Rounded(rect,border);Rounded(new Rect(rect.x+thickness,rect.y+thickness,rect.width-thickness*2,rect.height-thickness*2),fill);
        }
        void Outline(Rect rect,Color color,float thickness)
        {
            Fill(new Rect(rect.x,rect.y,rect.width,thickness),color);Fill(new Rect(rect.x,rect.yMax-thickness,rect.width,thickness),color);
            Fill(new Rect(rect.x,rect.y+thickness,thickness,rect.height-thickness*2),color);Fill(new Rect(rect.xMax-thickness,rect.y+thickness,thickness,rect.height-thickness*2),color);
        }
        void Frame(Rect rect,Color border,Color fill,float thickness)
        {
            Fill(rect,fill);
            Fill(new Rect(rect.x,rect.y,rect.width,thickness),border);Fill(new Rect(rect.x,rect.yMax-thickness,rect.width,thickness),border);
            Fill(new Rect(rect.x,rect.y+thickness,thickness,rect.height-thickness*2),border);Fill(new Rect(rect.xMax-thickness,rect.y+thickness,thickness,rect.height-thickness*2),border);
        }
        void Text(Rect rect,string text,Color color,bool value=false,TextAnchor alignment=TextAnchor.MiddleLeft,int size=0)
        {
            var t=Element<Text>(rect);t.font=font;t.fontSize=size>0?size:value?23:16;t.fontSize=Mathf.RoundToInt(t.fontSize*ScreenContent.Number("battle-hud","font-scale",1));t.fontStyle=FontStyle.Normal;t.color=color;t.text=text;t.alignment=alignment;
            t.resizeTextForBestFit=true;t.resizeTextMaxSize=t.fontSize;t.resizeTextMinSize=Mathf.Min(t.fontSize,value?16:12);
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;
            if(UI.ArcadeTextStyles.IsWhite(color))UI.ArcadeTextStyles.ApplyWhite(t);
            else if(t.TryGetComponent<UI.ArcadeWhiteText>(out var style))style.enabled=false;
        }
        void Icon(Rect rect,Texture2D texture,bool dead=false){
            if(texture){
                float scale=Mathf.Min(rect.width/texture.width,rect.height/texture.height);
                var size=new Vector2(texture.width*scale,texture.height*scale);
                rect=new Rect(rect.center-size*.5f,size);
            }
            var image=Element<RawImage>(rect);image.texture=texture;image.uvRect=new Rect(0,0,1,1);image.color=dead?new Color(.025f,.03f,.04f,1):Color.white;
        }
        public static int PowerupIcon(string type)
        {
            switch(type)
            {
                case "shield":return 0;case "defence":return 1;case "freeze":return 2;case "life":return 3;case "upgrade":return 4;
                case "wipeout":return 5;case "speed":return 7;case "batc100":return 12;case "batc200":return 13;case "zoomout":return 14;
                default:return -1;
            }
        }
        public static bool TryPowerupUv(string type,out Rect uv)
        {
            int icon=PowerupIcon(type);
            int[] xs={24,339,660,978,1300},rights={319,640,958,1281,1597};
            int[] ys={34,338,647},bottoms={323,621,917};
            if(icon<0){uv=default;return false;}
            int col=icon%5,row=icon/5;
            uv=new Rect(xs[col]/1619f,1-bottoms[row]/971f,(rights[col]-xs[col])/1619f,(bottoms[row]-ys[row])/971f);
            return true;
        }
        void PowerupIcon(Rect rect,Texture2D atlas,string type,bool available)
        {
            var image=Element<RawImage>(rect);image.texture=atlas;
            if(atlas&&TryPowerupUv(type,out var uv)){image.uvRect=uv;image.color=available?Color.white:new Color(.72f,.76f,.8f,.78f);}
            else {image.texture=Texture2D.whiteTexture;image.uvRect=new Rect(0,0,1,1);image.color=new Color(.12f,.17f,.22f,.35f);}
        }
        void PowerupBar(EconomyClient economy,Texture2D atlas,bool pending,bool topLeft)
        {
            float scale=layout.OverlayScale,iconSize=82*scale,gap=5*scale;
            float total=iconSize*4+gap*3;
            float px=topLeft?14*scale:Width-total-14*scale;
            float py=topLeft?TopHeight+12*scale:Height-iconSize-14*scale;
            for(int i=0;i<4;i++)
            {
                string liveType=economy?economy.SlotType(i):null;int count=economy?economy.SlotCount(i):0;bool available=count>0&&!pending;string type=string.IsNullOrEmpty(liveType)?emptySlotTypes[i]:liveType;
                float x=px+i*(iconSize+gap),badge=23*scale;
                PowerupIcon(new Rect(x,py,iconSize,iconSize),atlas,type,available);
                Rounded(new Rect(x+2*scale,py+2*scale,badge,badge),new Color(.92f,.95f,.94f,.98f));
                Text(new Rect(x+2*scale,py+1*scale,badge,badge),(i+1).ToString(),new Color(.04f,.1f,.17f),false,TextAnchor.MiddleCenter,Mathf.RoundToInt(15*scale));
                Rounded(new Rect(x+iconSize-badge-2*scale,py+2*scale,badge,badge),new Color(.015f,.045f,.08f,.94f));
                Text(new Rect(x+iconSize-badge-2*scale,py+1*scale,badge,badge),pending?"…":count.ToString(),available?new Color(1,.78f,.03f):new Color(.74f,.79f,.82f),false,TextAnchor.MiddleCenter,Mathf.RoundToInt(16*scale));
            }
        }
        void WebPowerups(EconomyClient economy,Texture2D atlas,bool pending)
        {
            float slot=BattleHudLayout.AndroidPowerupSlotWidth,gap=BattleHudLayout.AndroidPowerupGap,size=BattleHudLayout.AndroidPowerupIconSize;
            float left=Width-6-(slot*4+gap*3),y=(TopHeight-size)*.5f;
            for(int i=0;i<4;i++)
            {
                string liveType=economy?economy.SlotType(i):null;
                string type=string.IsNullOrEmpty(liveType)?emptySlotTypes[i]:liveType;
                int count=economy?economy.SlotCount(i):0;bool available=count>0&&!pending;
                float x=left+i*(slot+gap)+(slot-size)*.5f;
                PowerupIcon(new Rect(x,y,size,size),atlas,type,available);
                elements[cursor-1].color=new Color(1,1,1,available?.3f:.15f);
                Rounded(new Rect(x,y,14,14),new Color(.92f,.95f,.94f,.98f));
                Text(new Rect(x,y,14,14),(i+1).ToString(),new Color(.04f,.1f,.17f),false,TextAnchor.MiddleCenter,11);
                Rounded(new Rect(x+size-12,y-2,18,18),new Color(.015f,.045f,.08f,.94f));
                Text(new Rect(x+size-14,y-3,22,20),pending?"...":count.ToString(),Color.white,false,TextAnchor.MiddleCenter,14);
            }
        }
        float Psg1PowerupWidth(bool narrow)
        {
            float scale=layout.DetailScale,size=(narrow?34:38)*scale,gap=3*scale;
            return size*4+gap*3+14*scale;
        }
        void Psg1Powerups(EconomyClient economy,Texture2D atlas,bool pending,bool narrow,int selectedSlot,float trailingWidth)
        {
            float scale=layout.DetailScale,size=(narrow?34:38)*scale,gap=3*scale;
            float total=size*4+gap*3,x=Width-total-6*scale-trailingWidth,y=5,badge=14*scale;
            Fill(new Rect(x-8*scale,9,1,30),gold);
            for(int i=0;i<4;i++)
            {
                string liveType=economy?economy.SlotType(i):null;int count=economy?economy.SlotCount(i):0;bool available=count>0&&!pending;string type=string.IsNullOrEmpty(liveType)?emptySlotTypes[i]:liveType;
                float px=x+i*(size+gap);
                PowerupIcon(new Rect(px,y,size,size),atlas,type,available);
                if(i==selectedSlot)Outline(new Rect(px-2*scale,y-2*scale,size+4*scale,size+4*scale),gold,2*scale);
                Rounded(new Rect(px+scale,y+scale,badge,badge),new Color(.92f,.95f,.94f,.98f));
                Text(new Rect(px+scale,y,badge,badge),psgPowerupButtons[i],new Color(.04f,.1f,.17f),false,TextAnchor.MiddleCenter,Mathf.RoundToInt(9*scale));
                Rounded(new Rect(px+size-badge-scale,y+scale,badge,badge),new Color(.015f,.045f,.08f,.94f));
                Text(new Rect(px+size-badge-scale,y,badge,badge),pending?"...":count.ToString(),available?new Color(1,.78f,.03f):new Color(.74f,.79f,.82f),false,TextAnchor.MiddleCenter,Mathf.RoundToInt(9*scale));
            }
        }
        void PaintMapRect(BattleSimulation state,Box box,Color32 color)
        {
            int x0=Mathf.Clamp(Mathf.FloorToInt(box.X/state.Width*MinimapPixels),0,MinimapPixels-1);
            int x1=Mathf.Clamp(Mathf.CeilToInt(box.Right/state.Width*MinimapPixels),x0+1,MinimapPixels);
            int y0=Mathf.Clamp(MinimapPixels-Mathf.CeilToInt(box.Bottom/state.Height*MinimapPixels),0,MinimapPixels-1);
            int y1=Mathf.Clamp(MinimapPixels-Mathf.FloorToInt(box.Y/state.Height*MinimapPixels),y0+1,MinimapPixels);
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)minimapPixels[y*MinimapPixels+x]=color;
        }
        void UpdateMinimap(BattleSimulation state)
        {
            if(minimapTick==state.Tick)return;minimapTick=state.Tick;
            var background=new Color32(9,24,34,92);for(int i=0;i<minimapPixels.Length;i++)minimapPixels[i]=background;
            foreach(var wall in state.Terrain)
            {
                if(!wall.Alive)continue;Color32 color;
                if(wall.Brick)color=new Color32(224,93,42,255);
                else if(wall.Type=="steel")color=new Color32(174,194,204,255);
                else if(wall.Type=="water")color=new Color32(21,143,207,255);
                else if(wall.Type=="ice")color=new Color32(132,202,226,255);
                else if(wall.Type==BattleTerrain.Lava)color=new Color32(247,76,15,255);
                else if(wall.Type==BattleTerrain.MuddyWater)color=new Color32(114,91,45,255);
                else if(wall.Type==BattleTerrain.Quicksand)color=new Color32(211,164,78,255);
                else if(wall.Type==BattleTerrain.Grease)color=new Color32(49,47,64,255);
                else if(wall.Type=="jungle")color=new Color32(54,133,58,255);
                else continue;
                PaintMapRect(state,wall.Bounds,color);
            }
            PaintMapRect(state,state.BaseBounds,state.BaseAlive?new Color32(255,190,35,255):new Color32(55,55,55,255));
            if(state.IsTeamBattle)PaintMapRect(state,state.RivalBaseBounds,state.RivalBaseAlive?new Color32(79,199,109,255):new Color32(55,55,55,255));
            if(state.PickupType!=null)PaintMapRect(state,new Box(state.PickupX-10,state.PickupY-10,20,20),new Color32(255,225,70,255));
            foreach(var tank in state.Tanks)if(tank.Alive)PaintMapRect(state,new Box(tank.X-13,tank.Y-13,26,26),state.IsTeamBattle?(BattleSimulation.TeamForSlot(tank.Slot)==0?new Color32(255,210,35,255):new Color32(79,199,109,255)):tank.Player?new Color32(255,210,35,255):new Color32(244,65,55,255));
            if(state.IsCaptureFlag)for(int team=0;team<2;team++)
            {
                var flag=state.Flags[team];
                PaintMapRect(state,new Box(flag.X-19,flag.Y-19,38,38),new Color32(255,255,255,255));
                PaintMapRect(state,new Box(flag.X-12,flag.Y-12,24,24),team==0?new Color32(255,210,35,255):new Color32(79,199,109,255));
            }
            minimapTexture.SetPixels32(minimapPixels);minimapTexture.Apply(false,false);
        }
        void Minimap(BattleSimulation state)
        {
            UpdateMinimap(state);float scale=layout.OverlayScale,size=142*scale;
            float top=TopHeight,x=Width-size-14*scale,y=top+12*scale;
            var map=Element<RawImage>(new Rect(x,y,size,size));map.texture=minimapTexture;map.uvRect=new Rect(0,0,1,1);map.color=new Color(1,1,1,.88f);
            Outline(new Rect(x,y,size,size),new Color(1,.68f,.05f,.86f),1.5f*scale);
        }
        void SpecialIndicator(BattleSimulation state,SecondaryAttack special,bool belowMap=false)
        {
            if(!BattleSpecialAbilities.Valid(special))return;
            float scale=belowMap?1:layout.DetailScale,size=belowMap?60:(layout.Narrow?34:38)*scale;
            float x=belowMap?Width-14*layout.OverlayScale-size:Width-6*scale-size;
            float y=belowMap?TopHeight+154*layout.OverlayScale+12:5,unit=size/82f;
            bool cooling=state.SecondaryCooldown>0,limited=state.SecondaryCountFor(special)>=state.SecondaryLimitFor(special);
            float duration=special==SecondaryAttack.Mine?BattleSimulation.SecondaryCooldownSeconds:4f;
            float progress=Mathf.Clamp01(1-state.SecondaryCooldown/duration);
            Color accent=limited?new Color(1,.32f,.16f):cooling?cyan:gold;
            float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*3);
            RoundedFrame(new Rect(x,y,size,size),new Color(.07f,.34f,.64f),navy,1.2f*scale);
            Ring(new Rect(x+unit,y+unit,80*unit,80*unit),new Color(accent.r,accent.g,accent.b,cooling?.10f:.12f+pulse*.16f),1);
            Ring(new Rect(x+4*unit,y+4*unit,74*unit,74*unit),new Color(.12f,.22f,.32f),1);
            var icon=Element<RawImage>(new Rect(x+7*unit,y+7*unit,68*unit,68*unit));
            icon.texture=specialIcons.Get(special);icon.uvRect=new Rect(0,0,1,1);
            icon.color=cooling||limited?new Color(.6f,.65f,.72f,.7f):Color.white;
            Ring(new Rect(x+4*unit,y+4*unit,74*unit,74*unit),accent,limited?1:progress);
            if(!belowMap)
            {
                var badge=new Rect(x+scale,y,20*scale,14*scale);
                Rounded(badge,new Color(.92f,.95f,.94f,.98f));
                Text(badge,"L1",new Color(.04f,.1f,.17f),false,TextAnchor.MiddleCenter,Mathf.RoundToInt(10*scale));
            }
        }
        void Ring(Rect rect,Color color,float progress)
        {
            var image=Element<Image>(rect);image.sprite=specialRing;image.color=color;
            image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Radial360;
            image.fillOrigin=(int)Image.Origin360.Top;image.fillClockwise=true;image.fillAmount=progress;
        }
        public void Draw(BattleSimulation state,EconomyClient economy,Texture2D powerupAtlas,bool consumePending,int selectedSlot=0,bool showPowerups=true,SecondaryAttack? special=null)
            => DrawLayout(state,economy,powerupAtlas,consumePending,BattleHudLayout.Current,RuntimePlatformInfo.PowerupUi,selectedSlot,showPowerups,special);
#if UNITY_EDITOR
        public void DrawPreview(BattleSimulation state,Texture2D atlas,BattleHudLayout preview,PowerupUiMode mode,EconomyClient economy=null)
            => DrawLayout(state,economy,atlas,false,preview,mode,0,true,state.EquippedSecondary);
#endif
        void DrawLayout(BattleSimulation state,EconomyClient economy,Texture2D powerupAtlas,bool consumePending,BattleHudLayout viewport,PowerupUiMode powerupUi,int selectedSlot,bool showPowerups,SecondaryAttack? special)
        {
            Initialize();layout=viewport;canvas.scaleFactor=layout.Scale;if(frameRate)frameRate.ApplyLayout(layout,powerupUi==PowerupUiMode.ExternalController||powerupUi==PowerupUiMode.WebTopLeft);cursor=0;bool narrow=layout.Narrow;float h=TopHeight;
            bool psg1=powerupUi==PowerupUiMode.MergedTopHud;
            bool android=powerupUi==PowerupUiMode.ExternalController;
            bool web=powerupUi==PowerupUiMode.WebTopLeft;
            bool cornerSlots=android||web;
            bool compact=psg1||cornerSlots;
            bool hasSpecial=psg1&&special.HasValue&&BattleSpecialAbilities.Valid(special.Value);
            float specialWidth=hasSpecial?((narrow?34:38)+3)*layout.DetailScale:0;
            Fill(new Rect(0,0,Width,h),navy);Fill(new Rect(0,0,Width,2),new Color(.22f,.3f,.38f));Fill(new Rect(0,h-3,Width,3),gold);
            float metricWidth=narrow?Mathf.Min(124,Width*.52f/3):175;
            float scoreWidth=cornerSlots&&narrow?112:compact?(narrow?metricWidth:138):metricWidth;
            float timeWidth=cornerSlots&&narrow?72:compact?Mathf.Min(metricWidth,82):metricWidth;
            float livesWidth=cornerSlots&&narrow?88:compact?(narrow?metricWidth:136):metricWidth;
            float scoreTextX=cornerSlots&&narrow?34:46,scoreIcon=cornerSlots&&narrow?26:36;
            Icon(new Rect(5,(h-scoreIcon)*.5f,scoreIcon,scoreIcon),player);Text(new Rect(scoreTextX,2,scoreWidth-scoreTextX-8,18),ScreenContent.Text("battle-hud","score-a60ffb","SCORE"),cream);Text(new Rect(scoreTextX,18,scoreWidth-scoreTextX-8,27),state.Score.ToString("D6"),gold,true);
            float x=scoreWidth;
            Fill(new Rect(x-7,9,1,28),gold);
            int seconds=state.Tick/60;
            Text(new Rect(x+4,2,timeWidth-8,18),ScreenContent.Text("battle-hud","time-588867","TIME"),cream);Text(new Rect(x+4,18,timeWidth-8,27),(seconds/60).ToString("D2")+":"+(seconds%60).ToString("D2"),cyan,true);
            x+=timeWidth;Fill(new Rect(x-7,9,1,28),gold);
            Text(new Rect(x+4,2,livesWidth-8,18),state.IsCaptureFlag?ScreenContent.Text("battle-hud","respawns","RESPAWNS"):ScreenContent.Text("battle-hud","lives","LIVES"),cream);Text(new Rect(x+4,18,32,27),state.IsCaptureFlag?"∞":state.Lives.ToString(),gold,true);
            float lifeStep=Mathf.Min(30,(livesWidth-36)/3);
            for(int i=0;i<(state.IsCaptureFlag?0:3);i++)Icon(new Rect(x+32+i*lifeStep,18,lifeStep-2,25),player,i>=state.Lives);
            float enemyX=scoreWidth+timeWidth+livesWidth+4,enemyY=4;
            Fill(new Rect(enemyX-11,9,1,28),gold);
            int opponents=0;
            if(state.IsPvp)for(int i=0;i<BattleSimulation.MaxPlayers;i++)if(!state.AreAllies(i,state.LocalPlayerSlot)&&state.Participants[i].Connected&&state.Participants[i].Lives>0)opponents++;
            Text(new Rect(enemyX,enemyY,100,19),state.IsPvp?ScreenContent.Text("battle-hud","rivals","RIVALS"):ScreenContent.Text("battle-hud","enemies","ENEMIES"),cream);
            Text(new Rect(enemyX,enemyY+18,100,23),(state.IsPvp?opponents:state.Remaining).ToString()+" LEFT",new Color(1,.42f,.32f));
            float start=enemyX+(cornerSlots&&narrow?66:compact?76:100),reserved=psg1?Psg1PowerupWidth(narrow)+specialWidth:0;
            if(web&&showPowerups&&!state.IsMultiplayer)reserved=layout.AndroidPowerupWidth;
            if(android&&showPowerups&&!state.IsMultiplayer&&powerupAtlas){
                // Touch slots have fixed positions. Reserve only the occupied span, as empty slots are hidden.
                for(int i=0;i<4;i++)if(economy&&TryPowerupUv(economy.SlotType(i),out _)){
                    reserved=(4-i)*(BattleHudLayout.AndroidPowerupSlotWidth+BattleHudLayout.AndroidPowerupGap)+8;break;
                }
            }
            if(android)reserved+=Mathf.Max(0,Screen.width-Screen.safeArea.xMax)/layout.Scale;
            float available=Mathf.Max(1,Width-start-10-reserved);
            if(state.IsCaptureFlag)
            {
                Text(new Rect(start,enemyY,available,22),"FLAGS  YELLOW "+state.FlagScores[0]+" : "+state.FlagScores[1]+" GREEN",gold,true);
                Text(new Rect(start,enemyY+22,available,19),ScreenContent.Text("battle-hud","first-to-3-own-flag-must-be-home-57a971","FIRST TO 3 · OWN FLAG MUST BE HOME"),cream);
            }
            int total=state.IsCaptureFlag?0:state.TotalEnemies,deadCount=total-state.Remaining;
            float step=Mathf.Min(cornerSlots?48:compact?40:34,available/Mathf.Max(1,total));
            for(int i=0;i<total;i++)
            {
                float iconWidth=Mathf.Max(1,step-(android&&step<20?.5f:2));
                float iconHeight=enemy?Mathf.Min(32,iconWidth*enemy.height/enemy.width):32;
                var slot=new Rect(start+i*step,enemyY+4+(32-iconHeight)*.5f,iconWidth,iconHeight);
                Fill(new Rect(slot.x,enemyY+7,slot.width,26),new Color(.1f,.15f,.2f,.6f));
                Icon(slot,enemy,i<deadCount);
            }
            Minimap(state);
            if((psg1||web)&&special.HasValue)SpecialIndicator(state,special.Value,web);
            if(showPowerups&&!state.IsMultiplayer)
            {
                if(psg1)Psg1Powerups(economy,powerupAtlas,consumePending,narrow,selectedSlot,specialWidth);
                else if(web)WebPowerups(economy,powerupAtlas,consumePending);
                else if(powerupUi==PowerupUiMode.GameOverlay)PowerupBar(economy,powerupAtlas,consumePending,false);
            }
            while(cursor<elements.Count)elements[cursor++].gameObject.SetActive(false);
        }
        public void SetVisible(bool visible){if(canvas)canvas.gameObject.SetActive(visible);}
        public void Dispose(){specialIcons.Dispose();if(specialRing)BattleVisualLifetime.Release(specialRing);if(specialRingTexture)BattleVisualLifetime.Release(specialRingTexture);if(canvas)BattleVisualLifetime.Release(canvas.gameObject);if(roundedSprite)BattleVisualLifetime.Release(roundedSprite);if(roundedTexture)BattleVisualLifetime.Release(roundedTexture);if(minimapTexture)BattleVisualLifetime.Release(minimapTexture);}
    }
}
