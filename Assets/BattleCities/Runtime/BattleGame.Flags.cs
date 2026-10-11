using UnityEngine;
namespace BattleCities
{
    public sealed partial class BattleGame
    {
        readonly GameObject[] flagViews=new GameObject[2];
        void SyncFlags()
        {
            for(int team=0;team<2;team++)
            {
                if(Simulation==null||!Simulation.IsCaptureFlag)
                {if(flagViews[team])flagViews[team].SetActive(false);continue;}
                if(!flagViews[team])
                {
                    var root=new GameObject(team==0?"Yellow flag":"Green flag");root.transform.SetParent(actorsRoot);flagViews[team]=root;
                    var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.color=team==0?new Color(1,.8f,.1f):new Color(.25f,.85f,.35f);ownedMaterials.Add(material);
                    FlagPart(root.transform,"Pole",new Vector3(0,.65f,0),new Vector3(.045f,1.3f,.045f),material);
                    FlagPart(root.transform,"Banner",new Vector3(.24f,1.08f,0),new Vector3(.48f,.32f,.06f),material);
                    FlagPart(root.transform,"Foot",new Vector3(0,.035f,0),new Vector3(.22f,.07f,.22f),material);
                }
                var flag=Simulation.Flags[team];flagViews[team].SetActive(true);
                flagViews[team].transform.position=World(flag.X,flag.Y,flag.CarrierId!=0?.75f:.04f);
            }
        }
        static void FlagPart(Transform root,string name,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(root,false);
            part.transform.localPosition=position;part.transform.localScale=scale;
            Object.Destroy(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
