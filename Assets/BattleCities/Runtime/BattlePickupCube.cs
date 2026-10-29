using System;
using System.Collections.Generic;
using BattleCities.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    // Visuals only. The simulation still owns spawning, lifetime and collection.
    public sealed class BattlePickupCube : MonoBehaviour
    {
        public Material EmblemMaterial,HaloMaterial;
        public Sprite CurrencyIllustration;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<UnityEngine.Object> emblemOwned=new List<UnityEngine.Object>();
        Transform halo,emblems,body;
        readonly List<Transform> motes=new List<Transform>();
        public const float BodyScale=.6f;
        Material haloInstance;
        Light pickupLight;
        struct GlowSurface { public Renderer Renderer;public int Slot;public bool Gold; }
        readonly List<GlowSurface> glowSurfaces=new List<GlowSurface>();
        MaterialPropertyBlock glow;
        Color panelColour;
        string currentType;
        public void Present(string type,Vector3 floor,float time)
        {
            if(string.IsNullOrEmpty(type)){gameObject.SetActive(false);return;}
            gameObject.SetActive(true);transform.position=floor+Vector3.up*.025f;
            if(!body){
                body=new GameObject("Floating power-up cube").transform;body.SetParent(transform,false);
                transform.Find("Gold frame and blue faces").SetParent(body,false);
                body.localScale=Vector3.one*BodyScale;
            }
            if(currentType!=type){currentType=type;panelColour=PanelColor(type);SetEmblems(type);}
            if(!halo)CreateHalo();
            body.localPosition=Vector3.up*(.19f+.055f*Mathf.Sin(time*2.8f));
            body.localRotation=Quaternion.Euler(Mathf.Sin(time*1.7f)*3f,time*38f,Mathf.Cos(time*1.4f)*3f);
            halo.localRotation=Quaternion.Euler(0,time*55f,0);
            float pulse=.5f+.5f*Mathf.Sin(time*4f);
            halo.localScale=Vector3.one*(.86f+.14f*pulse);
            haloInstance.SetColor("_BaseColor",new Color(2.4f,1.35f,.1f,.58f+.25f*pulse));
            UpdateGlow(pulse);
            AnimateMotes(time);
        }
        void UpdateGlow(float pulse)
        {
            if(glow==null){
                glow=new MaterialPropertyBlock();
                foreach(var renderer in body.Find("Gold frame and blue faces").GetComponentsInChildren<Renderer>()){
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++)glowSurfaces.Add(new GlowSurface{Renderer=renderer,Slot=i,Gold=materials[i].name.IndexOf("Gold",StringComparison.OrdinalIgnoreCase)>=0});
                }
            }
            foreach(var surface in glowSurfaces){
                glow.Clear();
                var panel=panelColour;
                if(!surface.Gold)glow.SetColor("_BaseColor",panel);
                glow.SetColor("_EmissionColor",surface.Gold?new Color(.42f,.16f,.008f)*(1f+.3f*pulse):panel*(panel.maxColorComponent>.9f?.025f:.12f));
                surface.Renderer.SetPropertyBlock(glow,surface.Slot);
            }
            if(!pickupLight){
                pickupLight=new GameObject("Warm pickup light",typeof(Light)).GetComponent<Light>();
                pickupLight.transform.SetParent(transform,false);pickupLight.transform.localPosition=Vector3.up*.55f;
                pickupLight.type=LightType.Point;pickupLight.color=new Color(1f,.74f,.3f);
                pickupLight.range=BattlePickupSizing.WorldSize*2f;pickupLight.shadows=LightShadows.None;
                pickupLight.renderMode=LightRenderMode.Auto;
            }
            pickupLight.intensity=1.1f+.35f*pulse;
        }
        public static Color PanelColor(string type)
        {
            // Retain each illustration's identity rather than painting every inset cobalt.
            string hex=type=="healmax"?"FFF4DC":type=="heal2"?"F4F2E9":
                type=="freeze"?"DCEFFF":type=="defence"?"E8CFA6":
                type=="speed"?"DCE3EA":type=="wipeout"?"852E38":
                type=="zoomout"?"195C6B":type=="rapidlong"?"173D82":"0754B8";
            ColorUtility.TryParseHtmlString("#"+hex,out var color);return color;
        }
        void SetEmblems(string type)
        {
            if(emblems){emblems.gameObject.SetActive(false);Release(emblems.gameObject);}
            foreach(var item in emblemOwned)if(item)Release(item);emblemOwned.Clear();
            emblems=new GameObject("Power-up illustrations").transform;emblems.SetParent(body,false);
            var sprite=type=="batc100"||type=="batc200"?CurrencyIllustration:Illustration(type);
            if(!sprite)throw new InvalidOperationException("Missing cube illustration: "+type);
            var material=new Material(EmblemMaterial){name="Cube emblem "+type};emblemOwned.Add(material);
            material.SetTexture("_BaseMap",sprite.texture);
            var r=sprite.textureRect;var texture=sprite.texture;
            var uv=new Rect(r.x/texture.width,r.y/texture.height,r.width/texture.width,r.height/texture.height);
            // Use the complete sprite, including its transparent safety margin.
            // Shop layout bounds are not texture UVs and can cut off the illustration here.
            float aspect=uv.width*texture.width/(uv.height*texture.height);
            float width=.60f*Mathf.Min(1,aspect),height=.60f*Mathf.Min(1,1/aspect);
            Face(new Vector3(0,1.001f,0),Quaternion.Euler(90,0,0),width,height,uv,material);
            for(int side=0;side<4;side++){
                var rotation=Quaternion.Euler(0,side*90,0);
                Face(rotation*new Vector3(0,.5f,-.501f),rotation,width,height,uv,material);
            }
        }
        public static Sprite Illustration(string type)
        {
            string id=Array.Find(ShopCatalog.InventoryIds,item=>ShopCatalog.PowerupType(item)==type);
            if(id==null)return null;
            var extra=ExtendedPowerupArt.Illustration(id,true);if(extra)return extra;
            var shop=RemoteGameArt.Load<ShopArt>("ShopArt");
            int index=Array.FindIndex(ShopCatalog.Products,p=>p.Id==id);
            return shop&&index>=0&&index<shop.products.Length?shop.products[index]:null;
        }
        void Face(Vector3 position,Quaternion rotation,float width,float height,Rect uv,Material material)
        {
            var mesh=new Mesh{name="Power-up face"};emblemOwned.Add(mesh);
            mesh.vertices=new[]{new Vector3(-width/2,-height/2,0),new Vector3(width/2,-height/2,0),new Vector3(-width/2,height/2,0),new Vector3(width/2,height/2,0)};
            mesh.uv=new[]{new Vector2(uv.xMin,uv.yMin),new Vector2(uv.xMax,uv.yMin),new Vector2(uv.xMin,uv.yMax),new Vector2(uv.xMax,uv.yMax)};
            mesh.triangles=new[]{0,2,1,2,3,1};mesh.RecalculateNormals();mesh.RecalculateBounds();
            var face=new GameObject("Power-up emblem",typeof(MeshFilter),typeof(MeshRenderer));
            face.transform.SetParent(emblems,false);face.transform.localPosition=position*BattlePickupSizing.WorldSize;
            face.transform.localRotation=rotation;face.transform.localScale=Vector3.one*BattlePickupSizing.WorldSize;
            face.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=face.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void CreateHalo()
        {
            halo=new GameObject("Animated gold floor halo",typeof(MeshFilter),typeof(MeshRenderer)).transform;
            halo.SetParent(transform,false);halo.localPosition=new Vector3(0,.006f,0);
            var mesh=new Mesh{name="Gold halo arcs"};owned.Add(mesh);
            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();
            float[] innerRadii={.70f,.74f,.77f,.83f},outerRadii={.74f,.77f,.81f,.85f};
            float[] innerAlpha={0,1,.35f,.65f},outerAlpha={.35f,1,0,.65f};
            for(int band=0;band<4;band++)for(int i=0;i<128;i++){
                if(band==3&&i%32>23)continue;
                int start=vertices.Count;
                for(int end=0;end<2;end++)for(int edge=0;edge<2;edge++){
                    float angle=(i+end)*Mathf.PI*2/128,radius=(edge==0?innerRadii[band]:outerRadii[band])/.85f*BattlePickupSizing.TileWorldSize*.5f;
                    vertices.Add(new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));colors.Add(new Color(1,1,1,edge==0?innerAlpha[band]:outerAlpha[band]));
                }
                triangles.AddRange(new[]{start,start+2,start+1,start+1,start+2,start+3});
            }
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            halo.GetComponent<MeshFilter>().sharedMesh=mesh;
            haloInstance=new Material(HaloMaterial);owned.Add(haloInstance);
            var renderer=halo.GetComponent<MeshRenderer>();renderer.sharedMaterial=haloInstance;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void AnimateMotes(float time)
        {
            if(motes.Count==0){
                var mesh=new Mesh{name="Gold energy spark"};owned.Add(mesh);
                mesh.vertices=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.forward,Vector3.right,Vector3.back};
                mesh.triangles=new[]{0,2,3,0,3,4,0,4,5,0,5,2,1,3,2,1,4,3,1,5,4,1,2,5};mesh.RecalculateNormals();mesh.RecalculateBounds();
                for(int i=0;i<6;i++){
                    var mote=new GameObject("Orbiting gold spark",typeof(MeshFilter),typeof(MeshRenderer)).transform;
                    mote.SetParent(transform,false);mote.GetComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=mote.GetComponent<MeshRenderer>();renderer.sharedMaterial=haloInstance;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    motes.Add(mote);
                }
            }
            for(int i=0;i<motes.Count;i++){
                float phase=time*(i%2==0?1.1f:-.8f)+i*Mathf.PI/3;
                float radius=BattlePickupSizing.TileWorldSize*(.43f+.035f*Mathf.Sin(time*2+i));
                motes[i].localPosition=new Vector3(Mathf.Cos(phase)*radius,.1f+.20f*(.5f+.5f*Mathf.Sin(time*2.1f+i*1.6f)),Mathf.Sin(phase)*radius);
                motes[i].localScale=new Vector3(.013f,.027f,.013f)*(.65f+.35f*Mathf.Sin(time*4+i));
                motes[i].localRotation=Quaternion.Euler(0,time*90+i*30,20);
            }
        }
        static void Release(UnityEngine.Object item){if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        void OnDestroy(){foreach(var item in owned)if(item)Release(item);foreach(var item in emblemOwned)if(item)Release(item);}
    }
}
