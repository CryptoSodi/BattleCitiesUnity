using UnityEngine;

namespace BattleCities
{
    // Screen-space ornament keeps pickups readable above walls, just like their icons.
    public sealed class PickupSparkles
    {
        readonly Texture2D star;
        public PickupSparkles()
        {
            star=new Texture2D(64,64,TextureFormat.RGBA32,false);
            star.name="Golden pickup sparkle";star.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float dx=Mathf.Abs((x-31.5f)/31.5f),dy=Mathf.Abs((y-31.5f)/31.5f);
                float rays=Mathf.Clamp01(1-(Mathf.Sqrt(dx)+Mathf.Sqrt(dy)));
                float glow=Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dy*dy));
                float alpha=Mathf.Clamp01(rays*4+glow*glow*.2f);
                star.SetPixel(x,y,new Color(1,Mathf.Lerp(.68f,1,rays),Mathf.Lerp(.12f,.85f,rays),alpha));
            }
            star.Apply();
        }
        public void Draw(Rect icon,float time)
        {
            var previous=GUI.color;
            for(int i=0;i<6;i++)
            {
                float angle=time*1.7f+i*Mathf.PI/3;
                float pulse=(Mathf.Sin(time*4+i*2)+1)*.5f;
                float size=12+10*pulse;
                Vector2 point=icon.center+new Vector2(Mathf.Cos(angle)*icon.width*.65f,Mathf.Sin(angle)*icon.height*.58f);
                GUI.color=new Color(1,1,1,.55f+.45f*pulse);
                GUI.DrawTexture(new Rect(point.x-size/2,point.y-size/2,size,size),star);
            }
            GUI.color=previous;
        }
        public void Dispose(){Object.Destroy(star);}
    }
}
