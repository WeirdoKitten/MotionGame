using UnityEngine;
namespace FamilyAdventure.Presentation
{
    // Body turn (memutar badan): which way the body faces. Front, the child's own left, back, right, in
    // quarter turns (left = counterclockwise seen from above = negative). Each pose sample is judged from:
    // - shoulder width against torso length, compared with the standing baseline (narrow = side view);
    // - the face (nose/eyes seen = front half, hidden = back half);
    // - which way: the nose off the shoulder centre, the shoulder depths (BlazePose z) or a single ear.
    // Single samples are noisy, left/right labels can swap when the child faces away, and the body can be
    // missed for a moment, so a facing only counts after holding briefly and turns are built from the order of
    // settled facings. When a side is skipped (fast turn, missed frames) the cues seen in between decide the
    // way round: front -> left -> back -> right -> front is a full turn, front -> left -> back -> left -> front
    // (half a turn and back) is not.
    public sealed class TurnDetector : IBasicMoveDetector
    {
        public enum Facing { Unknown, Front, Left, Back, Right }
        public float FrontWidth = .75f;   // shoulder width (fraction of standing) at or above: front or back
        public float SideWidth = .55f;    // at or below: side
        public float FaceSeen = .5f;      // nose or eye visibility for "face in view"
        public float NoseShift = .12f;    // nose off the shoulder centre (torso lengths): turned that way
        public float DepthShift = .1f;    // shoulder depth difference (torso lengths, BlazePose): turned that way
        public float HoldTime = .3f;      // a facing counts after holding this long (seconds)
        public float GapTolerance = .8f;  // seconds without a body before Tracking is false
        public float LostReset = 4f;      // after this long the facing is unknown again
        public const int BaselineSamples = 6;

        public string Move => "turn";
        public bool HasBaseline => baseSamples >= BaselineSamples;
        public bool Tracking { get; private set; }
        public Facing Settled { get; private set; }
        // Quarter turns since calibration (front = 0), unwrapped: a full left turn ends at -4.
        public int Quarter { get; private set; }
        public int FullTurns { get; private set; }
        public int Count { get; private set; }
        public int Incomplete { get; private set; }
        public int Direction { get; private set; }
        public float Confidence { get; private set; }
        public MoveRecord Last { get; private set; }
        // Latest sample: raw facing, estimated angle (-180..180, 0 = facing the camera), width ratio, face.
        public Facing Raw { get; private set; }
        public float Yaw { get; private set; }
        public float Ratio { get; private set; }
        public bool Face { get; private set; }
        public int Sign { get; private set; }
        public bool Ambiguous { get; private set; }
        public float Amount => Mathf.Abs(Yaw);
        public float Peak { get; private set; }
        public float Progress => Raw == Settled || Settled == Facing.Unknown ? 0 : Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(Yaw, SettledYaw)) / 90);
        public float SettledYaw => Settled == Facing.Left ? -90 : Settled == Facing.Right ? 90 : Settled == Facing.Back ? 180 : 0;

        int baseSamples; float sumRatio, sumTorso, baseRatio, baseTorso;
        float lastGood = float.NegativeInfinity, candidateSince, leftAt;
        Facing candidate; int candidateSamples, frontQuarter;
        bool seenLeft, seenRight, seenFront, seenBack, away;
        float lowest = 1;

        public void Reset()
        {
            ClearBaseline(); Count = Incomplete = FullTurns = 0; Last = default; Confidence = 0; Direction = 0;
        }
        public void ClearBaseline()
        {
            baseSamples = 0; sumRatio = sumTorso = baseRatio = baseTorso = 0; Tracking = false;
            Settled = Facing.Unknown; Quarter = frontQuarter = 0; candidate = Facing.Unknown; Raw = Facing.Unknown;
            lastGood = float.NegativeInfinity; Forget();
        }

        // Standing facing the camera: the normal shoulder width against the torso.
        public void Calibrate(PoseResult pose, float time)
        {
            if (pose == null || !pose.found || !Shoulders(pose, out var left, out var right) || !Hips(pose, out var hip)) return;
            float torso = Mathf.Abs((left.y + right.y) * .5f - hip.y);
            if (torso < .02f) return;
            baseSamples++; sumRatio += Width(pose, left, right) / torso; sumTorso += torso;
            baseRatio = sumRatio / baseSamples; baseTorso = sumTorso / baseSamples; lastGood = time;
            if (HasBaseline && Settled == Facing.Unknown) { Settled = Facing.Front; Quarter = frontQuarter = 0; Tracking = true; }
        }

        public void Wait(float time) => Gap(time);

        void Gap(float time)
        {
            if (!HasBaseline) return;
            // The face-based person finder tends to lose a child who faces away: a gap after a side view
            // hints at the back.
            if (Settled != Facing.Front) seenBack = true;
            if (time - lastGood > GapTolerance) Tracking = false;
            if (time - lastGood > LostReset) { Settled = Facing.Unknown; candidate = Facing.Unknown; Forget(); }
        }

        public void Step(PoseResult pose, float time)
        {
            if (!HasBaseline) { Calibrate(pose, time); return; }
            if (!Read(pose, out var raw, out float confidence)) { Confidence *= .7f; Gap(time); return; }
            lastGood = time; Tracking = true; Raw = raw;
            Confidence = Mathf.Lerp(Confidence, confidence, .5f);
            // Cues for the way round, kept until the next settled facing.
            if (Sign < 0) seenLeft = true; else if (Sign > 0) seenRight = true;
            if (Face && Ratio >= FrontWidth) seenFront = true;
            if (!Face && Ratio > SideWidth) seenBack = true;

            if (raw != Settled && raw != Facing.Unknown && Settled != Facing.Unknown && !away) { away = true; leftAt = time; lowest = confidence; }
            if (away) { lowest = Mathf.Min(lowest, confidence); Peak = Mathf.Max(Peak, Mathf.Abs(Mathf.DeltaAngle(Yaw, SettledYaw))); }
            if (raw == Facing.Unknown) { candidate = Facing.Unknown; return; }
            if (raw != candidate) { candidate = raw; candidateSince = time; candidateSamples = 1; return; }
            candidateSamples++;
            if (time - candidateSince < HoldTime || candidateSamples < 2) return;
            if (raw == Settled)
            {
                // Back where it started without settling elsewhere: a turn that was not finished.
                if (away && Peak >= 45) Incomplete++;
                away = false; Peak = 0; Forget();
                return;
            }
            Settle(raw, time);
        }

        void Settle(Facing facing, float time)
        {
            int target = Residue(facing), d = ((target - Quarter) % 4 + 4) % 4;
            if (d == 3) d = -1;
            Ambiguous = false;
            if (Settled == Facing.Unknown)
            {
                // Found again after a long loss: no turn is counted.
                Quarter += d; Settled = facing; if (facing == Facing.Front) frontQuarter = Quarter;
                Forget(); away = false; return;
            }
            if (d == 2)
            {
                // Opposite facing: the cues seen in between say which way round.
                bool viaPlus = Seen(Quarter + 1), viaMinus = Seen(Quarter - 1);
                if (viaPlus != viaMinus) d = viaPlus ? 2 : -2;
                else { d = Direction != 0 ? 2 * Direction : -2; Ambiguous = true; }
            }
            Quarter += d; Settled = facing; Direction = d > 0 ? 1 : -1; Count++;
            float trust = lowest * (Ambiguous ? .5f : 1);
            Last = new MoveRecord { move = Move, start = away ? leftAt : time, end = time, peak = Mathf.Abs(d) * 90, confidence = trust, direction = Direction };
            if (facing == Facing.Front)
            {
                int turned = Quarter - frontQuarter;
                if (Mathf.Abs(turned) >= 4)
                {
                    FullTurns++;
                    Last = new MoveRecord { move = "full turn", start = Last.start, end = time, peak = Mathf.Abs(turned) * 90, confidence = trust, direction = turned > 0 ? 1 : -1 };
                }
                frontQuarter = Quarter;
            }
            away = false; Peak = 0; Forget();
        }

        bool Seen(int quarter)
        {
            switch (((quarter % 4) + 4) % 4)
            {
                case 0: return seenFront;
                case 1: return seenRight;
                case 2: return seenBack;
                default: return seenLeft;
            }
        }
        static int Residue(Facing f) => f == Facing.Right ? 1 : f == Facing.Back ? 2 : f == Facing.Left ? 3 : 0;
        void Forget() { seenLeft = seenRight = seenFront = seenBack = false; }

        // Raw facing of one sample. False when the shoulders are not seen.
        bool Read(PoseResult p, out Facing raw, out float confidence)
        {
            raw = Facing.Unknown; confidence = 0;
            if (p == null || !p.found) return false;
            bool ls = p.Visible(PoseJoint.LeftShoulder), rs = p.Visible(PoseJoint.RightShoulder);
            if (!ls && !rs) return false;
            Vector2 left = p[PoseJoint.LeftShoulder], right = p[PoseJoint.RightShoulder], mid = ls && rs ? (left + right) * .5f : ls ? left : right;
            float torso = Hips(p, out var hip) ? Mathf.Abs(mid.y - hip.y) : baseTorso;
            if (torso < .02f) torso = baseTorso;
            // One shoulder hidden behind the body: a side view.
            Ratio = ls && rs ? Width(p, left, right) / torso / baseRatio : .3f;
            float faceSeen = Mathf.Max(p.Visibility(PoseJoint.Nose), Mathf.Max(p.Visibility(PoseJoint.LeftEye), p.Visibility(PoseJoint.RightEye)));
            Face = faceSeen >= FaceSeen;
            // Which way: the nose (front half), the shoulder depths, then a single ear.
            int nose = 0, depth = 0, ear = 0;
            if (p.Visible(PoseJoint.Nose, FaceSeen))
            {
                float shift = (p[PoseJoint.Nose].x - mid.x) * p.aspect / torso;
                nose = shift <= -NoseShift ? -1 : shift >= NoseShift ? 1 : 0;
            }
            if (p.hasDepth && ls && rs)
            {
                // The right shoulder nearer the camera (smaller depth) means turned to the left.
                float dz = (p.depth[(int)PoseJoint.RightShoulder] - p.depth[(int)PoseJoint.LeftShoulder]) * p.aspect / torso;
                depth = dz <= -DepthShift ? -1 : dz >= DepthShift ? 1 : 0;
            }
            bool leftEar = p.Visible(PoseJoint.LeftEar), rightEar = p.Visible(PoseJoint.RightEar);
            if (leftEar != rightEar) ear = rightEar ? -1 : 1;
            Sign = Face ? (nose != 0 ? nose : depth != 0 ? depth : ear) : depth;
            if (Ratio >= FrontWidth) raw = Face ? Facing.Front : Facing.Back;
            else if (Ratio <= SideWidth) raw = Sign < 0 ? Facing.Left : Sign > 0 ? Facing.Right : Facing.Unknown;
            // Estimated angle, for display and research data.
            float off = Mathf.Acos(Mathf.Clamp01(Ratio)) * Mathf.Rad2Deg;
            int sign = Sign != 0 ? Sign : Settled == Facing.Left ? -1 : Settled == Facing.Right ? 1 : 0;
            Yaw = (Face ? off : 180 - off) * (sign == 0 ? 1 : sign);
            if (!Face && sign == 0 && Ratio >= FrontWidth) Yaw = 180;
            confidence = Mathf.Min(ls ? p.Visibility(PoseJoint.LeftShoulder) : 1, rs ? p.Visibility(PoseJoint.RightShoulder) : 1) * (ls && rs ? 1 : .6f);
            return true;
        }

        static bool Shoulders(PoseResult p, out Vector2 left, out Vector2 right)
        {
            left = p[PoseJoint.LeftShoulder]; right = p[PoseJoint.RightShoulder];
            return p.Visible(PoseJoint.LeftShoulder) && p.Visible(PoseJoint.RightShoulder);
        }
        static bool Hips(PoseResult p, out Vector2 hip)
        {
            bool l = p.Visible(PoseJoint.LeftHip), r = p.Visible(PoseJoint.RightHip);
            hip = l && r ? (p[PoseJoint.LeftHip] + p[PoseJoint.RightHip]) * .5f : l ? p[PoseJoint.LeftHip] : p[PoseJoint.RightHip];
            return l || r;
        }
        // Shoulder width in real proportions (height units).
        static float Width(PoseResult p, Vector2 left, Vector2 right) { var d = right - left; d.x *= p.aspect; return d.magnitude; }
    }
}
