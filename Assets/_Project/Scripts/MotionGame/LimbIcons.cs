using System;
using System.Collections.Generic;
using UnityEngine;
namespace FamilyAdventure.Presentation
{
    public enum HandShape { Open, Fist, Side }

    // Placeholder target pictures for the motion games, drawn in code until the final art exists: a right
    // hand (open, fist or side-on) and a right footprint, as the child sees them in the mirrored camera
    // picture (palm toward the screen, thumb toward the body). Flip horizontally for the left side.
    // The fill is white and the outline dark, so Image.color tints the fill and keeps the outline readable.
    public static class LimbIcons
    {
        const int Size = 256;
        const float Outline = 7;
        static readonly Color Ink = new Color(.18f, .13f, .1f);
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Hand(HandShape shape)
        {
            switch (shape)
            {
                case HandShape.Fist: return Get("hand-fist", FistBody, FistLines);
                case HandShape.Side: return Get("hand-side", SideBody, SideLines);
                default: return Get("hand-open", OpenBody, null);
            }
        }
        public static Sprite Foot() => Get("foot", FootBody, null);
        // A face for head moves: looking straight (rotate it for a tilt) or turned to the screen-right
        // (flip it for the left).
        public static Sprite Head(bool turned) => turned ? Get("head-turn", HeadBody, TurnedFace) : Get("head", HeadBody, Face);

        static float HeadBody(Vector2 p) => Min(Circle(p, 128, 128, 96), Circle(p, 32, 128, 16), Circle(p, 224, 128, 16));
        static float Face(Vector2 p) => Min(Circle(p, 96, 150, 13), Circle(p, 160, 150, 13),
            Segment(p, 92, 98, 110, 84), Segment(p, 110, 84, 146, 84), Segment(p, 146, 84, 164, 98));
        static float TurnedFace(Vector2 p) => Min(Circle(p, 138, 150, 12), Circle(p, 192, 150, 12),
            Segment(p, 140, 94, 158, 82), Segment(p, 158, 82, 186, 84), Segment(p, 186, 84, 200, 96));

        // Shapes are signed distances in texture pixels (negative inside), y up, thumb on the left.
        static float OpenBody(Vector2 p) => Min(
            Box(p, 128, 98, 46, 50, 24),               // palm
            Box(p, 128, 30, 30, 26, 10),               // wrist
            Capsule(p, 98, 140, 82, 210, 13.5f),       // index
            Capsule(p, 121, 146, 118, 230, 14),        // middle
            Capsule(p, 143, 143, 153, 220, 13.5f),     // ring
            Capsule(p, 163, 132, 184, 188, 12),        // little finger
            Capsule(p, 86, 90, 42, 134, 15));          // thumb
        static float FistBody(Vector2 p) => Min(
            Box(p, 128, 118, 54, 50, 28),              // curled fingers
            Box(p, 128, 40, 30, 34, 10),               // wrist
            FistThumb(p));
        static float FistThumb(Vector2 p) => Capsule(p, 84, 102, 134, 94, 15);
        static float FistLines(Vector2 p) => Min(
            Segment(p, 82, 130, 176, 130),             // finger tips folded in
            Segment(p, 103, 132, 103, 164), Segment(p, 128, 132, 128, 166), Segment(p, 153, 132, 153, 164),
            Mathf.Abs(FistThumb(p)));
        static float SideBody(Vector2 p) => Min(
            Box(p, 128, 100, 32, 52, 22),              // edge of the palm
            Box(p, 128, 30, 26, 26, 10),               // wrist
            Capsule(p, 130, 140, 134, 214, 27),        // fingers together
            SideThumb(p));
        static float SideThumb(Vector2 p) => Capsule(p, 100, 96, 94, 152, 13);
        static float SideLines(Vector2 p) => Mathf.Abs(SideThumb(p));
        static float FootBody(Vector2 p) => Min(
            Circle(p, 126, 58, 36),                    // heel
            Capsule(p, 126, 62, 134, 140, 31),         // arch
            Circle(p, 136, 146, 44),                   // ball of the foot
            Circle(p, 98, 206, 17), Circle(p, 128, 216, 12.5f), Circle(p, 152, 212, 11),
            Circle(p, 171, 202, 10), Circle(p, 186, 188, 9));

        static Sprite Get(string key, Func<Vector2, float> body, Func<Vector2, float> lines)
        {
            if (cache.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    float d = body(p), alpha = Mathf.Clamp01(.5f - d);
                    if (alpha <= 0) continue;
                    // Outline band inside the edge, plus inner detail lines (folded fingers, thumb).
                    float fill = Mathf.Clamp01(-d - Outline + .5f);
                    if (lines != null) fill = Mathf.Min(fill, Mathf.Clamp01(lines(p) - Outline * .4f + .5f));
                    var c = Color.Lerp(Ink, Color.white, fill); c.a = alpha;
                    pixels[y * Size + x] = c;
                }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = key, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels); texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100);
            sprite.name = key;
            cache[key] = sprite; return sprite;
        }

        static float Min(params float[] values) { float m = float.MaxValue; foreach (var v in values) m = Mathf.Min(m, v); return m; }
        static float Circle(Vector2 p, float x, float y, float r) => Vector2.Distance(p, new Vector2(x, y)) - r;
        static float Segment(Vector2 p, float ax, float ay, float bx, float by) => Capsule(p, ax, ay, bx, by, 0);
        static float Capsule(Vector2 p, float ax, float ay, float bx, float by, float r)
        {
            var a = new Vector2(ax, ay); var ba = new Vector2(bx, by) - a; var pa = p - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }
        static float Box(Vector2 p, float cx, float cy, float hx, float hy, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x - cx) - hx + r, Mathf.Abs(p.y - cy) - hy + r);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
        }
    }
}
