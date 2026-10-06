using UnityEngine;

namespace BattleCities.UI
{
    /// <summary>Small silver padlock for unavailable tabs, fitted like the Back arrow.</summary>
    public sealed class LockedTabIcon : UnityEngine.UI.MaskableGraphic
    {
        public static void Apply(UnityEngine.UI.Button button,Sprite skin)
        {
            button.interactable=false;
            button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var background=button.GetComponent<UnityEngine.UI.Image>();
            background.sprite=skin;background.overrideSprite=null;background.type=UnityEngine.UI.Image.Type.Sliced;
            background.color=Color.white;background.raycastTarget=true;
            background.CrossFadeColor(Color.white,0,true,true);
            var selection=button.transform.Find("Selection");
            if(selection)selection.GetComponent<UnityEngine.UI.Image>().enabled=false;
            var original=button.transform.Find("Icon");if(original)original.gameObject.SetActive(false);
            var rect=button.transform.Find("Lock") as RectTransform;
            if(!rect)
            {
                var go=new GameObject("Lock",typeof(RectTransform),typeof(CanvasRenderer),typeof(LockedTabIcon));
                go.layer=button.gameObject.layer;rect=(RectTransform)go.transform;rect.SetParent(button.transform,false);
            }
            rect.anchorMin=new Vector2(.075f,.17f);rect.anchorMax=new Vector2(.265f,.83f);
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            var icon=rect.GetComponent<LockedTabIcon>();icon.color=Color.white;icon.raycastTarget=false;
            if(button.targetGraphic!=background)button.targetGraphic.CrossFadeColor(Color.clear,0,true,true);
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();float h=Mathf.Min(rect.height,rect.width/ .76f);
            if(h<=0)return;
            var center=rect.center;var edge=new Color32(6,29,54,255);
            var silver=new Color32(213,234,248,255);
            float radius=h*.24f,thickness=h*.09f;
            Arc(mesh,center+new Vector2(0,h*.20f),radius,thickness,edge);
            Arc(mesh,center+new Vector2(0,h*.20f),radius,thickness*.53f,silver);
            foreach(float sign in new[]{-1f,1f})
            {
                var leg=new Rect(center.x+sign*radius-thickness*.5f,center.y-h*.055f,thickness,h*.255f);
                Quad(mesh,leg,edge);leg.x+=thickness*.23f;leg.width*=.54f;Quad(mesh,leg,silver);
            }
            var body=new Rect(center.x-h*.38f,center.y-h*.50f,h*.76f,h*.62f);
            Body(mesh,body,h*.055f,edge,edge);
            body.x+=h*.025f;body.y+=h*.025f;body.width-=h*.05f;body.height-=h*.05f;
            Body(mesh,body,h*.04f,new Color32(233,247,255,255),new Color32(110,150,182,255));
            var key=center+new Vector2(0,-h*.18f);float keyRadius=h*.064f;
            int start=mesh.currentVertCount;mesh.AddVert(key,edge,Vector2.zero);
            for(int i=0;i<=16;i++)mesh.AddVert(key+new Vector2(Mathf.Cos(i*Mathf.PI/8),Mathf.Sin(i*Mathf.PI/8))*keyRadius,edge,Vector2.zero);
            for(int i=0;i<16;i++)mesh.AddTriangle(start,start+i+1,start+i+2);
            Quad(mesh,new Rect(key.x-h*.034f,key.y-h*.14f,h*.068f,h*.14f),edge);
        }

        static void Arc(UnityEngine.UI.VertexHelper mesh,Vector2 center,float radius,float thickness,Color tint)
        {
            int start=mesh.currentVertCount;
            for(int i=0;i<=20;i++)
            {
                var direction=new Vector2(Mathf.Cos(i*Mathf.PI/20),Mathf.Sin(i*Mathf.PI/20));
                mesh.AddVert(center+direction*(radius-thickness*.5f),tint,Vector2.zero);
                mesh.AddVert(center+direction*(radius+thickness*.5f),tint,Vector2.zero);
                if(i==0)continue;int at=start+i*2;mesh.AddTriangle(at-2,at-1,at);mesh.AddTriangle(at-1,at+1,at);
            }
        }
        static void Body(UnityEngine.UI.VertexHelper mesh,Rect rect,float bevel,Color top,Color bottom)
        {
            var points=new[]{new Vector2(rect.xMin+bevel,rect.yMin),new Vector2(rect.xMax-bevel,rect.yMin),
                new Vector2(rect.xMax,rect.yMin+bevel),new Vector2(rect.xMax,rect.yMax-bevel),
                new Vector2(rect.xMax-bevel,rect.yMax),new Vector2(rect.xMin+bevel,rect.yMax),
                new Vector2(rect.xMin,rect.yMax-bevel),new Vector2(rect.xMin,rect.yMin+bevel)};
            int start=mesh.currentVertCount;mesh.AddVert(rect.center,Color.Lerp(bottom,top,.5f),Vector2.zero);
            foreach(var point in points)mesh.AddVert(point,Color.Lerp(bottom,top,(point.y-rect.yMin)/rect.height),Vector2.zero);
            for(int i=0;i<points.Length;i++)mesh.AddTriangle(start,start+i+1,start+(i+1)%points.Length+1);
        }
        static void Quad(UnityEngine.UI.VertexHelper mesh,Rect rect,Color tint)
        {
            int start=mesh.currentVertCount;
            mesh.AddVert(new Vector2(rect.xMin,rect.yMin),tint,Vector2.zero);mesh.AddVert(new Vector2(rect.xMax,rect.yMin),tint,Vector2.zero);
            mesh.AddVert(new Vector2(rect.xMax,rect.yMax),tint,Vector2.zero);mesh.AddVert(new Vector2(rect.xMin,rect.yMax),tint,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
