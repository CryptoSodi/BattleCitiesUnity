using UnityEngine;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        BattlePickupCube pickupCube;
        void SyncPickupCube()
        {
            if(Simulation.PickupType==null){if(pickupCube)pickupCube.gameObject.SetActive(false);return;}
            if(!pickupCube){
                var prefab=RemoteGameArt.Load<GameObject>("PowerupCube");
                if(!prefab)throw new System.InvalidOperationException("Power-up cube is missing from game content.");
                pickupCube=Instantiate(prefab,transform).GetComponent<BattlePickupCube>();
            }
            pickupCube.Present(Simulation.PickupType,World(Simulation.PickupX,Simulation.PickupY),Simulation.Tick*Core.BattleSimulation.StepSeconds);
        }
    }
}
