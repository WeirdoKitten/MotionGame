using UnityEngine;
namespace FamilyAdventure.Presentation
{
    // Arm circle (putar lengan): a long arm turning around its shoulder, like a windmill facing the camera.
    // The angle of the wrist around the shoulder is unwrapped and added up per arm; 360 degrees is one circle.
    // Angles use the camera picture's real proportions (u is scaled by the aspect), counterclockwise on the
    // (mirrored) screen is positive. Circles must be drawn facing the camera: a circle to the side of the body
    // only moves the wrist up and down in the picture and is not counted.
    public sealed class ArmCircleDetector : IBasicMoveDetector
    {
        public enum Side { Left, Right }
        public float MinRadius = .45f;    // wrist at least this far from the shoulder (fraction of arm length)
        public float MaxStep = 150;       // bigger jumps between samples are ambiguous and skipped (degrees)
        public float StallTime = 1.5f;    // seconds without turning end an unfinished circle
        public float GapTolerance = .6f;  // missed samples shorter than this keep the circle going
        public float AttemptAngle = 180;  // unfinished circles larger than this count as incomplete
        public float ArmFromTorso = 1f;   // arm length guess from the torso when the arms are not seen
        public const int BaselineSamples = 6;

        sealed class Arm
        {
            public bool hasAngle; public float angle, sum, start, moved, lowest, peak;
            public int count, direction;
        }
        readonly Arm[] arms = { new Arm(), new Arm() };
        int baseSamples, armSamples; float sumArm, sumTorso, armLength;
        float lastGood = float.NegativeInfinity;

        public string Move => "arm circle";
        public bool HasBaseline => baseSamples >= BaselineSamples;
        public bool Tracking { get; private set; }
        public int Count { get; private set; }
        public int Incomplete { get; private set; }
        public float Confidence { get; private set; }
        public MoveRecord Last { get; private set; }
        public string LastMiss { get; private set; } = "";
        // The arm turning most right now, and per-arm values.
        public Side Active => Mathf.Abs(arms[1].sum) > Mathf.Abs(arms[0].sum) ? Side.Right : Side.Left;
        public float Turned(Side side) => arms[(int)side].sum;
        public int Circles(Side side) => arms[(int)side].count;
        public int LastDirection(Side side) => arms[(int)side].direction;
        public float Radius(Side side) => radius[(int)side];
        public float ArmLength => armLength;
        readonly float[] radius = new float[2];
        // Degrees turned in the current circle of the most active arm (sign = direction).
        public float Angle => Turned(Active);
        public float Amount => Mathf.Abs(Angle);
        public float Peak => arms[(int)Active].peak;
        public int Direction => Angle > 20 ? 1 : Angle < -20 ? -1 : 0;
        public float Progress => Mathf.Clamp01(Amount / 360);

        public void Reset()
        {
            ClearBaseline(); Count = Incomplete = 0; Last = default; Confidence = 0; LastMiss = "";
            foreach (var a in arms) a.count = a.direction = 0;
        }
        public void ClearBaseline()
        {
            baseSamples = armSamples = 0; sumArm = sumTorso = armLength = 0; Tracking = false;
            lastGood = float.NegativeInfinity;
            foreach (var a in arms) { a.hasAngle = false; a.sum = a.peak = 0; }
        }

        // Standing with the arms hanging down: the wrist-to-shoulder distance is the arm length.
        public void Calibrate(PoseResult pose, float time)
        {
            if (pose == null || !pose.found || !Torso(pose, out float torso)) return;
            baseSamples++; sumTorso += torso; lastGood = time;
            for (int i = 0; i < 2; i++)
                if (Joints(pose, i, out var shoulder, out var wrist)) { armSamples++; sumArm += Distance(pose, shoulder, wrist); }
            float guess = ArmFromTorso * sumTorso / baseSamples;
            // A bent arm at calibration would make circles too easy: never shorter than the torso guess.
            armLength = armSamples > 0 ? Mathf.Max(sumArm / armSamples, .8f * guess) : guess;
            if (HasBaseline) Tracking = true;
        }

        public void Wait(float time)
        {
            if (HasBaseline && Tracking && time - lastGood > GapTolerance) Lose();
        }

        public void Step(PoseResult pose, float time)
        {
            if (!HasBaseline) { Calibrate(pose, time); return; }
            bool any = false; float confidence = 0;
            for (int i = 0; i < 2; i++)
            {
                var arm = arms[i];
                if (pose == null || !pose.found || !Joints(pose, i, out var shoulder, out var wrist)) { radius[i] = 0; continue; }
                any = true;
                var d = Offset(pose, shoulder, wrist); radius[i] = d.magnitude / armLength;
                confidence = Mathf.Max(confidence, Mathf.Min(pose.Visibility(shoulder), pose.Visibility(wrist)));
                // A short arm (bent elbow, arm pointing at the camera) gives no reliable angle.
                if (radius[i] < MinRadius) continue;
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                if (arm.hasAngle)
                {
                    float step = Mathf.DeltaAngle(arm.angle, angle);
                    if (Mathf.Abs(step) <= MaxStep) Turn(arm, step, time, confidence);
                }
                arm.angle = angle; arm.hasAngle = true;
            }
            if (!any)
            {
                Confidence *= .7f;
                if (Tracking && time - lastGood > GapTolerance) Lose();
                return;
            }
            Tracking = true; lastGood = time;
            Confidence = Mathf.Lerp(Confidence, confidence, .5f);
            // A circle that stops turning for a while is over.
            foreach (var arm in arms)
                if (arm.sum != 0 && time - arm.moved > StallTime) End(arm, "berhenti");
        }

        void Turn(Arm arm, float step, float time, float confidence)
        {
            if (arm.sum == 0) { arm.start = arm.moved = time; arm.lowest = confidence; arm.peak = 0; }
            arm.sum += step;
            // Only turning further than before counts as progress; jitter around a stopped arm does not.
            if (Mathf.Abs(arm.sum) > arm.peak + 2) arm.moved = time;
            arm.lowest = Mathf.Min(arm.lowest, confidence);
            arm.peak = Mathf.Max(arm.peak, Mathf.Abs(arm.sum));
            if (Mathf.Abs(arm.sum) < 360) return;
            // One full circle: count it and keep any extra turning for the next one.
            int direction = arm.sum > 0 ? 1 : -1;
            arm.count++; arm.direction = direction; Count++;
            Last = new MoveRecord { move = Move, start = arm.start, end = time, peak = 360, confidence = arm.lowest, direction = direction };
            arm.sum -= 360 * direction; arm.start = time; arm.peak = Mathf.Abs(arm.sum);
        }

        void End(Arm arm, string why)
        {
            if (arm.peak >= AttemptAngle) { Incomplete++; LastMiss = why; }
            arm.sum = 0; arm.peak = 0;
        }

        void Lose()
        {
            // Circles in progress are dropped, never counted.
            Tracking = false;
            foreach (var arm in arms) { End(arm, "badan hilang"); arm.hasAngle = false; }
        }

        static void Pair(int side, out PoseJoint shoulder, out PoseJoint wrist)
        {
            shoulder = side == 0 ? PoseJoint.LeftShoulder : PoseJoint.RightShoulder;
            wrist = side == 0 ? PoseJoint.LeftWrist : PoseJoint.RightWrist;
        }
        static bool Joints(PoseResult p, int side, out PoseJoint shoulder, out PoseJoint wrist)
        {
            Pair(side, out shoulder, out wrist);
            return p.Visible(shoulder) && p.Visible(wrist);
        }
        // Torso length (shoulders to hips), needed for the scale.
        static bool Torso(PoseResult p, out float torso)
        {
            torso = 0;
            bool shoulders = p.Visible(PoseJoint.LeftShoulder) || p.Visible(PoseJoint.RightShoulder);
            bool hips = p.Visible(PoseJoint.LeftHip) || p.Visible(PoseJoint.RightHip);
            if (!shoulders || !hips) return false;
            torso = Mathf.Abs(Mid(p, PoseJoint.LeftShoulder, PoseJoint.RightShoulder).y - Mid(p, PoseJoint.LeftHip, PoseJoint.RightHip).y);
            return torso > .02f;
        }
        static Vector2 Mid(PoseResult p, PoseJoint a, PoseJoint b) =>
            p.Visible(a) && p.Visible(b) ? (p[a] + p[b]) * .5f : p.Visible(a) ? p[a] : p[b];
        // Offset in real proportions (height units).
        static Vector2 Offset(PoseResult p, PoseJoint from, PoseJoint to) { var d = p[to] - p[from]; d.x *= p.aspect; return d; }
        static float Distance(PoseResult p, PoseJoint from, PoseJoint to) => Offset(p, from, to).magnitude;
    }
}
