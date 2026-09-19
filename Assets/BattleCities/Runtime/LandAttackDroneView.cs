using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using UnityEngine;
namespace BattleCities
{
    public sealed class LandAttackDroneView : MonoBehaviour
    {
        public GameObject Visual;
        public AnimationClip Drive,Idle;
        [SerializeField] float lensIntensity=1;
        Transform[] wheels;Quaternion[] wheelRest;
        float previousDistance;bool initialized;
        public void Initialize()
        {
            if(initialized)return;initialized=true;
            foreach(var animator in Visual.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            foreach(var animation in Visual.GetComponentsInChildren<Animation>(true)){animation.Stop();animation.enabled=false;}
            wheels=Visual.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"Wheel_FL","Wheel_FR","Wheel_RL","Wheel_RR"}.Contains(t.name)).ToArray();
            wheelRest=wheels.Select(t=>t.localRotation).ToArray();
            foreach(var light in Visual.GetComponentsInChildren<Light>(true))light.intensity=lensIntensity;
        }
        public void Tick(LandDroneState state)
        {
            Initialize();
            transform.position=BattleGame.World(state.X,state.Y);
            transform.rotation=Quaternion.Euler(0,state.Heading,0);
            bool moving=state.Distance>previousDistance+.0001f;
            if(moving&&Drive)Drive.SampleAnimation(Visual,Mathf.Repeat(state.Distance/64/(2*Mathf.PI*.163f),1)*Drive.length);
            else if(Idle)Idle.SampleAnimation(Visual,Mathf.Repeat(state.Age,Idle.length));
            // Distance drives the axle angle, including when Idle does not key the wheels.
            float angle=state.Distance/64/.163f*Mathf.Rad2Deg;
            for(int i=0;i<wheels.Length;i++)wheels[i].localRotation=wheelRest[i]*Quaternion.AngleAxis(angle,Vector3.right);
            previousDistance=state.Distance;
        }
    }
}
