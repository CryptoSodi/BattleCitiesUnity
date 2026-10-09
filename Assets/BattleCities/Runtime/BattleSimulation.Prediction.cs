namespace BattleCities.Core
{
    public sealed partial class BattleSimulation
    {
        // Uses the authoritative movement/collision routine on a detached local pose.
        // No fire, damage, pickups, RNG or simulation time advances here.
        public void PredictMovement(TankState pose,Command command)
        {
            if(!MatchStarted||intro>0||Won||Lost)return;
            PrepareTerrain(pose);
            if(command.Move.HasValue)
            {
                Rotate(pose,command.Move.Value);Move(pose);
                pose.Slide=TouchesSlipperyGround(pose)&&!pose.InQuicksand?.5f:0;
            }
            else if(pose.Slide>0){pose.Slide-=StepSeconds;Move(pose);}
            pose.Aim=command.Aim??pose.Direction;
        }
    }
}
