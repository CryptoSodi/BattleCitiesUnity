using UnityEngine;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        void DrawLivePickup()
        {
            if(Simulation.PickupType==null)return;
            var screen=gameCamera.WorldToScreenPoint(World(Simulation.PickupX,Simulation.PickupY,.7f));
            if(screen.z<=0)return;
            float size=BattleHudLayout.Current.PickupSizePixels;
            var rect=new Rect(screen.x-size/2,Screen.height-screen.y-size/2,size,size);
            if(PowerupAtlas&&BattleHud.TryPowerupUv(Simulation.PickupType,out var uv))GUI.DrawTextureWithTexCoords(rect,PowerupAtlas,uv);
            else GUI.Box(rect,Simulation.PickupType);
            pickupSparkles.Draw(rect,Simulation.Tick*Core.BattleSimulation.StepSeconds);
        }
    }
}
