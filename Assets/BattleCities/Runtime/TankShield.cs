using UnityEngine;

namespace BattleCities
{
    // Visualizes the simulation shield timer; never grants protection itself.
    public sealed class TankShield : MonoBehaviour
    {
        GameObject shell;
        LineRenderer[] rings,arcs;
        Material field,energy;
        float age;
        bool visible;
        void Awake()
        {
            field=new Material(Resources.Load<Shader>("ShieldDome"));
            energy=Transparent(new Color(.2f,.85f,1,.85f));
            shell=GameObject.CreatePrimitive(PrimitiveType.Sphere);shell.name="Magnetic shield field";
            Destroy(shell.GetComponent<Collider>());shell.transform.SetParent(transform,false);
            shell.transform.localPosition=new Vector3(0,.03f,0);shell.transform.localScale=new Vector3(1.38f,1.8f,1.38f);
            var renderer=shell.GetComponent<Renderer>();renderer.sharedMaterial=field;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            rings=new LineRenderer[0];for(int i=0;i<0;i++)rings[i]=Line("Magnetic ring",65,.013f);
            arcs=new LineRenderer[6];for(int i=0;i<6;i++)arcs[i]=Line("Electric arc",11,.011f);
            SetVisible(false);
        }
        Material Transparent(Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetColor("_BaseColor",color);m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",1);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",2);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;return m;
        }
        LineRenderer Line(string name,int points,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=points;line.widthMultiplier=width;line.sharedMaterial=energy;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;line.numCornerVertices=2;return line;
        }
        void SetVisible(bool value)
        {visible=value;shell.SetActive(value);foreach(var r in rings)r.enabled=value;foreach(var a in arcs)a.enabled=value;}
        public void Tick(float remaining,float dt)
        {
            bool active=remaining>0;if(active!=visible)SetVisible(active);if(!active)return;
            age+=dt;
            // A restrained pulse becomes quicker as the shield is about to expire.
            float pulse=.8f+.2f*Mathf.Sin(age*(remaining<2?14:4));
            energy.SetColor("_BaseColor",new Color(.3f,.85f,1,.85f*pulse));field.SetFloat("_Pulse",pulse);
            for(int r=0;r<rings.Length;r++)
            {
                float radius=.775f+r*.045f;
                for(int i=0;i<65;i++){float a=i*Mathf.PI*2/64;rings[r].SetPosition(i,new Vector3(Mathf.Cos(a)*radius,.035f,Mathf.Sin(a)*radius));}
            }
            for(int r=0;r<arcs.Length;r++)
            {
                int flicker=Mathf.FloorToInt(age*12);arcs[r].enabled=(flicker+r)%3==0;
                for(int i=0;i<11;i++)
                {
                    float longitude=r*Mathf.PI/3+Mathf.Sin(flicker*3.7f+r)*.6f+i*.042f;
                    float latitude=.24f+Mathf.Sin(i*9.7f+flicker+r)*.035f;
                    longitude+=Mathf.Sin(i*19.3f+flicker*7.1f+r)*.035f;
                    float radial=Mathf.Cos(latitude)*.697f;
                    arcs[r].SetPosition(i,new Vector3(Mathf.Cos(longitude)*radial,.03f+Mathf.Sin(latitude)*.908f,Mathf.Sin(longitude)*radial));
                }
            }
        }
        void OnDestroy(){Destroy(field);Destroy(energy);}
    }
}


