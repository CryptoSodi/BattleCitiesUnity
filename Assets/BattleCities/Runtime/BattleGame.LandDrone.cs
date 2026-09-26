using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using UnityEngine;
namespace BattleCities
{
    public sealed partial class BattleGame
    {
        public LandDroneConfig LandDroneSettings=new LandDroneConfig();
        private readonly Dictionary<int,LandAttackDroneView> landDroneViews=new Dictionary<int,LandAttackDroneView>();
        private GameObject landDronePrefab;
        private AudioSource landDroneAudio;
        private AudioClip landDroneExplosion;
        private void PlayLandDroneExplosion()
        {
            if(!landDroneExplosion)landDroneExplosion=Resources.Load<AudioClip>("Audio/LandDroneExplosion");
            if(!landDroneAudio){landDroneAudio=gameObject.AddComponent<AudioSource>();landDroneAudio.playOnAwake=false;landDroneAudio.spatialBlend=0;}
            if(landDroneExplosion)landDroneAudio.PlayOneShot(landDroneExplosion,.65f);
        }
        private void SyncLandDrones()
        {
            foreach(var state in Simulation.LandDrones.Where(d=>d.Alive))
            {
                if(!landDroneViews.TryGetValue(state.Id,out var view))
                {
                    if(!landDronePrefab)landDronePrefab=Resources.Load<GameObject>("Deployables/LandAttackDrone");
                    if(!landDronePrefab)throw new System.InvalidOperationException("LandAttackDrone prefab is missing");
                    view=Instantiate(landDronePrefab,actorsRoot).GetComponent<LandAttackDroneView>();
                    TintDeployable(view.gameObject,state.OwnerSlot);
                    Shadows(view.gameObject);landDroneViews.Add(state.Id,view);
                }
                view.Tick(state);
            }
            foreach(var id in landDroneViews.Keys.Where(id=>!Simulation.LandDrones.Any(d=>d.Alive&&d.Id==id)).ToArray())
            {Destroy(landDroneViews[id].gameObject);landDroneViews.Remove(id);}
        }
    }
}
