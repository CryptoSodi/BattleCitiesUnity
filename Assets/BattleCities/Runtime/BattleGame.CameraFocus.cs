using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        public static Vector3 ClampCameraToStage(Vector3 focus,float width,float height,float halfX,float halfZ)
        {
            focus.x=halfX*2>=width?width/2:Mathf.Clamp(focus.x,halfX,width-halfX);
            focus.z=halfZ*2>=height?-height/2:Mathf.Clamp(focus.z,-height+halfZ,-halfZ);
            return focus;
        }

        public static Vector3 CameraFocus(BattleSimulation simulation)
        {
            if(simulation.Lost||(!simulation.IsMultiplayer&&simulation.Lives<=0)){
                var home=simulation.IsTeamBattle&&BattleSimulation.TeamForSlot(simulation.LocalPlayerSlot)==1?simulation.RivalBaseBounds:simulation.BaseBounds;
                if(home.X>=0&&home.Y>=0)return World(home.X+home.W/2,home.Y+home.H/2);
            }else if(simulation.Player!=null)return World(simulation.Player.X,simulation.Player.Y);
            var spawn=simulation.PlayerRespawnPoint;return World(spawn.x,spawn.y);
        }
    }
}
