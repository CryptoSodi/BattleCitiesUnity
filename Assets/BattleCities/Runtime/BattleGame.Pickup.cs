using UnityEngine;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        void DrawLivePickup()
        {
            if(Simulation.PickupType==null)return;
            var position=World(Simulation.PickupX,Simulation.PickupY,.7f);
            var screen=gameCamera.WorldToScreenPoint(position);
            if(screen.z<=0)return;
            float size=BattlePickupSizing.ScreenSize(gameCamera,position);
            var rect=new Rect(screen.x-size/2,Screen.height-screen.y-size/2,size,size);
            if(ExtendedPowerupArt.TryImage(PowerupAtlas,Simulation.PickupType,out var texture,out var uv))GUI.DrawTextureWithTexCoords(rect,texture,uv);
            else GUI.Box(rect,Simulation.PickupType);
            pickupSparkles.Draw(rect,Simulation.Tick*Core.BattleSimulation.StepSeconds);
        }
    }
}
