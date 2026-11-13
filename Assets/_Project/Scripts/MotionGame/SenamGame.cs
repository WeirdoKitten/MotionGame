using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
namespace FamilyAdventure.Presentation
{
    // 4. Ikuti Senam: a whole senam converted from a real video (Tools/Pose-To-Senam.py, docs/52). The game
    // plays the instructor's movements at the video's own tempo: a guide figure (top right) and a see-through
    // shadow on the child's own body show the instructor's pose, and at each key pose pictures of hands, feet
    // and a face show where the child's hands, feet and head should be. A move done turns into a star;
    // marching on the spot gives a star every few steps. Missing a move costs nothing. Without a pose model
    // the targets react to camera motion like the other games (playable, but not a real copy of the senam).
    public sealed class SenamGame : MotionGame
    {
        // Choreography file in Resources/Motion/Senam.
        public static string Choreography = "senam-pemanasan";
        // Playback speed; 1 = the tempo of the video.
        public static float Speed = 1;
        static readonly Color HandColor = new Color(1, .86f, .3f), FootColor = new Color(.45f, .8f, 1), HeadColor = new Color(.85f, .55f, 1);
        protected override string Instruction => "Ikuti gerakan senamnya!";
        protected override bool HandsAndFeetOnly => false;
        protected override float RoundTime => (Senam != null ? Senam.Duration / Mathf.Max(.1f, Speed) : 60) + 1;
        public SenamChoreography Senam { get; private set; }
        public SenamBody Body { get; private set; }
        public SenamJudge Judge { get; private set; }
        // Position in the senam, in seconds of the video.
        public float SongTime { get; private set; }
        public int PicturesShown => pictures.Count;
        public string LogPath { get; private set; }
        SenamFigure shadow, figure;
        RectTransform panel, march;
        Image[] marchFeet;
        Text marchText;
        readonly Dictionary<SenamJudge.Target, Image> pictures = new Dictionary<SenamJudge.Target, Image>();
        readonly List<SenamJudge.Target> gone = new List<SenamJudge.Target>();
        readonly Vector2[] guide = new Vector2[SenamChoreography.Joints], onBody = new Vector2[SenamChoreography.Joints], inPanel = new Vector2[SenamChoreography.Joints];
        int first, cueMove;
        float cueWait;

        public static SenamChoreography Load(string name)
        {
            var text = Resources.Load<TextAsset>("Motion/Senam/" + name);
            return text != null ? SenamChoreography.Parse(text.text) : null;
        }

        protected override void Setup()
        {
            if (Senam == null) Senam = Load(Choreography) ?? new SenamChoreography { Duration = 30 };
            Body = new SenamBody(); Judge = null; SongTime = 0; first = 0; cueMove = 0; cueWait = 0; LogPath = null;
            pictures.Clear();
            shadow = Figure(area, "Shadow", new Color(1, 1, 1, .35f), 16); UiKit.Anchor(shadow.rectTransform, 0, 0, 1, 1);
            shadow.gameObject.SetActive(false);
            // Guide figure: the instructor, top right, as seen in the video.
            panel = UiKit.Panel("Guide", area, new Color(.08f, .16f, .12f, .55f)); UiKit.Anchor(panel, .76f, .36f, .99f, .9f);
            panel.GetComponent<Image>().raycastTarget = false;
            figure = Figure(panel, "Instructor", new Color(1, .95f, .8f, 1), 9); UiKit.Anchor(figure.rectTransform, 0, 0, 1, 1);
            var label = UiKit.Label(panel, "Kakak senam", 22, Color.white); UiKit.Anchor(label.rectTransform, 0, .9f, 1, 1); label.alignment = TextAnchor.MiddleCenter;
            // Marching on the spot: two feet that light up in the instructor's rhythm, and the child's steps.
            march = UiKit.Rect("March", area); UiKit.Anchor(march, .38f, .01f, .62f, .13f);
            marchFeet = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                marchFeet[i] = MotionUi.Picture(march, "Foot " + i, "star"); marchFeet[i].sprite = LimbIcons.Foot();
                UiKit.Anchor(marchFeet[i].rectTransform, i == 0 ? .02f : .2f, .05f, i == 0 ? .18f : .36f, .95f);
                marchFeet[i].rectTransform.localScale = new Vector3(i == 0 ? -1 : 1, 1, 1);
            }
            marchText = Text(march, "", 28, .38f, 0, 1, 1); marchText.alignment = TextAnchor.MiddleLeft;
            march.gameObject.SetActive(false);
            DrawGuide(0);
        }

        static SenamFigure Figure(Transform parent, string name, Color color, float thickness)
        {
            var f = UiKit.Rect(name, parent).gameObject.AddComponent<SenamFigure>();
            f.color = color; f.thickness = thickness; f.raycastTarget = false; return f;
        }

        // The standing body is measured while the child keeps still (pose model only).
        protected override void Standing()
        {
            if (Sensor.PoseActive && Calibration >= 1) Body.Add(Sensor.Pose);
        }

        protected override void Tick(float dt)
        {
            if (Judge == null) { Judge = new SenamJudge(Senam, Body); shadow.gameObject.SetActive(true); }
            SongTime += dt * Speed;
            Judge.Update(SongTime, Sensor.PoseActive ? Sensor.Pose : null, Sensor.LimbEnergy);
            Score = Judge.Stars;
            DrawGuide(SongTime);
            DrawTargets();
            DrawMarch();
            CueNext(dt);
            if (SongTime >= Senam.Duration)
            {
                WriteLog();
                EndNow();
            }
        }

        void DrawGuide(float time)
        {
            Senam.Guide(time, guide);
            var size = panel.rect.size; float k = .16f, wide = size.x > 0 ? size.y / size.x : 1.6f;
            for (int j = 0; j < SenamChoreography.Joints; j++)
            {
                inPanel[j] = new Vector2(.5f + guide[j].x * k * wide, .45f + guide[j].y * k);
                if (Body != null) onBody[j] = Body.ToScreen(guide[j], SenamChoreography.IsLeg(j), Senam.LegRatio);
            }
            figure.SetPose(inPanel);
            if (Body != null) shadow.SetPose(onBody);
        }

        void DrawTargets()
        {
            var targets = Judge.Targets;
            while (first < targets.Count && targets[first].closes < SongTime - 1) first++;
            for (int i = first; i < targets.Count && targets[i].opens - SenamJudge.Lead <= SongTime; i++)
            {
                var t = targets[i];
                if (!Judge.Visible(t, SongTime) || pictures.ContainsKey(t)) continue;
                var image = Sprite("Target", "star", t.point, .05f);
                image.sprite = t.head ? LimbIcons.Head(!t.move.TiltNotTurn) : t.foot ? LimbIcons.Foot() : LimbIcons.Hand(HandShape.Open);
                pictures[t] = image;
            }
            foreach (var t in Judge.JustCaught) Burst(t.point, t.foot ? FootColor : t.head ? HeadColor : HandColor);
            foreach (var m in Judge.JustCompleted)
            {
                FlyToScore(Judge.Targets.Find(x => x.move == m).point);
                // Praise only when it does not hide the name of the next move.
                if (cueWait <= 0) Cue("Hebat!", .6f);
            }
            gone.Clear();
            foreach (var pair in pictures)
            {
                var t = pair.Key; var image = pair.Value;
                if (!Judge.Visible(t, SongTime)) { gone.Add(t); continue; }
                // Grows in during the lead time, full size and bright while it can be reached.
                float grow = Mathf.Clamp01((SongTime - (t.opens - SenamJudge.Lead)) / SenamJudge.Lead), open = SongTime >= t.opens ? 1 : 0;
                float size = (t.head ? .12f : t.foot ? .11f : .13f) * Mathf.Lerp(.55f, 1, grow) * (1 + open * .06f * Mathf.Sin(SongTime * 10));
                Place(image.rectTransform, t.point, size);
                var color = t.head ? HeadColor : t.foot ? FootColor : HandColor; color.a = Mathf.Lerp(.3f, .65f, grow) + open * .3f; image.color = color;
                bool flip = t.head ? t.move.turn < 0 && !t.move.TiltNotTurn : !t.ScreenRight;
                image.rectTransform.localScale = new Vector3(flip ? -1 : 1, 1, 1);
                float angle = t.head ? (t.move.TiltNotTurn ? t.move.tilt : 0) : t.foot ? 0 : t.Angle - 90;
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            }
            foreach (var t in gone) { Destroy(pictures[t].gameObject); pictures.Remove(t); }
        }

        void DrawMarch()
        {
            var m = Judge.MarchAt(SongTime);
            march.gameObject.SetActive(m != null);
            if (m == null) return;
            // The instructor's higher ankle is the lifted foot (screen-left foot first).
            bool leftUp = guide[(int)SenamChoreography.Joint.SLAnkle].y > guide[(int)SenamChoreography.Joint.SRAnkle].y;
            for (int i = 0; i < 2; i++)
            {
                bool up = (i == 0) == leftUp; var c = FootColor; c.a = up ? 1 : .35f;
                marchFeet[i].color = c; marchFeet[i].rectTransform.anchoredPosition = new Vector2(0, up ? 14 : 0);
            }
            marchText.text = "Jalan di tempat  " + Judge.Steps;
        }

        // Names the next move shortly before it comes, so the child hears what to do.
        void CueNext(float dt)
        {
            cueWait -= dt;
            var moves = Senam.Moves;
            while (cueMove < moves.Count && moves[cueMove].time < SongTime + .6f)
            {
                var m = moves[cueMove++];
                if (cueWait > 0 || m.time < SongTime - .2f) continue;
                string text = MoveName(m);
                if (text == null) continue;
                Cue(text, 1.4f); cueWait = 1.6f;
            }
        }

        public static string MoveName(SenamChoreography.Move m)
        {
            switch (m.kind)
            {
                case SenamChoreography.Kind.Hands:
                    if (m.left.y > 1.6f && m.right.y > 1.6f) return "Angkat tangan ke atas!";
                    if (Mathf.Abs(m.left.x) > 1.2f || Mathf.Abs(m.right.x) > 1.2f) return "Rentangkan tangan!";
                    return null;
                case SenamChoreography.Kind.Foot: return m.raise ? "Angkat lutut!" : "Langkah kaki!";
                case SenamChoreography.Kind.Head: return m.TiltNotTurn ? "Tekuk kepala!" : "Tengok!";
                case SenamChoreography.Kind.Hips: return "Tangan di pinggang!";
                case SenamChoreography.Kind.March: return "Jalan di tempat!";
                default: return null;
            }
        }

        void FlyToScore(Vector2 from)
        {
            var star = Sprite("Caught star", "star", from, .1f);
            star.gameObject.AddComponent<MotionSpark>().velocity = Vector2.Scale(new Vector2(.05f, .93f) - from, area.rect.size) / .6f;
        }

        // Research log (never shown to the child): one line per target, in the app's data folder.
        void WriteLog()
        {
            if (LogPath != null || Judge == null) return;
            try
            {
                var folder = Path.Combine(Application.persistentDataPath, "senam");
                Directory.CreateDirectory(folder);
                LogPath = Path.Combine(folder, $"{Choreography}-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
                using var w = new StreamWriter(LogPath);
                w.WriteLine($"# {Senam.Name}; model {(Sensor.Model.HasValue ? Sensor.Model.Value.ToString() : Sensor.PoseInjected ? "injected" : "camera motion")}; speed {Speed}; " +
                            $"body hip {Body.Hip.x:0.000} {Body.Hip.y:0.000} torso {Body.Torso:0.000} leg {Body.Leg:0.000} measured {Body.Measured}; reach {SenamJudge.Reach}");
                Judge.WriteCsv(w);
            }
            catch (Exception e) { Debug.LogWarning("Senam log not written: " + e.Message); LogPath = ""; }
        }
    }

    // Stick figure of the instructor drawn as thick lines with round joints and a head. Points are
    // normalized to the figure's own rectangle, in SenamChoreography.Joint order.
    public sealed class SenamFigure : MaskableGraphic
    {
        public float thickness = 12;
        readonly Vector2[] points = new Vector2[SenamChoreography.Joints];
        bool hasPose;
        static readonly int[] Bones =
        {
            3, 4, 3, 5, 5, 7, 4, 6, 6, 8, 3, 9, 4, 10, 9, 10, 9, 11, 11, 13, 10, 12, 12, 14,
        };
        public void SetPose(Vector2[] normalized)
        {
            Array.Copy(normalized, points, SenamChoreography.Joints); hasPose = true; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!hasPose) return;
            var r = rectTransform.rect;
            Vector2 P(int j) => new Vector2(r.xMin + points[j].x * r.width, r.yMin + points[j].y * r.height);
            for (int i = 0; i < Bones.Length; i += 2) Line(vh, P(Bones[i]), P(Bones[i + 1]), thickness);
            var neck = (P(3) + P(4)) * .5f;
            Line(vh, neck, P(0), thickness * .8f);
            float head = Mathf.Max(thickness * 1.6f, Vector2.Distance(P(1), P(2)) * .7f);
            Disc(vh, P(0), head);
            foreach (int j in new[] { 7, 8, 13, 14 }) Disc(vh, P(j), thickness * .9f);
        }
        void Line(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            var d = b - a; if (d.sqrMagnitude < 1e-4f) return;
            var n = new Vector2(-d.y, d.x).normalized * width * .5f;
            int i = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector4.zero); vh.AddVert(a + n, color, Vector4.zero); vh.AddVert(b + n, color, Vector4.zero); vh.AddVert(b - n, color, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
            Disc(vh, a, width * .5f); Disc(vh, b, width * .5f);
        }
        void Disc(VertexHelper vh, Vector2 c, float radius)
        {
            const int Sides = 14;
            int centre = vh.currentVertCount;
            vh.AddVert(c, color, Vector4.zero);
            for (int s = 0; s <= Sides; s++)
            {
                float a = s * Mathf.PI * 2 / Sides;
                vh.AddVert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector4.zero);
                if (s > 0) vh.AddTriangle(centre, centre + s, centre + s + 1);
            }
        }
    }
}
