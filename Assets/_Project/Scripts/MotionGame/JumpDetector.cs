using UnityEngine;
namespace FamilyAdventure.Presentation
{
    // Jump (lompat): both feet leave the floor together and land again shortly after. Heights are fractions
    // of the standing leg length (hips to the lower ankle), so near and far children are treated the same.
    // Telling a small jump from standing on tiptoe: on tiptoe the toes stay on the floor (BlazePose toe points)
    // and the body rises slowly; a jump rises fast. MoveNet has no toes, so it relies on speed alone.
    // At about 12 pose samples a second a small jump (0.2-0.3 s in the air) gives only 2-3 samples, so a
    // single airborne sample is enough, and samples missed in the air (head out of the picture) are allowed.
    public sealed class JumpDetector : IBasicMoveDetector
    {
        public enum Phase { Waiting, Ground, Air, Lost }
        // Fractions of leg length.
        public float FeetLift = .06f;     // both ankles at least this far above the standing floor
        public float HipLift = .05f;      // hips at least this far above standing height
        public float ToeLift = .03f;      // toes too, when the model has them (tiptoe keeps them down)
        public float FeetApart = .2f;     // the two ankles may differ this much (two feet, not one)
        public float LandLift = .03f;     // ankles back below this = landed
        // Seconds.
        public float RiseSpeed = .6f;     // leg lengths per second from the floor to up in the air (tiptoe is slower)
        public float MaxAir = .7f;        // longer is not a jump (stepping onto something, a held tiptoe)
        public float GapTolerance = .6f;
        public float LegFromTorso = 1.3f;
        public const int BaselineSamples = 6;

        public string Move => "jump";
        public Phase State { get; private set; }
        public bool HasBaseline => baseSamples >= BaselineSamples;
        public bool Tracking => State == Phase.Ground || State == Phase.Air;
        public int Direction { get; private set; }
        public float Height { get; private set; }          // hips above standing height now (fraction of leg)
        public float Amount => Mathf.Max(0, Height);
        public float Peak { get; private set; }
        public int Count { get; private set; }
        public int Incomplete { get; private set; }
        public float Confidence { get; private set; }
        public float Progress => State == Phase.Air ? .5f : 0;
        public MoveRecord Last { get; private set; }
        public string Reason { get; private set; } = "";
        public string LastMiss { get; private set; } = "";
        public bool FeetSeen { get; private set; }
        public bool ToesSeen { get; private set; }
        public float LegLength => baseLeg;
        public float FloorHeight => baseFloor;
        public float FeetHeight { get; private set; }      // lower ankle above the floor (fraction of leg)

        int baseSamples, floorSamples, toeSamples, shoulderSamples;
        float sumHip, sumFloor, sumToe, sumShoulder;
        float baseHip, baseFloor, baseToe, baseLeg;
        bool floorFromFeet;
        float lastGood = float.NegativeInfinity, lastHeight, takeoff, lowest, onFloor, floorHeight;
        bool missedInAir;

        struct Sample { public float hip, ankleLow, ankleHigh, toeLow, shoulder, confidence; public bool feet, toes, shoulders; }

        public void Reset()
        {
            ClearBaseline(); Count = Incomplete = 0; Last = default; Confidence = 0; Reason = LastMiss = "";
        }
        public void ClearBaseline()
        {
            baseSamples = floorSamples = toeSamples = shoulderSamples = 0; sumHip = sumFloor = sumToe = sumShoulder = 0;
            baseHip = baseFloor = baseToe = baseLeg = 0; floorFromFeet = false;
            State = Phase.Waiting; Height = Peak = 0; Direction = 0; lastGood = float.NegativeInfinity;
        }

        public void Calibrate(PoseResult pose, float time)
        {
            if (!Read(pose, out var s) || !(s.feet || s.shoulders)) return;
            lastGood = time; baseSamples++; sumHip += s.hip;
            if (s.feet) { floorSamples++; sumFloor += s.ankleLow; }
            if (s.toes) { toeSamples++; sumToe += s.toeLow; }
            if (s.shoulders) { shoulderSamples++; sumShoulder += s.shoulder; }
            baseHip = sumHip / baseSamples;
            floorFromFeet = floorSamples * 2 > baseSamples;
            if (floorFromFeet) { baseFloor = sumFloor / floorSamples; baseLeg = baseHip - baseFloor; }
            else { baseLeg = LegFromTorso * (sumShoulder / shoulderSamples - baseHip); baseFloor = baseHip - baseLeg; }
            baseToe = toeSamples > 0 ? sumToe / toeSamples : baseFloor;
            if (baseLeg < .05f) { ClearBaseline(); return; }
            if (HasBaseline && State == Phase.Waiting) { State = Phase.Ground; lastHeight = 0; onFloor = time; }
        }

        public void Wait(float time)
        {
            if (State == Phase.Air && time - takeoff > MaxAir + GapTolerance) { Miss("terlalu lama"); State = Phase.Lost; }
            else if (State == Phase.Ground && time - lastGood > GapTolerance) State = Phase.Lost;
        }

        public void Step(PoseResult pose, float time)
        {
            if (!Read(pose, out var s))
            {
                // In the air the head may leave the picture: keep waiting for the landing.
                Confidence *= .7f; Direction = 0;
                if (State == Phase.Air) missedInAir = true;
                Wait(time);
                return;
            }
            if (!HasBaseline) { Calibrate(pose, time); Confidence = s.confidence; return; }
            FeetSeen = s.feet && floorFromFeet; ToesSeen = s.toes && toeSamples > 0;
            float dt = Mathf.Max(.001f, time - lastGood);
            float height = (s.hip - baseHip) / baseLeg;
            float speed = (height - lastHeight) / dt;
            Direction = speed > .5f ? 1 : speed < -.5f ? -1 : 0;
            Height = height; lastHeight = height; lastGood = time;
            Confidence = Mathf.Lerp(Confidence, s.confidence, .5f);
            if (State == Phase.Lost) { State = Phase.Ground; onFloor = float.NegativeInfinity; }

            float lowFoot = FeetSeen ? (s.ankleLow - baseFloor) / baseLeg : height;
            float highFoot = FeetSeen ? (s.ankleHigh - baseFloor) / baseLeg : height;
            float toes = ToesSeen ? (s.toeLow - baseToe) / baseLeg : lowFoot;
            FeetHeight = lowFoot;
            bool twoFeet = !FeetSeen || highFoot - lowFoot <= FeetApart;
            // Without feet in view only a clearly higher hip counts.
            bool up = height >= (FeetSeen ? HipLift : HipLift * 2) && lowFoot >= FeetLift && toes >= ToeLift && twoFeet;

            if (State == Phase.Ground)
            {
                Reason = "";
                if (height < HipLift * .5f && lowFoot < FeetLift * .5f) { onFloor = time; floorHeight = height; }
                if (up && (height - floorHeight) / Mathf.Max(.001f, time - onFloor) >= RiseSpeed) { State = Phase.Air; takeoff = time; Peak = height; lowest = s.confidence; missedInAir = false; }
                else if (up) Reason = "jinjit";
                else if (FeetSeen && highFoot - lowFoot > FeetApart) Reason = "satu kaki";
                else if (ToesSeen && lowFoot >= FeetLift && toes < ToeLift) Reason = "jinjit";
                if (Reason == "" && Mathf.Abs(speed) < .3f && Mathf.Abs(height) < .05f && lowFoot < LandLift) Adapt(s);
                return;
            }
            // In the air.
            Peak = Mathf.Max(Peak, height); lowest = Mathf.Min(lowest, s.confidence);
            if (time - takeoff > MaxAir) { Miss("terlalu lama"); State = Phase.Ground; return; }
            if (FeetSeen ? lowFoot < LandLift : height < HipLift)
            {
                Count++;
                float trust = lowest * (missedInAir ? .7f : 1) * (ToesSeen ? 1 : .8f);
                Last = new MoveRecord { move = Move, start = takeoff, end = time, peak = Peak, confidence = trust, direction = 1 };
                State = Phase.Ground;
            }
        }

        void Miss(string why) { Incomplete++; LastMiss = why; }

        // Standing still: follow slow changes (the child steps a little nearer or further).
        void Adapt(Sample s)
        {
            const float rate = .1f;
            baseHip += (s.hip - baseHip) * rate;
            if (s.feet && floorFromFeet) baseFloor += (s.ankleLow - baseFloor) * rate;
            if (s.toes && toeSamples > 0) baseToe += (s.toeLow - baseToe) * rate;
            if (floorFromFeet) baseLeg = Mathf.Max(.05f, baseHip - baseFloor); else baseFloor = baseHip - baseLeg;
        }

        static bool Read(PoseResult p, out Sample s)
        {
            s = default;
            if (p == null || !p.found) return false;
            if (!Pair(p, PoseJoint.LeftHip, PoseJoint.RightHip, out float hipLow, out float hipHigh, out float hipSeen)) return false;
            s.hip = (hipLow + hipHigh) * .5f;
            // Both ankles are needed to see both feet leave the floor.
            s.feet = p.Visible(PoseJoint.LeftAnkle) && p.Visible(PoseJoint.RightAnkle);
            if (s.feet) Pair(p, PoseJoint.LeftAnkle, PoseJoint.RightAnkle, out s.ankleLow, out s.ankleHigh, out _);
            s.toes = p.Visible(PoseJoint.LeftToe) && p.Visible(PoseJoint.RightToe);
            if (s.toes) Pair(p, PoseJoint.LeftToe, PoseJoint.RightToe, out s.toeLow, out _, out _);
            s.shoulders = Pair(p, PoseJoint.LeftShoulder, PoseJoint.RightShoulder, out float sl, out float sh, out _);
            s.shoulder = (sl + sh) * .5f;
            s.confidence = hipSeen * (s.feet ? 1 : .5f);
            return true;
        }
        static bool Pair(PoseResult p, PoseJoint a, PoseJoint b, out float low, out float high, out float seen)
        {
            bool va = p.Visible(a), vb = p.Visible(b);
            float ya = p[a].y, yb = p[b].y;
            low = va && vb ? Mathf.Min(ya, yb) : va ? ya : yb;
            high = va && vb ? Mathf.Max(ya, yb) : va ? ya : yb;
            seen = va && vb ? (p.Visibility(a) + p.Visibility(b)) * .5f : va ? p.Visibility(a) : vb ? p.Visibility(b) : 0;
            return va || vb;
        }
    }
}
