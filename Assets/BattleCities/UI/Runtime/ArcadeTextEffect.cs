using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>Shared outline and shadow geometry for legacy uGUI lettering.</summary>
    [RequireComponent(typeof(Text))]
    public abstract class ArcadeTextEffect : BaseMeshEffect
    {
        protected abstract bool Gold { get; }
        readonly List<UIVertex> source=new List<UIVertex>();
        readonly List<UIVertex> styled=new List<UIVertex>();
        static readonly Vector2[] OutlineOffsets=
        {
            new Vector2(-1,-1),new Vector2(0,-1),new Vector2(1,-1),new Vector2(-1,0),
            new Vector2(1,0),new Vector2(-1,1),new Vector2(0,1),new Vector2(1,1)
        };

        public override void ModifyMesh(VertexHelper mesh)
        {
            if(!IsActive()||mesh.currentVertCount==0)return;
            source.Clear();styled.Clear();mesh.GetUIVertexStream(source);
            var label=(Text)graphic;
            float size=label.resizeTextForBestFit?label.cachedTextGenerator.fontSizeUsedForBestFit:label.fontSize;
            if(size<=0f)size=label.fontSize;
            float bottom=float.MaxValue,top=float.MinValue;
            foreach(var vertex in source)
            {
                bottom=Mathf.Min(bottom,vertex.position.y);
                top=Mathf.Max(top,vertex.position.y);
            }
            AddLayer(new Vector2(size*(Gold?.01f:.006f),-size*(Gold?.05f:.04f)),ArcadeTextStyles.TextShadow);
            foreach(var offset in OutlineOffsets)
                AddLayer(offset*(size*(Gold?.03f:.025f)),Gold?ArcadeTextStyles.GoldOutline:ArcadeTextStyles.WhiteOutline);
            foreach(var original in source)
            {
                var vertex=original;
                var tint=Gold?Color.Lerp(ArcadeTextStyles.GoldBottom,ArcadeTextStyles.GoldTop,
                    Mathf.InverseLerp(bottom,top,vertex.position.y)):Color.white;
                tint.a*=original.color.a/255f;
                vertex.color=tint;
                styled.Add(vertex);
            }
            mesh.Clear();mesh.AddUIVertexTriangleStream(styled);
        }

        void AddLayer(Vector2 offset,Color32 tint)
        {
            foreach(var original in source)
            {
                var vertex=original;
                vertex.position+=new Vector3(offset.x,offset.y,0f);
                var color=tint;
                color.a=(byte)(tint.a*(original.color.a/255f));
                vertex.color=color;
                styled.Add(vertex);
            }
        }
    }
}
