using UnityEngine;

namespace BattleCities
{
    /// <summary>Battlefield drops share map scale, independent of HUD, device and render density.</summary>
    public static class BattlePickupSizing
    {
        public const float TileFraction=.9f;
        public static float TileWorldSize=>Vector3.Distance(BattleGame.World(0,0),BattleGame.World(64,0));
        public static float WorldSize=>TileWorldSize*TileFraction;
        public static float ScreenSize(Camera camera,Vector3 position)
        {
            var half=camera.transform.right*(WorldSize*.5f);
            return Vector2.Distance(camera.WorldToScreenPoint(position-half),camera.WorldToScreenPoint(position+half));
        }
    }
}
