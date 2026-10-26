using UnityEngine;

namespace BattleCities
{
    // Referenced by the remote art bundle and preloaded before login.
    public sealed class ExtendedPowerupArt : ScriptableObject
    {
        public Sprite maxHealer,healthHealer,rapidShooter,rapidShooterLong;
        public Texture2D maxHealerFrame,healthHealerFrame,rapidShooterLongFrame;
        public static Sprite Illustration(string item)
        {
            var art=RemoteGameArt.Load<ExtendedPowerupArt>("ExtendedPowerupArt");if(!art)return null;
            return item=="max-healer"?art.maxHealer:item=="health-healer"?art.healthHealer:
                item=="rapid-shooter"?art.rapidShooter:item=="rapid-shooter-long"?art.rapidShooterLong:null;
        }
        public static bool TryImage(Texture2D atlas,string type,out Texture2D texture,out Rect uv)
        {
            if(BattleHud.TryPowerupUv(type,out uv)){texture=atlas;return texture;}
            var art=RemoteGameArt.Load<ExtendedPowerupArt>("ExtendedPowerupArt");
            texture=!art?null:type=="healmax"?art.maxHealerFrame:type=="heal2"?art.healthHealerFrame:type=="rapidlong"?art.rapidShooterLongFrame:null;
            uv=new Rect(0,0,1,1);return texture;
        }
    }
}
