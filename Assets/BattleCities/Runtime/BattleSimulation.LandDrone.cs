using System;
using System.Collections.Generic;
using System.Linq;
namespace BattleCities.Core
{
    [Serializable] public sealed class LandDroneConfig
    {
        public float PatrolSpeedMultiplier=.55f;
        public float SightAngleDegrees=90;
        public float SpeedMultiplier=1.5f,DetectionTiles=9,Lifetime=60,RouteTimeout=60,RepathSeconds=.5f,Acceleration=540,SplashTiles=1.25f;
        public int Health=2,DirectDamage=8,SplashDamage=2;
    }
    public sealed class LandDroneState
    {
        public int Id,TargetId,Health,OwnerSlot=-1;
        public float X,Y,Age,Speed,Distance,Heading,NoRouteTime,Repath;
        public bool Alive=true;
        public float YieldRemaining,YieldX,YieldY;
        public bool HasYieldPoint;
        public readonly List<Point> Route=new List<Point>();
        public readonly HashSet<int> PatrolledTiles=new HashSet<int>();
        // Conservative footprint includes the body during turns, independent of visual meshes.
        public const float HalfSize=29;
        public Box Bounds=>new Box(X-HalfSize,Y-HalfSize,HalfSize*2,HalfSize*2);
    }
    public sealed partial class BattleSimulation
    {
        public LandDroneConfig LandDroneSettings=new LandDroneConfig();
        public readonly List<LandDroneState> LandDrones=new List<LandDroneState>();
        public event Action<LandDroneState> LandDroneExploded;
        public bool LandDroneGroundFree(Box box)
        {
            if(box.X<0||box.Y<0||box.Right>Width||box.Bottom>Height||box.Overlaps(BaseBounds))return false;
            if(Terrain.Any(w=>w.Alive&&(w.Solid||w.Type=="bush"||w.Type=="grass")&&box.Overlaps(w.Bounds)))return false;
            if(Turrets.Any(t=>t.Alive&&box.Overlaps(t.Clearance)))return false;
            return !Tanks.Any(t=>t.Alive&&t.Player&&!IsPvp&&box.Overlaps(t.MovementBounds));
        }
        private static Box LandBox(float x,float y)=>new Box(x-LandDroneState.HalfSize,y-LandDroneState.HalfSize,LandDroneState.HalfSize*2,LandDroneState.HalfSize*2);
        private bool PlaceLandDrone(TankState player)
        {
            // Search nearest clear ground around the owner, never overlapping any tank.
            for(float radius=64;radius<=128;radius+=16)
            for(int i=0;i<16;i++)
            {
                double angle=i*Math.PI/8;float x=player.X+(float)Math.Cos(angle)*radius,y=player.Y+(float)Math.Sin(angle)*radius;
                var box=LandBox(x,y);
                if(!LandDroneGroundFree(box)||Tanks.Any(t=>t.Alive&&box.Overlaps(t.MovementBounds))||Mines.Any(m=>m.Alive&&box.Overlaps(m.Bounds)))continue;
                LandDrones.Add(new LandDroneState{OwnerSlot=player.Slot,Id=++nextId,X=x,Y=y,Health=Math.Max(1,LandDroneSettings.Health),Heading=(int)player.Direction*90});
                SecondaryCooldown=4;SecondaryStatus="Land drone deployed.";return true;
            }
            SecondaryStatus="No clear ground nearby for the land drone.";return false;
        }
        private bool LandSight(LandDroneState drone,TankState target)
        {
            float dx=target.X-drone.X,dy=target.Y-drone.Y,range=Math.Max(1,LandDroneSettings.DetectionTiles)*64;
            if(dx*dx+dy*dy>range*range)return false;
            // Heading matches the visual nose: zero is up, clockwise in map coordinates.
            double heading=drone.Heading*Math.PI/180;
            double distance=Math.Sqrt(dx*dx+dy*dy);
            double forward=dx*Math.Sin(heading)-dy*Math.Cos(heading);
            double halfAngle=Math.Max(1,Math.Min(179,LandDroneSettings.SightAngleDegrees))*.5*Math.PI/180;
            if(distance>.001&&forward/distance<Math.Cos(halfAngle))return false;
            int steps=Math.Max(1,(int)Math.Ceiling(Math.Sqrt(dx*dx+dy*dy)/4));
            for(int i=1;i<steps;i++)
            {
                var probe=new Box(drone.X+dx*i/steps-1,drone.Y+dy*i/steps-1,2,2);
                if(probe.Overlaps(BaseBounds)||Terrain.Any(w=>w.Alive&&(w.StopsBullet||w.Type=="bush"||w.Type=="grass")&&probe.Overlaps(w.Bounds))
                    ||Turrets.Any(t=>t.BlocksPath&&probe.Overlaps(t.BlockingBounds)))return false;
            }
            return true;
        }
        private bool PlanLandRoute(LandDroneState drone,TankState target=null)
        {
            // Align nodes to map subdivision boundaries: one-tile corridors have
            // centers at multiples of 16, not at an eight-unit offset.
            // Inflate obstacles by the full vehicle footprint.
            // Edges are swept so the grid cannot shortcut across thin walls or corners.
            const int cell=16;int cols=Width/cell,rows=Height/cell,count=cols*rows;
            var previous=new int[count];var free=new bool[count];var queue=new Queue<int>();
            for(int i=0;i<count;i++)
            {
                previous[i]=-2;var b=LandBox((i%cols)*cell,(i/cols)*cell);
                free[i]=b.X>=0&&b.Y>=0&&b.Right<=Width&&b.Bottom<=Height;
            }
            var obstacles=Terrain.Where(w=>w.Alive&&(w.Solid||w.Type=="bush"||w.Type=="grass")).Select(w=>w.Bounds)
                .Concat(Turrets.Where(t=>t.Alive).Select(t=>t.Clearance))
                .Concat(Tanks.Where(t=>t.Alive&&t.Player&&(!IsPvp||t.Slot==drone.OwnerSlot)).Select(t=>t.MovementBounds)).Concat(new[]{BaseBounds});
            foreach(var obstacle in obstacles)
            {
                int left=Math.Max(0,(int)Math.Floor((obstacle.X-LandDroneState.HalfSize)/cell));
                int right=Math.Min(cols-1,(int)Math.Ceiling((obstacle.Right+LandDroneState.HalfSize)/cell));
                int top=Math.Max(0,(int)Math.Floor((obstacle.Y-LandDroneState.HalfSize)/cell));
                int bottom=Math.Min(rows-1,(int)Math.Ceiling((obstacle.Bottom+LandDroneState.HalfSize)/cell));
                for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)
                    if(LandBox(x*cell,y*cell).Overlaps(obstacle))free[y*cols+x]=false;
            }
            int start=-1;float nearest=float.MaxValue;
            for(int i=0;i<count;i++)
            {
                if(!free[i])continue;float x=i%cols*cell,y=i/cols*cell,d=DistanceSquared(x,y,drone.X,drone.Y);
                if(d<nearest&&d<=32*32&&LandDroneGroundFree(Sweep(drone.Bounds,LandBox(x,y)))){nearest=d;start=i;}
            }
            drone.Route.Clear();if(start<0)return false;
            queue.Enqueue(start);previous[start]=-1;int end=-1;float patrolScore=float.MinValue;
            while(queue.Count>0)
            {
                int current=queue.Dequeue(),cx=current%cols,cy=current/cols;var box=LandBox(cx*cell,cy*cell);
                if(target!=null&&box.Overlaps(target.MovementBounds)){end=current;break;}
                if(target==null)
                {
                    float distance=DistanceSquared(cx*cell,cy*cell,drone.X,drone.Y);
                    int tile=(cy*cell/64)*(Width/64)+cx*cell/64;
                    float score=distance-(drone.PatrolledTiles.Contains(tile)?Width*Width+Height*Height:0);
                    if(distance>=16*16&&score>patrolScore){patrolScore=score;end=current;}
                }
                for(int dir=0;dir<4;dir++)
                {
                    int nx=cx+(dir==0?1:dir==1?-1:0),ny=cy+(dir==2?1:dir==3?-1:0);
                    if(nx<0||ny<0||nx>=cols||ny>=rows)continue;int next=ny*cols+nx;
                    if(previous[next]!=-2||!free[next])continue;
                    previous[next]=current;queue.Enqueue(next);
                }
            }
            if(end<0)return false;
            for(int i=end;i>=0;i=previous[i])drone.Route.Add(new Point{x=i%cols*cell,y=i/cols*cell});
            drone.Route.Reverse();return true;
        }

        // Player input remains authoritative even when its attempted movement is blocked.
        // Reserve the player's forward lane and move out of it before resuming pursuit.
        private bool YieldToPlayer(LandDroneState drone,float dt)
        {
            var player=IsMultiplayer?Tanks.Find(t=>t.Alive&&t.Player&&t.Slot==drone.OwnerSlot):Player;
            bool approaching=false;float px=0,py=0;
            if(player!=null&&(playerMoveIntent.HasValue||player.Slide>0))
            {
                Vector(playerMoveIntent??player.Direction,out px,out py);
                var lane=Sweep(player.MovementBounds,TankState.MovementBox(player.X+px*120,player.Y+py*120));
                approaching=lane.Overlaps(drone.Bounds)&&DistanceSquared(player.X,player.Y,drone.X,drone.Y)<180*180;
            }
            if(approaching)drone.YieldRemaining=.4f;
            else drone.YieldRemaining=Math.Max(0,drone.YieldRemaining-dt);
            if(drone.YieldRemaining<=0)
            {
                if(drone.HasYieldPoint){drone.HasYieldPoint=false;drone.Route.Clear();drone.Repath=0;}
                return false;
            }
            drone.Route.Clear();drone.Repath=0;
            if(!drone.HasYieldPoint||DistanceSquared(drone.X,drone.Y,drone.YieldX,drone.YieldY)<4
                ||!LandDroneGroundFree(Sweep(drone.Bounds,LandBox(drone.YieldX,drone.YieldY))))
            {
                drone.HasYieldPoint=false;float best=float.MinValue;
                if(player!=null)
                {
                    // Prefer a clear lateral escape; backing away wins if a narrow lane
                    // has no shoulder. Test the entire path, not just its endpoint.
                    if(px==0&&py==0)Vector(player.Direction,out px,out py);
                    var lane=Sweep(player.MovementBounds,TankState.MovementBox(player.X+px*160,player.Y+py*160));
                    for(int direction=0;direction<4;direction++)
                    {
                        Vector((Facing)direction,out var dx,out var dy);
                        for(int distance=96;distance>=16;distance-=16)
                        {
                            float x=drone.X+dx*distance,y=drone.Y+dy*distance;
                            var sweep=Sweep(drone.Bounds,LandBox(x,y));
                            if(!LandDroneGroundFree(sweep)||Tanks.Any(t=>t.Alive&&!t.Player&&sweep.Overlaps(t.MovementBounds)))continue;
                            float away=(x-player.X)*(x-player.X)+(y-player.Y)*(y-player.Y);
                            float score=(!lane.Overlaps(LandBox(x,y))?100000:0)+away+distance;
                            if(score<=best)continue;best=score;drone.YieldX=x;drone.YieldY=y;drone.HasYieldPoint=true;
                        }
                    }
                }
            }
            if(!drone.HasYieldPoint){drone.Speed=0;return true;}
            float vx=drone.YieldX-drone.X,vy=drone.YieldY-drone.Y;
            float length=(float)Math.Sqrt(vx*vx+vy*vy);
            if(length<.01f){drone.HasYieldPoint=false;return true;}
            drone.Speed=Math.Min((player?.Speed??180)*Math.Max(1.5f,LandDroneSettings.SpeedMultiplier),
                drone.Speed+Math.Max(540,LandDroneSettings.Acceleration)*dt);
            float travel=Math.Min(length,drone.Speed*dt);
            while(travel>0)
            {
                float step=Math.Min(1,travel),x=drone.X+vx/length*step,y=drone.Y+vy/length*step;
                var sweep=Sweep(drone.Bounds,LandBox(x,y));
                if(!LandDroneGroundFree(sweep)||Tanks.Any(t=>t.Alive&&!t.Player&&sweep.Overlaps(t.MovementBounds)))
                {drone.HasYieldPoint=false;drone.Speed=0;break;}
                drone.X=x;drone.Y=y;drone.Distance+=step;travel-=step;
                drone.Heading=(float)(Math.Atan2(vx,-vy)*180/Math.PI);
            }
            return true;
        }

        private void UpdateLandDrones(float dt)
        {
            foreach(var drone in LandDrones)
            {
                if(!drone.Alive)continue;
                drone.Age+=dt;
                if(drone.Age>=Math.Max(.1f,LandDroneSettings.Lifetime)){drone.Alive=false;continue;}
                var contact=Tanks.Find(t=>t.Alive&&IsSecondaryTarget(t,drone.OwnerSlot)&&drone.Bounds.Overlaps(t.MovementBounds));
                if(contact!=null){DetonateLandDrone(drone,contact);continue;}
                if(YieldToPlayer(drone,dt))continue;
                drone.Repath-=dt;
                var target=Tanks.Find(t=>t.Alive&&IsSecondaryTarget(t,drone.OwnerSlot)&&t.Id==drone.TargetId);
                if(drone.Repath<=0||target==null&&drone.TargetId!=0)
                {
                    drone.Repath=Math.Max(.1f,LandDroneSettings.RepathSeconds);
                    var patrolRoute=drone.TargetId==0?drone.Route.ToArray():Array.Empty<Point>();
                    bool routed=target!=null&&PlanLandRoute(drone,target);
                    if(!routed)
                    {
                        drone.TargetId=0;drone.Route.Clear();
                        foreach(var candidate in Tanks.Where(t=>t.Alive&&IsSecondaryTarget(t,drone.OwnerSlot)).OrderBy(t=>DistanceSquared(drone.X,drone.Y,t.X,t.Y)))
                            if(LandSight(drone,candidate)&&PlanLandRoute(drone,candidate)){drone.TargetId=candidate.Id;routed=true;break;}
                        if(!routed){drone.Route.Clear();drone.Route.AddRange(patrolRoute);if(drone.Route.Count==0)PlanLandRoute(drone);}
                    }
                }
                drone.PatrolledTiles.Add(((int)drone.Y/64)*(Width/64)+(int)drone.X/64);
                if(drone.TargetId==0&&drone.Route.Count==0&&drone.Repath<=StepSeconds)PlanLandRoute(drone);
                bool moved=false;
                if(drone.Route.Count>0)
                {
                    float speedMultiplier=drone.TargetId!=0?LandDroneSettings.SpeedMultiplier:LandDroneSettings.PatrolSpeedMultiplier;
                    drone.Speed=Math.Min((Player?.Speed??180)*Math.Max(.1f,speedMultiplier),drone.Speed+Math.Max(1,LandDroneSettings.Acceleration)*dt);
                    float budget=drone.Speed*dt;
                    while(budget>0&&drone.Route.Count>0&&drone.Alive)
                    {
                        var point=drone.Route[0];float dx=point.x-drone.X,dy=point.y-drone.Y,length=(float)Math.Sqrt(dx*dx+dy*dy);
                        if(length<.01f){drone.Route.RemoveAt(0);continue;}
                        float step=Math.Min(1,Math.Min(length,budget)),x=drone.X+dx/length*step,y=drone.Y+dy/length*step;
                        if(!LandDroneGroundFree(Sweep(drone.Bounds,LandBox(x,y)))){drone.Route.Clear();drone.Speed=0;drone.Repath=0;break;}
                        drone.X=x;drone.Y=y;drone.Heading=(float)(Math.Atan2(dx,-dy)*180/Math.PI);drone.Distance+=step;budget-=step;moved=true;
                        contact=Tanks.Find(t=>t.Alive&&IsSecondaryTarget(t,drone.OwnerSlot)&&drone.Bounds.Overlaps(t.MovementBounds));
                        if(contact!=null)DetonateLandDrone(drone,contact);
                    }
                }
                if(moved)drone.NoRouteTime=0;
                else {drone.Speed=0;drone.NoRouteTime+=dt;}
                if(drone.NoRouteTime>=Math.Max(.1f,LandDroneSettings.RouteTimeout))drone.Alive=false;
            }
            LandDrones.RemoveAll(d=>!d.Alive);
        }
        private void DetonateLandDrone(LandDroneState drone,TankState direct)
        {
            if(!drone.Alive)return;drone.Alive=false;LandDroneExploded?.Invoke(drone);
            foreach(var victim in Tanks.Where(t=>t.Alive&&IsSecondaryTarget(t,drone.OwnerSlot)).ToArray())
            {
                bool hit=victim==direct;
                if(!hit&&DistanceSquared(victim.X,victim.Y,drone.X,drone.Y)>Math.Pow(Math.Max(0,LandDroneSettings.SplashTiles)*64,2))continue;
                if(victim.Shield>0)continue;
                victim.Health-=Math.Max(1,hit?LandDroneSettings.DirectDamage:LandDroneSettings.SplashDamage);
                if(victim.Drop){victim.Drop=false;DropRequested?.Invoke();}
                if(victim.Health<=0)Kill(victim);
            }
        }
    }
}
