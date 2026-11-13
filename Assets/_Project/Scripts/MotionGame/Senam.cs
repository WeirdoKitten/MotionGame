using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace FamilyAdventure.Presentation
{
    // A senam (exercise routine) converted from a video by Tools/Pose-To-Senam.py: the instructor's key poses
    // become targets for the hands, feet and head, and the whole pose track drives the guide figure.
    // Points are in screen space (x right, y up), relative to the instructor's standing hip, in torso lengths
    // (hip centre to shoulder centre). "SL"/"SR" = seen on the screen-left/right. The child copies the
    // instructor like a mirror and sees their own camera picture mirrored, so SR is the child's right side.
    // Plain C# (no engine calls), so the converter output can be checked outside Unity too.
    public sealed class SenamChoreography
    {
        public enum Kind { Hands, Foot, Head, Hips, March, Shift }
        public sealed class Move
        {
            public Kind kind;
            public float time, end;
            // Hands and hips: the screen-left and screen-right wrist. Foot: the foot is in left.
            public Vector2 left, right;
            // Forearm direction in degrees (0 = to the right, 90 = up).
            public float leftAngle, rightAngle;
            public bool screenRight, raise;   // foot: which foot; knee raise (else a step out)
            public float tilt, turn;          // head: tilt (+ = toward the screen-left), turn (+ = nose to the screen-right)
            public int steps;                 // march
            public float shift;               // hips moved sideways
            public bool TiltNotTurn => Mathf.Abs(tilt) / 7 >= Mathf.Abs(turn) / .4f;
        }
        // Guide figure joints, in file order.
        public enum Joint { Nose, SLEar, SREar, SLShoulder, SRShoulder, SLElbow, SRElbow, SLWrist, SRWrist, SLHip, SRHip, SLKnee, SRKnee, SLAnkle, SRAnkle }
        public const int Joints = 15;
        public static bool IsLeg(int joint) => joint >= (int)Joint.SLKnee;

        public string Name = "Senam";
        public float Duration, LegRatio = 1.3f;
        public Vector2 NeutralLeftHand, NeutralRightHand, NeutralLeftFoot, NeutralRightFoot;
        public readonly List<Move> Moves = new List<Move>();
        readonly List<float> guideTimes = new List<float>();
        readonly List<Vector2[]> guidePoses = new List<Vector2[]>();
        public int GuideFrames => guideTimes.Count;

        public static SenamChoreography Parse(string text)
        {
            var c = new SenamChoreography();
            float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
            Vector2 V(string[] f, int i) => new Vector2(F(f[i]), F(f[i + 1]));
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var f = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (f[0])
                {
                    case "name": c.Name = line.Substring(5).Trim(); break;
                    case "duration": c.Duration = F(f[1]); break;
                    case "legratio": c.LegRatio = F(f[1]); break;
                    case "neutral": c.NeutralLeftHand = V(f, 1); c.NeutralRightHand = V(f, 3); c.NeutralLeftFoot = V(f, 5); c.NeutralRightFoot = V(f, 7); break;
                    case "g":
                        var pose = new Vector2[Joints];
                        for (int j = 0; j < Joints; j++) pose[j] = V(f, 2 + 2 * j);
                        c.guideTimes.Add(F(f[1])); c.guidePoses.Add(pose);
                        break;
                    case "hands":
                        c.Moves.Add(new Move { kind = Kind.Hands, time = F(f[1]), end = F(f[1]) + F(f[2]), left = V(f, 3), leftAngle = F(f[5]), right = V(f, 6), rightAngle = F(f[8]) });
                        break;
                    case "foot":
                        c.Moves.Add(new Move { kind = Kind.Foot, time = F(f[1]), end = F(f[1]) + F(f[2]), screenRight = f[3] == "SR", left = V(f, 4), raise = f[6] == "raise" });
                        break;
                    case "head": c.Moves.Add(new Move { kind = Kind.Head, time = F(f[1]), end = F(f[1]), tilt = F(f[2]), turn = F(f[3]) }); break;
                    case "hips": c.Moves.Add(new Move { kind = Kind.Hips, time = F(f[1]), end = F(f[2]), left = V(f, 3), right = V(f, 5), leftAngle = -60, rightAngle = -120 }); break;
                    case "march": c.Moves.Add(new Move { kind = Kind.March, time = F(f[1]), end = F(f[2]), steps = int.Parse(f[3], CultureInfo.InvariantCulture) }); break;
                    case "shift": c.Moves.Add(new Move { kind = Kind.Shift, time = F(f[1]), end = F(f[1]), shift = F(f[2]) }); break;
                }
            }
            c.Moves.Sort((a, b) => a.time.CompareTo(b.time));
            if (c.Duration <= 0 && c.guideTimes.Count > 0) c.Duration = c.guideTimes[c.guideTimes.Count - 1];
            return c;
        }

        // The instructor's pose at a time, interpolated between guide frames.
        public void Guide(float time, Vector2[] into)
        {
            if (guideTimes.Count == 0) return;
            int i = guideTimes.BinarySearch(time);
            if (i < 0) i = ~i;
            if (i <= 0) { Array.Copy(guidePoses[0], into, Joints); return; }
            if (i >= guideTimes.Count) { Array.Copy(guidePoses[guideTimes.Count - 1], into, Joints); return; }
            float a = guideTimes[i - 1], b = guideTimes[i], k = b > a ? (time - a) / (b - a) : 0;
            for (int j = 0; j < Joints; j++) into[j] = Vector2.Lerp(guidePoses[i - 1][j], guidePoses[i][j], k);
        }
    }

    // The child's standing body, measured while the child stands still in the guide, so the instructor's
    // points land on the child's own body: torso lengths for the upper body, leg length for the legs.
    // Points are normalized screen coordinates (u right, v up) like the motion grid and the pose results.
    public sealed class SenamBody
    {
        // Without a pose the standing guide drawn on the calibration screen sets the body.
        public Vector2 Hip = new Vector2(.5f, .42f);
        public float Torso = .25f, Leg = .34f, Aspect = 4f / 3;
        int samples; Vector2 hipSum; float torsoSum, legSum, aspectSum;
        public bool Measured => samples >= 5;
        public void Add(PoseResult pose)
        {
            if (pose == null || !pose.found) return;
            if (!pose.Visible(PoseJoint.LeftHip) || !pose.Visible(PoseJoint.RightHip) || !pose.Visible(PoseJoint.LeftShoulder) || !pose.Visible(PoseJoint.RightShoulder)) return;
            var hip = (pose[PoseJoint.LeftHip] + pose[PoseJoint.RightHip]) * .5f;
            var shoulder = (pose[PoseJoint.LeftShoulder] + pose[PoseJoint.RightShoulder]) * .5f;
            float torso = new Vector2((shoulder.x - hip.x) * pose.aspect, shoulder.y - hip.y).magnitude;
            if (torso < .02f) return;
            float leg = pose.Visible(PoseJoint.LeftAnkle) && pose.Visible(PoseJoint.RightAnkle)
                ? hip.y - (pose[PoseJoint.LeftAnkle].y + pose[PoseJoint.RightAnkle].y) * .5f : torso * 1.3f;
            samples++; hipSum += hip; torsoSum += torso; legSum += Mathf.Max(leg, torso * .6f); aspectSum += pose.aspect;
            Hip = hipSum / samples; Torso = torsoSum / samples; Leg = legSum / samples; Aspect = aspectSum / samples;
        }
        // An instructor point (torso lengths from the hip) on the child's screen. Leg joints use the leg length.
        public Vector2 ToScreen(Vector2 body, bool leg, float legRatio)
        {
            float down = leg && body.y < 0 ? Leg / Mathf.Max(.3f, legRatio) : Torso;
            return new Vector2(Hip.x + body.x * Torso / Aspect, Hip.y + body.y * down);
        }
        // Distance between two screen points in the child's torso lengths.
        public float Distance(Vector2 a, Vector2 b) => new Vector2((a.x - b.x) * Aspect, a.y - b.y).magnitude / Torso;
    }

    // Follows the child through a senam: which targets are open, which the child reached, and the stars.
    // A move gives a star once all of its targets are reached (both hands for a hand pose). Missing a move
    // costs nothing. With a pose, hands must be at the hand targets, feet at the foot targets, the head
    // tilted or turned the same way, elbows out for hands on the hips; without a pose, camera motion at the
    // target counts (like the other games). Every target keeps its result for research (WriteCsv).
    public sealed class SenamJudge
    {
        public const float Lead = .8f, Early = .35f, Late = .45f, StepsPerStar = 4;
        // Catch radius in the child's torso lengths (hip to shoulder); tuned with the converted senam (docs/52).
        public static float Reach = .35f;
        public sealed class Target
        {
            public SenamChoreography.Move move;
            public int part;            // 0 = screen-left (or the only one), 1 = screen-right
            public Vector2 point;       // where the target is drawn and reached, screen coordinates
            public float opens, closes;
            public bool foot, head, hips;
            public bool caught;
            public float caughtAt = -1, best = float.MaxValue;
            public float Angle => part == 0 ? move.leftAngle : move.rightAngle;
            public bool ScreenRight => foot ? move.screenRight : part == 1;
        }
        public readonly SenamChoreography Senam;
        public readonly SenamBody Body;
        public readonly List<Target> Targets = new List<Target>();
        public readonly List<SenamChoreography.Move> Marches = new List<SenamChoreography.Move>();
        public readonly List<Target> JustCaught = new List<Target>();
        public readonly List<SenamChoreography.Move> JustCompleted = new List<SenamChoreography.Move>();
        public int Stars { get; private set; }
        public int MovesCompleted { get; private set; }
        public int Steps { get; private set; }
        public int MarchStars { get; private set; }
        readonly Dictionary<SenamChoreography.Move, int> missing = new Dictionary<SenamChoreography.Move, int>();
        readonly Vector2[] guide = new Vector2[SenamChoreography.Joints];
        int stepState, stepsAtStar, next;
        float lift, lastStep = -10;

        public SenamJudge(SenamChoreography senam, SenamBody body)
        {
            Senam = senam; Body = body;
            foreach (var m in senam.Moves)
                switch (m.kind)
                {
                    case SenamChoreography.Kind.Hands:
                    case SenamChoreography.Kind.Hips:
                        bool hips = m.kind == SenamChoreography.Kind.Hips;
                        float closes = hips ? m.end : Mathf.Max(m.time, m.end) + Late;
                        Add(m, 0, Body.ToScreen(m.left, false, senam.LegRatio), m.time - Early, closes, hips: hips);
                        Add(m, 1, Body.ToScreen(m.right, false, senam.LegRatio), m.time - Early, closes, hips: hips);
                        break;
                    case SenamChoreography.Kind.Foot:
                        Add(m, 0, Body.ToScreen(m.left, true, senam.LegRatio), m.time - Early, Mathf.Max(m.time, m.end) + Late, foot: true);
                        break;
                    case SenamChoreography.Kind.Head:
                        senam.Guide(m.time, guide);
                        var above = Body.ToScreen(guide[(int)SenamChoreography.Joint.Nose] + new Vector2(0, .6f), false, senam.LegRatio);
                        Add(m, 0, above, m.time - .4f, m.time + .6f, head: true);
                        break;
                    case SenamChoreography.Kind.March: Marches.Add(m); break;
                }
            Targets.Sort((a, b) => a.opens.CompareTo(b.opens));
        }
        void Add(SenamChoreography.Move m, int part, Vector2 point, float opens, float closes, bool foot = false, bool head = false, bool hips = false)
        {
            point = new Vector2(Mathf.Clamp(point.x, .03f, .97f), Mathf.Clamp(point.y, .03f, .95f));
            Targets.Add(new Target { move = m, part = part, point = point, opens = opens, closes = closes, foot = foot, head = head, hips = hips });
            missing[m] = missing.TryGetValue(m, out var n) ? n + 1 : 1;
        }

        public bool Visible(Target t, float time) => time >= t.opens - Lead && time <= t.closes && !t.caught;
        public SenamChoreography.Move MarchAt(float time)
        {
            foreach (var m in Marches) if (time >= m.time && time <= m.end) return m;
            return null;
        }

        // One frame. pose: the child's pose (null or not found = camera motion only). motion(point, radius, feet):
        // camera motion near a point, used without a pose.
        public void Update(float time, PoseResult pose, Func<Vector2, float, bool, float> motion)
        {
            JustCaught.Clear(); JustCompleted.Clear();
            bool usePose = pose != null && pose.found;
            while (next < Targets.Count && Targets[next].closes < time - 5) next++;
            for (int i = next; i < Targets.Count; i++)
            {
                var t = Targets[i];
                if (t.opens > time) break;
                if (t.caught || time > t.closes) continue;
                bool hit;
                if (usePose)
                {
                    float d = t.head ? HeadMiss(pose, t.move) : Nearest(pose, t);
                    t.best = Mathf.Min(t.best, d);
                    hit = d <= Reach && (!t.hips || ElbowsOut(pose));
                }
                else hit = motion != null && motion(t.point, .07f, t.foot) > .35f;
                if (!hit) continue;
                t.caught = true; t.caughtAt = time; JustCaught.Add(t);
                if (--missing[t.move] == 0) { Stars++; MovesCompleted++; JustCompleted.Add(t.move); }
            }
            CountSteps(time, usePose ? pose : null, motion);
        }

        // Closest matching hand (or foot) to the target, in torso lengths. Either side counts.
        float Nearest(PoseResult pose, Target t)
        {
            var a = t.foot ? PoseJoint.LeftAnkle : PoseJoint.LeftWrist;
            var b = t.foot ? PoseJoint.RightAnkle : PoseJoint.RightWrist;
            float best = float.MaxValue;
            if (pose.Visible(a, .3f)) best = Mathf.Min(best, Body.Distance(pose[a], t.point));
            if (pose.Visible(b, .3f)) best = Mathf.Min(best, Body.Distance(pose[b], t.point));
            return best;
        }

        // Elbows further out to the sides than the wrists (hands on the hips).
        bool ElbowsOut(PoseResult pose)
        {
            float centre = (pose[PoseJoint.LeftHip].x + pose[PoseJoint.RightHip].x) * .5f;
            float Out(PoseJoint elbow, PoseJoint wrist) => (Mathf.Abs(pose[elbow].x - centre) - Mathf.Abs(pose[wrist].x - centre)) * Body.Aspect / Body.Torso;
            return Out(PoseJoint.LeftElbow, PoseJoint.LeftWrist) > .1f && Out(PoseJoint.RightElbow, PoseJoint.RightWrist) > .1f;
        }

        // Head tilt (degrees, + = toward the screen-left) and turn (nose offset / ear distance, + = to the
        // screen-right), measured like the converter does on the instructor.
        public static bool Head(PoseResult pose, float aspect, out float tilt, out float turn)
        {
            tilt = turn = 0;
            if (pose == null || !pose.found || !pose.Visible(PoseJoint.LeftEar, .3f) || !pose.Visible(PoseJoint.RightEar, .3f)) return false;
            Vector2 Iso(PoseJoint j) => new Vector2(pose[j].x * aspect, pose[j].y);
            var ear = Iso(PoseJoint.RightEar) - Iso(PoseJoint.LeftEar);
            var shoulders = Iso(PoseJoint.RightShoulder) - Iso(PoseJoint.LeftShoulder);
            tilt = Mathf.DeltaAngle(Mathf.Atan2(shoulders.y, shoulders.x) * Mathf.Rad2Deg, Mathf.Atan2(ear.y, ear.x) * Mathf.Rad2Deg);
            var mid = (Iso(PoseJoint.LeftEar) + Iso(PoseJoint.RightEar)) * .5f;
            turn = (Iso(PoseJoint.Nose).x - mid.x) / Mathf.Max(1e-3f, Mathf.Abs(ear.x));
            return true;
        }
        // 0 when the head matches the move well, growing as it differs (Reach or less counts as a match).
        float HeadMiss(PoseResult pose, SenamChoreography.Move m)
        {
            if (!Head(pose, Body.Aspect, out var tilt, out var turn)) return float.MaxValue;
            float want = m.TiltNotTurn ? m.tilt : m.turn, have = m.TiltNotTurn ? tilt : turn;
            float minimum = m.TiltNotTurn ? Mathf.Max(4, Mathf.Abs(want) * .5f) : Mathf.Max(.15f, Mathf.Abs(want) * .5f);
            if (Mathf.Sign(want) != Mathf.Sign(have)) return 2;
            return Mathf.Abs(have) >= minimum ? 0 : Reach + (minimum - Mathf.Abs(have)) / minimum;
        }

        // Marching on the spot: one step each time the other foot is the higher one; a star every 4 steps.
        void CountSteps(float time, PoseResult pose, Func<Vector2, float, bool, float> motion)
        {
            var march = MarchAt(time);
            if (march == null) { stepState = 0; return; }
            int state = stepState;
            if (pose != null)
            {
                if (!pose.Visible(PoseJoint.LeftAnkle, .3f) || !pose.Visible(PoseJoint.RightAnkle, .3f)) return;
                // Smoothed ankle height difference with a dead band, so pose jitter does not count as steps.
                float now = (pose[PoseJoint.LeftAnkle].y - pose[PoseJoint.RightAnkle].y) / Mathf.Max(.01f, Body.Torso);
                lift = Mathf.Lerp(lift, now, .5f);
                if (lift > .08f) state = 1; else if (lift < -.08f) state = -1;
            }
            else if (motion != null)
            {
                float y = Mathf.Max(.06f, Body.Hip.y - Body.Leg * .8f);
                float left = motion(new Vector2(Body.Hip.x - .05f, y), .06f, true), right = motion(new Vector2(Body.Hip.x + .05f, y), .06f, true);
                if (left > .35f && left > right) state = 1; else if (right > .35f && right > left) state = -1;
            }
            if (state != stepState && stepState != 0 && time - lastStep > .15f) { Steps++; lastStep = time; }
            if (state != stepState) stepState = state;
            if (Steps - stepsAtStar >= StepsPerStar) { stepsAtStar = Steps; Stars++; MarchStars++; }
        }

        // One line per target, for research only (never shown to the child).
        public void WriteCsv(TextWriter w)
        {
            w.WriteLine("kind,time,part,caught,delay_s,closest_torso");
            foreach (var t in Targets)
                w.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:0.000},{2},{3},{4},{5}", t.move.kind, t.move.time, t.part,
                    t.caught ? 1 : 0, t.caught ? (t.caughtAt - t.move.time).ToString("0.000", CultureInfo.InvariantCulture) : "",
                    t.best < float.MaxValue ? t.best.ToString("0.000", CultureInfo.InvariantCulture) : ""));
            w.WriteLine(string.Format(CultureInfo.InvariantCulture, "march,steps,{0},stars,{1},,", Steps, MarchStars));
        }
    }
}
