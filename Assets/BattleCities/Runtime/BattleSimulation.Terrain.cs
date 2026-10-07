using System;

namespace BattleCities.Core
{
    public sealed partial class BattleSimulation
    {
        public const float QuicksandSinkSeconds=5.5f;
        public const float QuicksandEscapeSeconds=3.2f;

        private bool TouchesSlipperyGround(TankState tank)
        {
            foreach(var wall in Terrain)
                if(wall.Alive&&BattleTerrain.IsSlippery(wall.Type)&&wall.Bounds.Overlaps(tank.Bounds))return true;
            return false;
        }
        private bool InQuicksand(TankState tank)
        {
            // The central part of both tracks must enter the patch. A tiny corner
            // overlap should not sink a tank that is still standing on solid ground.
            var contact=new Box(tank.X-16,tank.Y-16,32,32);
            float area=0;
            foreach(var wall in Terrain)
                if(wall.Alive&&wall.Type==BattleTerrain.Quicksand)area+=OverlapArea(contact,wall.Bounds);
            return area>=512;
        }
        private void PrepareTerrain(TankState tank)
        {
            tank.InQuicksand=InQuicksand(tank);
            if(tank.InQuicksand)tank.Slide=0;
        }
        private void UpdateQuicksand(TankState tank,float dt)
        {
            tank.InQuicksand=InQuicksand(tank);
            if(!tank.InQuicksand)tank.SinkDepth=Math.Max(0,tank.SinkDepth-dt*1.4f);
            else
            {
                tank.Slide=0;
                // Only real movement loosens the sand. Turning, firing or pushing
                // against a wall cannot bypass the trap. Speed never reaches zero.
                float change=tank.Moving?-dt/QuicksandEscapeSeconds:dt/QuicksandSinkSeconds;
                tank.SinkDepth=Math.Max(0,Math.Min(1,tank.SinkDepth+change));
            }
        }
    }
}
