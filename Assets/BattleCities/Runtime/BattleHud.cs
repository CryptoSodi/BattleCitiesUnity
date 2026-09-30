using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using BattleCities.Core;

namespace BattleCities
{
    // HUD skin is separate from gameplay and uses live simulation/economy values only.
    public sealed class BattleHud
    {
        public static float TopHeightPixels => Screen.width<920?84:48;
        readonly Color navy=new Color(.025f,.045f,.075f,.98f),gold=new Color(1,.68f,.015f),cream=new Color(1,.94f,.78f),cyan=new Color(.3f,.9f,1);
        readonly string[] emptySlotTypes={"shield","freeze","speed","upgrade"};
        Canvas canvas;
        readonly List<Graphic> elements=new List<Graphic>();
        int cursor;
        Font font;
        const int MinimapPixels=128;
        Texture2D player,enemy,roundedTexture,minimapTexture;
        Color32[] minimapPixels;
        int minimapTick=-1;
        Sprite roundedSprite;
        void Initialize()
        {
            if(canvas)return;
            canvas=new GameObject("Battle HUD",typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Bahnschrift","Arial"},23);
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
            minimapTexture=new Texture2D(MinimapPixels,MinimapPixels,TextureFormat.RGBA32,false){name="Battle tactical map",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            minimapPixels=new Color32[MinimapPixels*MinimapPixels];
        }
        T Element<T>(Rect rect) where T:Graphic
        {
            T element;
            // The economy panel and responsive layout can change the element order.
            // Reuse only a matching graphic type; an Image cannot be cast to Text.
            if(cursor<elements.Count&&!(elements[cursor] is T))
            {Object.Destroy(elements[cursor].gameObject);elements[cursor]=null;}
            if(cursor==elements.Count||!elements[cursor])
            {
                var go=new GameObject(typeof(T).Name,typeof(RectTransform));go.transform.SetParent(canvas.transform,false);
                element=go.AddComponent<T>();element.raycastTarget=false;
                if(cursor==elements.Count)elements.Add(element);else elements[cursor]=element;
            }
            else element=(T)elements[cursor];
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
            var t=Element<Text>(rect);t.font=font;t.fontSize=size>0?size:value?23:16;t.fontStyle=FontStyle.Bold;t.color=color;t.text=text;t.alignment=alignment;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;
        }
        void Icon(Rect rect,Texture2D texture,bool dead=false){var image=Element<RawImage>(rect);image.texture=texture;image.uvRect=new Rect(0,0,1,1);image.color=dead?new Color(.025f,.03f,.04f,1):Color.white;}
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
            float scale=Mathf.Clamp(Screen.width/1240f,.78f,1.15f),iconSize=82*scale,gap=5*scale;
            float total=iconSize*4+gap*3;
            float px=topLeft?14*scale:Screen.width-total-14*scale;
            float py=topLeft?TopHeightPixels+12*scale:Screen.height-iconSize-14*scale;
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
        float Psg1PowerupWidth(bool narrow)
        {
            float scale=Mathf.Clamp(Screen.width/1240f,.78f,1.15f),size=(narrow?34:38)*scale,gap=3*scale;
            return size*4+gap*3+14*scale;
        }
        void Psg1Powerups(EconomyClient economy,Texture2D atlas,bool pending,bool narrow,int selectedSlot)
        {
            float scale=Mathf.Clamp(Screen.width/1240f,.78f,1.15f),size=(narrow?34:38)*scale,gap=3*scale;
            float total=size*4+gap*3,x=Screen.width-total-6*scale,y=narrow?46:5,badge=14*scale;
            Fill(new Rect(x-8*scale,narrow?48:9,1,narrow?28:30),gold);
            for(int i=0;i<4;i++)
            {
                string liveType=economy?economy.SlotType(i):null;int count=economy?economy.SlotCount(i):0;bool available=count>0&&!pending;string type=string.IsNullOrEmpty(liveType)?emptySlotTypes[i]:liveType;
                float px=x+i*(size+gap);
                PowerupIcon(new Rect(px,y,size,size),atlas,type,available);
                if(i==selectedSlot)Outline(new Rect(px-2*scale,y-2*scale,size+4*scale,size+4*scale),gold,2*scale);
                Rounded(new Rect(px+scale,y+scale,badge,badge),new Color(.92f,.95f,.94f,.98f));
                Text(new Rect(px+scale,y,badge,badge),(i+1).ToString(),new Color(.04f,.1f,.17f),false,TextAnchor.MiddleCenter,Mathf.RoundToInt(9*scale));
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
                else if(wall.Type=="jungle")color=new Color32(54,133,58,255);
                else continue;
                PaintMapRect(state,wall.Bounds,color);
            }
            PaintMapRect(state,state.BaseBounds,state.BaseAlive?new Color32(255,190,35,255):new Color32(55,55,55,255));
            if(state.PickupType!=null)PaintMapRect(state,new Box(state.PickupX-10,state.PickupY-10,20,20),new Color32(255,225,70,255));
            foreach(var tank in state.Tanks)if(tank.Alive)PaintMapRect(state,new Box(tank.X-13,tank.Y-13,26,26),tank.Player?new Color32(255,210,35,255):new Color32(244,65,55,255));
            minimapTexture.SetPixels32(minimapPixels);minimapTexture.Apply(false,false);
        }
        void Minimap(BattleSimulation state)
        {
            UpdateMinimap(state);float scale=Mathf.Clamp(Screen.width/1240f,.78f,1.15f),size=142*scale;
            float top=TopHeightPixels,x=Screen.width-size-14*scale,y=top+12*scale;
            var map=Element<RawImage>(new Rect(x,y,size,size));map.texture=minimapTexture;map.uvRect=new Rect(0,0,1,1);map.color=new Color(1,1,1,.88f);
            Outline(new Rect(x,y,size,size),new Color(1,.68f,.05f,.86f),1.5f*scale);
        }
        public void Draw(BattleSimulation state,EconomyClient economy,Texture2D powerupAtlas,bool consumePending,int selectedSlot=0)
        {
            Initialize();cursor=0;bool narrow=Screen.width<920;float h=TopHeightPixels;
            var powerupUi=RuntimePlatformInfo.PowerupUi;bool psg1=powerupUi==PowerupUiMode.MergedTopHud;
            Fill(new Rect(0,0,Screen.width,h),navy);Fill(new Rect(0,0,Screen.width,2),new Color(.22f,.3f,.38f));Fill(new Rect(0,h-3,Screen.width,3),gold);
            float metricWidth=narrow?Screen.width/3f:175;
            Icon(new Rect(7,5,36,36),player);Text(new Rect(46,2,metricWidth-48,18),"SCORE",cream);Text(new Rect(46,18,metricWidth-48,27),state.Score.ToString("D6"),gold,true);
            float x=metricWidth;
            Fill(new Rect(x-7,9,1,28),gold);
            int seconds=state.Tick/60;
            Text(new Rect(x+4,2,metricWidth-8,18),"TIME",cream);Text(new Rect(x+4,18,metricWidth-8,27),(seconds/60).ToString("D2")+":"+(seconds%60).ToString("D2"),cyan,true);
            x+=metricWidth;Fill(new Rect(x-7,9,1,28),gold);
            Text(new Rect(x+4,2,metricWidth-8,18),"LIVES",cream);Text(new Rect(x+4,18,32,27),state.Lives.ToString(),gold,true);
            for(int i=0;i<3;i++)Icon(new Rect(x+32+i*30,18,28,25),player,i>=state.Lives);
            float enemyX=narrow?12:metricWidth*3+4,enemyY=narrow?46:4;
            Fill(new Rect(enemyX-11,9,1,narrow?0:28),gold);
            int opponents=0;
            if(state.IsPvp)for(int i=0;i<BattleSimulation.MaxPlayers;i++)if(i!=state.LocalPlayerSlot&&state.Participants[i].Connected&&state.Participants[i].Lives>0)opponents++;
            Text(new Rect(enemyX,enemyY,100,19),state.IsPvp?"RIVALS":"ENEMIES",cream);
            Text(new Rect(enemyX,enemyY+18,100,23),(state.IsPvp?opponents:state.Remaining).ToString()+" LEFT",new Color(1,.42f,.32f));
            float start=enemyX+100,reserved=psg1?Psg1PowerupWidth(narrow):0,available=Mathf.Max(1,Screen.width-start-10-reserved);
            int total=state.TotalEnemies,deadCount=total-state.Remaining;
            float step=Mathf.Min(34,available/Mathf.Max(1,total));
            for(int i=0;i<total;i++)
            {
                var slot=new Rect(start+i*step,enemyY+4,Mathf.Max(1,step-2),32);
                Fill(new Rect(slot.x,slot.y+3,slot.width,26),new Color(.1f,.15f,.2f,.6f));
                Icon(slot,enemy,i<deadCount);
            }
            Minimap(state);
            if(!state.IsMultiplayer)
            {
                if(psg1)Psg1Powerups(economy,powerupAtlas,consumePending,narrow,selectedSlot);
                else if(powerupUi==PowerupUiMode.WebTopLeft)PowerupBar(economy,powerupAtlas,consumePending,true);
                else if(powerupUi==PowerupUiMode.GameOverlay)PowerupBar(economy,powerupAtlas,consumePending,false);
            }
            while(cursor<elements.Count)elements[cursor++].gameObject.SetActive(false);
        }
        public void Dispose(){if(canvas)Object.Destroy(canvas.gameObject);if(font)Object.Destroy(font);if(roundedSprite)Object.Destroy(roundedSprite);if(roundedTexture)Object.Destroy(roundedTexture);if(minimapTexture)Object.Destroy(minimapTexture);}
    }
}
