using System;
using System.Linq;

namespace BattleCities.Core
{
    public sealed partial class BattleSimulation
    {
        public Box RivalBaseBounds { get; private set; } = new Box(-2048,-2048,64,64);
        public bool RivalBaseAlive { get; private set; }
        public event Action RivalBaseDestroyed;
        private readonly float[] teamSpawnTimers={0.2f,0.2f};
        private readonly int[] teamSpawnCounts=new int[2];

        private bool TouchesBase(Box box) => box.Overlaps(BaseBounds) || (RivalBaseAlive&&box.Overlaps(RivalBaseBounds));

        private bool HitTeamBase(ShotState shot,Box bounds)
        {
            if(IsCaptureFlag)return bounds.Overlaps(BaseBounds)||bounds.Overlaps(RivalBaseBounds);
            int team=TeamForSlot(ShotSlot(shot));
            if(team==1&&BaseAlive&&bounds.Overlaps(BaseBounds))
            {BaseAlive=false;BaseDestroyed?.Invoke();return true;}
            if(team==0&&RivalBaseAlive&&bounds.Overlaps(RivalBaseBounds))
            {RivalBaseAlive=false;RivalBaseDestroyed?.Invoke();return true;}
            return false;
        }

        private void SpawnTeamReinforcements(float dt)
        {
            for(int team=0;team<2;team++)
            {
                teamSpawnTimers[team]-=dt;
                if(teamSpawnTimers[team]>0||Tanks.Count(t=>t.Alive&&!t.Player&&TeamForSlot(t.Slot)==team)>=4)continue;
                // Four AI per side, independent of the two human player slots.
                for(int attempt=0;attempt<2;attempt++)
                {
                    float x=((teamSpawnCounts[team]+attempt)%2==0)?32:Width-32;
                    float y=team==0?Height-32:32;
                    var tank=new TankState {Id=nextId+1,Slot=team,Tier=teamSpawnCounts[team]%5==4?1:0,
                        X=x,Y=y,Direction=team==0?Facing.Up:Facing.Down,Aim=team==0?Facing.Up:Facing.Down};
                    if(!Free(tank.MovementBounds,tank))continue;
                    tank.Health=TankState.StartingHealth(tank.Tier,false);tank.Shield=1;
                    nextId++;Tanks.Add(tank);teamSpawnCounts[team]++;teamSpawnTimers[team]=4;break;
                }
            }
        }

        private void UpdateTeamEnemy(TankState tank)
        {
            int team=TeamForSlot(tank.Slot);
            var target=Tanks.Where(t=>t.Alive&&!AreAllies(t.Slot,tank.Slot))
                .OrderBy(t=>DistanceSquared(tank.X,tank.Y,t.X,t.Y)).FirstOrDefault();
            var enemyBase=team==0?RivalBaseBounds:BaseBounds;
            float tx=enemyBase.X+32,ty=enemyBase.Y+32;
            if(target!=null&&(IsCaptureFlag||DistanceSquared(tank.X,tank.Y,target.X,target.Y)<256*256))
            {tx=target.X;ty=target.Y;}
            tank.Think-=StepSeconds;tank.FireDelay-=StepSeconds;
            if(tank.Think<=0)
            {
                var direction=Cardinal(tx-tank.X,ty-tank.Y);
                if(tank.AiState==1)direction=(Facing)Next(0,4);
                Rotate(tank,direction);tank.Aim=direction;tank.Think=.35f;
            }
            if(tank.FireDelay<=0)
            {
                // Aim at aligned opponents; otherwise clear cover in the travel direction.
                if(Math.Abs(tx-tank.X)<28||Math.Abs(ty-tank.Y)<28)tank.Aim=Cardinal(tx-tank.X,ty-tank.Y);
                if(!DisableEnemyFire)Fire(tank);
                tank.FireDelay=.5f+Next(0,800)/1000f;
            }
            tank.AiState=Move(tank)?0:1;
        }
    }
}
