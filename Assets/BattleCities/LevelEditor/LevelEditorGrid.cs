using UnityEngine;

namespace BattleCities.LevelEditor
{
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class LevelEditorGrid : UnityEngine.UI.MaskableGraphic
    {
        public LevelEditorDocument Document;
        protected override void OnEnable() { base.OnEnable(); raycastTarget = false; }
        private void Update() { if (Document) SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear(); if (!Document || !Document.PreviewCamera) return;
            Vector2 Point(float x, float y)
            {
                var uv = Document.PreviewCamera.WorldToViewportPoint(new Vector3(x / 64, 0, -y / 64));
                return new Vector2(rectTransform.rect.xMin + uv.x * rectTransform.rect.width, rectTransform.rect.yMin + uv.y * rectTransform.rect.height);
            }
            void Line(Vector2 a, Vector2 b, Color c, float thickness)
            {
                a.x = Mathf.Clamp(a.x, rectTransform.rect.xMin, rectTransform.rect.xMax); b.x = Mathf.Clamp(b.x, rectTransform.rect.xMin, rectTransform.rect.xMax);
                a.y = Mathf.Clamp(a.y, rectTransform.rect.yMin, rectTransform.rect.yMax); b.y = Mathf.Clamp(b.y, rectTransform.rect.yMin, rectTransform.rect.yMax);
                var normal = new Vector2(-(b - a).y, (b - a).x).normalized * thickness * .5f; int i = vh.currentVertCount;
                vh.AddVert(a - normal, c, Vector2.zero); vh.AddVert(a + normal, c, Vector2.zero); vh.AddVert(b + normal, c, Vector2.zero); vh.AddVert(b - normal, c, Vector2.zero);
                vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
            }
            var faint = new Color(.6f, .8f, .9f, .18f);
            int step = Document.Snap == 16 || Document.Snap == 32 ? Document.Snap : 64;
            var minor = new Color(.6f,.8f,.9f,.10f);
            for(int x=step;x<Document.WidthTiles*64;x+=step)if(x%64!=0)Line(Point(x,0),Point(x,Document.HeightTiles*64),minor,.55f);
            for(int y=step;y<Document.HeightTiles*64;y+=step)if(y%64!=0)Line(Point(0,y),Point(Document.WidthTiles*64,y),minor,.55f);
            for (int x = 0; x <= Document.WidthTiles; x++) Line(Point(x * 64, 0), Point(x * 64, Document.HeightTiles * 64), faint, .8f);
            for (int y = 0; y <= Document.HeightTiles; y++) Line(Point(0, y * 64), Point(Document.WidthTiles * 64, y * 64), faint, .8f);
            if (Document.Selected)
            {
                var b = Document.Selected.Bounds; var a = Point(b.X, b.Y); var c = Point(b.Right, b.Bottom);
                Line(a, new Vector2(c.x, a.y), Color.cyan, 2); Line(new Vector2(c.x, a.y), c, Color.cyan, 2);
                Line(c, new Vector2(a.x, c.y), Color.cyan, 2); Line(new Vector2(a.x, c.y), a, Color.cyan, 2);
            }
        }
    }
}
