using System;
using System.Linq;

namespace BattleCities.Core
{
    [Serializable]
    public sealed class BattleFlag
    {
        public int CarrierId;
        public float X, Y, ReturnSeconds;
        public bool AtHome=true;
        public BattleFlag Copy()=>(BattleFlag)MemberwiseClone();
    }

    public sealed partial class BattleSimulation
    {
        public bool IsCaptureFlag=>BattleModeRules.IsFlagMode(Mode);
        public const int CapturesToWin=3;
        public readonly BattleFlag[] Flags={new BattleFlag(),new BattleFlag()};
        public readonly int[] FlagScores=new int[2];
        public Point FlagHome(int team)=>onlineSpawns[team+2];

        void ResetFlags()
        {
            for(int team=0;team<2;team++){FlagScores[team]=0;ReturnFlag(team);}
        }
        void ReturnFlag(int team)
        {
            var home=FlagHome(team);var flag=Flags[team];
            flag.X=home.x;flag.Y=home.y;flag.CarrierId=0;flag.AtHome=true;flag.ReturnSeconds=0;
        }
        public void DropCarriedFlag(int slot)
        {
            if(!IsCaptureFlag)return;
            foreach(var flag in Flags)
            {
                var carrier=Tanks.Find(t=>t.Id==flag.CarrierId&&t.Player&&t.Slot==slot);
                if(carrier==null)continue;
                ReplayLifecycle?.Invoke(new ReplayEvent {kind="flagdrop",slot=slot});
                flag.X=carrier.X;flag.Y=carrier.Y;flag.CarrierId=0;flag.AtHome=false;flag.ReturnSeconds=30;
            }
        }
        void UpdateFlags(float dt)
        {
            if(!IsCaptureFlag||Won||Lost)return;
            for(int team=0;team<2;team++)
            {
                var flag=Flags[team];
                if(flag.CarrierId!=0)
                {
                    var carrier=Tanks.Find(t=>t.Id==flag.CarrierId);
                    if(carrier!=null){flag.X=carrier.X;flag.Y=carrier.Y;}
                    if(carrier==null||!carrier.Alive||!Participants[carrier.Slot].Connected)
                    {flag.CarrierId=0;flag.AtHome=false;flag.ReturnSeconds=30;continue;}
                }
                else
                {
                    if(!flag.AtHome)
                    {
                        flag.ReturnSeconds-=dt;
                        if(flag.ReturnSeconds<=0){ReturnFlag(team);continue;}
                        // Defenders get priority if opposing tanks reach a dropped flag together.
                        if(Tanks.Any(t=>t.Alive&&t.Player&&TeamForSlot(t.Slot)==team&&NearFlag(t,flag)))
                        {ReturnFlag(team);continue;}
                    }
                    var thief=Tanks.FirstOrDefault(t=>t.Alive&&t.Player&&Participants[t.Slot].Connected&&TeamForSlot(t.Slot)!=team&&NearFlag(t,flag));
                    if(thief!=null){flag.CarrierId=thief.Id;flag.AtHome=false;flag.ReturnSeconds=0;flag.X=thief.X;flag.Y=thief.Y;}
                }
            }
            for(int team=0;team<2;team++)
            {
                var stolen=Flags[1-team];if(stolen.CarrierId==0||!Flags[team].AtHome)continue;
                var carrier=Tanks.Find(t=>t.Id==stolen.CarrierId&&t.Alive);
                var home=FlagHome(team);
                if(carrier==null||DistanceSquared(carrier.X,carrier.Y,home.x,home.y)>40*40)continue;
                FlagScores[team]++;ReturnFlag(1-team);
                if(FlagScores[team]>=CapturesToWin){WinnerSlot=team;Won=true;return;}
            }
        }
        static bool NearFlag(TankState tank,BattleFlag flag)=>DistanceSquared(tank.X,tank.Y,flag.X,flag.Y)<=32*32;
    }
}
