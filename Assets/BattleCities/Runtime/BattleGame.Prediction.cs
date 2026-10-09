using System.Collections.Generic;
using BattleCities.Core;
using BattleCities.Multiplayer;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        private struct PendingMovement { public int Sequence; public Command Command; }
        private readonly Queue<PendingMovement> pendingMovement=new Queue<PendingMovement>();
        private TankState predictedTank;
        private int movementSequence;
        public void PredictInput(ref BattleNetworkInput input)
        {
            input.Sequence=++movementSequence;
            if(!NetworkMatch||NetworkMatch.Object.HasStateAuthority||!Simulation.MatchStarted||Simulation.Won||Simulation.Lost)return;
            var tank=Simulation.Player;if(tank==null){ResetMovementPrediction();return;}
            if(predictedTank==null||predictedTank.Id!=tank.Id){pendingMovement.Clear();predictedTank=CloneMovement(tank);}
            var command=new Command {Move=input.Move>=0&&input.Move<4?(Facing?)input.Move:null,Aim=input.Aim>=0&&input.Aim<4?(Facing?)input.Aim:null};
            pendingMovement.Enqueue(new PendingMovement{Sequence=input.Sequence,Command=command});
            if(pendingMovement.Count>120){pendingMovement.Clear();predictedTank=CloneMovement(tank);return;}
            Simulation.PredictMovement(predictedTank,command);
        }
        public void ReconcileMovement(int acknowledged)
        {
            var tank=Simulation.Player;
            if(tank==null){ResetMovementPrediction();return;}
            if(predictedTank!=null&&predictedTank.Id!=tank.Id)pendingMovement.Clear();
            while(pendingMovement.Count>0&&pendingMovement.Peek().Sequence<=acknowledged)pendingMovement.Dequeue();
            predictedTank=CloneMovement(tank);
            foreach(var pending in pendingMovement)Simulation.PredictMovement(predictedTank,pending.Command);
        }
        private void ResetMovementPrediction(){pendingMovement.Clear();predictedTank=null;}
        private static TankState CloneMovement(TankState t)=>new TankState {Id=t.Id,Slot=t.Slot,Player=true,Tier=t.Tier,Health=t.Health,
            X=t.X,Y=t.Y,Direction=t.Direction,Aim=t.Aim,Slide=t.Slide,SpeedBoost=t.SpeedBoost,InQuicksand=t.InQuicksand,SinkDepth=t.SinkDepth};
    }
}
