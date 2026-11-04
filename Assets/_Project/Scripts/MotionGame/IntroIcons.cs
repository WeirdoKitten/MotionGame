using UnityEngine;
using UnityEngine.UI;
namespace FamilyAdventure.Presentation
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class IntroIcon : MaskableGraphic
    {
        public string symbol;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            void Line(float ax,float ay,float bx,float by,float width=5)
            {
                Vector2 a = new Vector2(ax,ay) * rectTransform.rect.width / 64, b = new Vector2(bx,by) * rectTransform.rect.width / 64;
                Vector2 d = (b-a).normalized; Vector2 n = new Vector2(-d.y,d.x)*width/2;
                int i=vh.currentVertCount; vh.AddVert(a-n,color,Vector2.zero); vh.AddVert(a+n,color,Vector2.zero); vh.AddVert(b+n,color,Vector2.zero); vh.AddVert(b-n,color,Vector2.zero);
                vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
            }
            // Filled shapes: a polygon fan around the centre.
            void Fill(params Vector2[] points)
            {
                float k = rectTransform.rect.width / 64; int c = vh.currentVertCount;
                vh.AddVert(Vector2.zero, color, Vector2.zero);
                foreach (var p in points) vh.AddVert(p * k, color, Vector2.zero);
                for (int i = 0; i < points.Length; i++) vh.AddTriangle(c, c + 1 + i, c + 1 + (i + 1) % points.Length);
            }
            Vector2[] Circle(float r, int n = 24) { var p = new Vector2[n]; for (int i = 0; i < n; i++) p[i] = new Vector2(Mathf.Cos(i * Mathf.PI * 2 / n), Mathf.Sin(i * Mathf.PI * 2 / n)) * r; return p; }
            if(symbol=="record") { Fill(Circle(14)); }
            if(symbol=="stop") { Fill(new Vector2(-12,-12), new Vector2(12,-12), new Vector2(12,12), new Vector2(-12,12)); }
            if(symbol=="close") { Line(-13,-13,13,13,8); Line(-13,13,13,-13,8); }
            if(symbol=="leaf")
            {
                var p = new Vector2[20];
                for (int i = 0; i < 20; i++) { float t = i / 19f * Mathf.PI; p[i] = new Vector2(Mathf.Cos(t) * 26, Mathf.Sin(t) * 12); }
                var q = new Vector2[38]; for (int i = 0; i < 19; i++) { q[i] = p[i]; q[37 - i] = new Vector2(p[i + 1].x, -p[i + 1].y); }
                Fill(q);
            }
            if(symbol=="pause") { Line(-9,-15,-9,15,7); Line(9,-15,9,15,7); }
            if(symbol=="play") { Line(-9,-17,15,0); Line(15,0,-9,17); Line(-9,17,-9,-17); }
            if(symbol=="next") { Line(-10,-17,10,0); Line(10,0,-10,17); }
            if(symbol=="previous") { Line(10,-17,-10,0); Line(-10,0,10,17); }
            if(symbol=="home") { Line(-21,0,0,19); Line(0,19,21,0); Line(-15,3,-15,-18); Line(15,3,15,-18); Line(-15,-18,15,-18); }
            if(symbol=="check") { Line(-19,0,-4,-14,7); Line(-4,-14,20,17,7); }
            if(symbol=="speaker") { Line(-18,-6,-8,-6); Line(-18,6,-8,6); Line(-18,-6,-18,6); Line(-8,-6,4,-16); Line(4,-16,4,16); Line(4,16,-8,6); Line(13,-11,17,0); Line(17,0,13,11); }
            if(symbol=="retry") { for(int j=0;j<15;j++){float a=j*.34f;Line(Mathf.Cos(a)*17,Mathf.Sin(a)*17,Mathf.Cos(a+.34f)*17,Mathf.Sin(a+.34f)*17);} Line(17,0,22,12);Line(17,0,6,6); }
            if(symbol=="hand") { Line(-9,-18,-17,-3,8);Line(-17,-3,-9,-5,8);Line(-9,-5,-9,21,8);Line(-9,21,-4,23,8);Line(-4,23,-2,0,8);Line(-2,0,6,8,8);Line(6,8,17,1,8);Line(17,1,14,-20,8);Line(-9,-18,14,-20,8); }
        }
    }
}
