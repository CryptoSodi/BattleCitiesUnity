using System;
using System.Collections.Generic;

namespace BattleCities.Core
{
    // Presentation state only. Clients never feed this back into authoritative combat.
    public sealed class BattleFrame
    {
        public int Tick, Score, WinnerSlot, SpawnedEnemies;
        public bool Started, Won, Lost, BaseAlive;
        public float Freeze, ZoomOut, PickupX, PickupY, PickupTime;
        public string PickupType;
        public BattleParticipant[] Participants;
        public TankState[] Tanks;
        public ShotState[] Shots;
        public MineState[] Mines;
        public DroneState[] Drones;
        public TurretState[] Turrets;
        public LandDroneState[] LandDrones;
        public bool[] TerrainAlive;
        public int[] TerrainHealth;
        public Wall[] ExtraWalls;
    }

    public struct BattleVisualEvent
    {
        public int Sequence, Kind, Id, Owner, Direction, Power;
        public float X, Y;
    }

    public sealed partial class BattleSimulation
    {
        public int InitialTerrainCount { get; private set; }
        public readonly List<BattleVisualEvent> VisualEvents=new List<BattleVisualEvent>();
        private int visualSequence;

        public void RecordVisualEvents()
        {
            InitialTerrainCount=Terrain.Count;
            ShotFired+=s=>Record(1,s.Id,s.X,s.Y,s.Owner,(int)s.Direction,s.PowerShot?1:0);
            ShotImpact+=s=>Record(2,s.Id,s.X,s.Y,s.Owner,(int)s.Direction,s.PowerShot?1:0);
            TankDestroyed+=t=>Record(3,t.Id,t.X,t.Y);
            MineDetonated+=m=>Record(4,m.Id,m.X,m.Y);
            DroneDetonated+=d=>Record(5,d.Id,d.X,d.Y);
            TurretFired+=t=>Record(6,t.Id,t.X,t.Y);
            TurretHit+=t=>Record(7,t.Id,t.X,t.Y);
            TurretDestroyed+=t=>Record(8,t.Id,t.X,t.Y);
            LandDroneExploded+=d=>Record(9,d.Id,d.X,d.Y);
        }
        private void Record(int kind,int id,float x,float y,int owner=0,int direction=0,int power=0)
        {
            VisualEvents.Add(new BattleVisualEvent{Sequence=++visualSequence,Kind=kind,Id=id,X=x,Y=y,Owner=owner,Direction=direction,Power=power});
            if(VisualEvents.Count>128)VisualEvents.RemoveAt(0);
        }

        public void ApplyFrame(BattleFrame f)
        {
            Tick=f.Tick;Score=f.Score;WinnerSlot=f.WinnerSlot;MatchStarted=f.Started;Won=f.Won;Lost=f.Lost;spawned=f.SpawnedEnemies;
            intro=f.Started?Math.Max(0,2-f.Tick*StepSeconds):2;
            Freeze=f.Freeze;ZoomOut=f.ZoomOut;PickupType=f.PickupType;PickupX=f.PickupX;PickupY=f.PickupY;PickupTime=f.PickupTime;
            if(BaseAlive&&!f.BaseAlive&&!IsPvp)BaseDestroyed?.Invoke();
            BaseAlive=f.BaseAlive;
            for(int i=0;i<MaxPlayers;i++)Participants[i]=f.Participants[i];
            Replace(Tanks,f.Tanks);Replace(Shots,f.Shots);Replace(Mines,f.Mines);Replace(Drones,f.Drones);Replace(Turrets,f.Turrets);Replace(LandDrones,f.LandDrones);
            bool changed=false;
            for(int i=0;i<InitialTerrainCount;i++)
            {
                bool alive=f.TerrainAlive[i];var wall=Terrain[i];
                if(f.TerrainHealth!=null && i<f.TerrainHealth.Length)
                {
                    int health=f.TerrainHealth[i];
                    if(wall.Health!=health){wall.Health=health;if(alive)WallDamaged?.Invoke(wall);}
                }
                if(wall.Alive==alive)continue;
                wall.Alive=alive;changed=true;if(!alive)WallDestroyed?.Invoke(wall);
            }
            int extras=Terrain.Count-InitialTerrainCount;
            if(extras!=f.ExtraWalls.Length)changed=true;
            else for(int i=0;i<extras;i++)if(Terrain[InitialTerrainCount+i].Id!=f.ExtraWalls[i].Id||Terrain[InitialTerrainCount+i].Alive!=f.ExtraWalls[i].Alive)changed=true;
            if(extras>0)Terrain.RemoveRange(InitialTerrainCount,extras);
            Terrain.AddRange(f.ExtraWalls);
            if(changed)TerrainChanged?.Invoke();
        }

        private static void Replace<T>(List<T> target,T[] values){target.Clear();target.AddRange(values);}

        public void PlayVisualEvent(BattleVisualEvent e)
        {
            switch(e.Kind)
            {
                case 1:ShotFired?.Invoke(EventShot(e));break;
                case 2:ShotImpact?.Invoke(EventShot(e));break;
                case 3:TankDestroyed?.Invoke(new TankState{Id=e.Id,X=e.X,Y=e.Y});break;
                case 4:MineDetonated?.Invoke(new MineState{Id=e.Id,X=e.X,Y=e.Y});break;
                case 5:DroneDetonated?.Invoke(new DroneState{Id=e.Id,X=e.X,Y=e.Y});break;
                case 6:TurretFired?.Invoke(Turrets.Find(t=>t.Id==e.Id)??new TurretState{Id=e.Id,X=e.X,Y=e.Y});break;
                case 7:TurretHit?.Invoke(new TurretState{Id=e.Id,X=e.X,Y=e.Y});break;
                case 8:TurretDestroyed?.Invoke(new TurretState{Id=e.Id,X=e.X,Y=e.Y});break;
                case 9:LandDroneExploded?.Invoke(new LandDroneState{Id=e.Id,X=e.X,Y=e.Y});break;
            }
        }
        private static ShotState EventShot(BattleVisualEvent e)=>new ShotState{Id=e.Id,Owner=e.Owner,X=e.X,Y=e.Y,Direction=(Facing)e.Direction,PowerShot=e.Power!=0};
    }
}
