using UnityEngine;
namespace FamilyAdventure.Presentation
{
    // Squat (jongkok): the hips go down toward the floor and come back up while both feet stay on the floor.
    // Depth is how far the hips dropped as a fraction of the standing hip-to-floor height (leg length), so a
    // child near the camera and one far away are treated the same. The floor is the lower ankle, or the
    // standing floor height when the feet are out of view. Only vertical distances are used: facing the
    // camera, the thighs point at it, so 2D knee angles stay nearly straight even in a deep squat.
    // Thresholds and limits per model: docs/49 DETEKTOR JONGKOK.md.
    public sealed class SquatDetector : IBasicMoveDetector
    {
        public enum Phase { Waiting, Up, Lowering, Low, Lost }

        // Fractions of leg length.
        public float TargetDepth = .25f;     // a squat counts once the hips reach this (a half squat)
        public float StandDepth = .10f;      // above this the child stands again; below it a squat starts
        public float AttemptDepth = .15f;    // shallower dips are not even counted as incomplete
        public float FloorTolerance = .15f;  // how far an ankle may rise off the standing floor
        public float KneeFloor = .5f;        // knees must keep this fraction of their standing height (not kneeling)
        public float SitDepth = .88f;        // hips this low are sitting on the floor
        public float LegFromTorso = 1.3f;    // leg length guess from the torso when the feet are not seen
        // Seconds.
        public float GapTolerance = .6f;     // missed samples shorter than this keep the squat going
        public float MinSquatTime = .6f;     // a faster dip and rise is a bounce or a jump take-off
        public const int BaselineSamples = 6;

        public string Move => "squat";
        public Phase State { get; private set; }
        public bool HasBaseline => baseSamples >= BaselineSamples;
        public bool Tracking => State != Phase.Waiting && State != Phase.Lost;
        public int Direction { get; private set; }
        public float Depth { get; private set; }
        public float Amount => Mathf.Clamp01(Depth);
        public float Peak { get; private set; }
        public int Count { get; private set; }
        public int Incomplete { get; private set; }
        public float Confidence { get; private set; }
        public MoveRecord Last { get; private set; }
        public float Progress
        {
            get
            {
                if (State == Phase.Lowering) return .5f * Mathf.Clamp01(Depth / TargetDepth);
                if (State == Phase.Low) return .5f + .5f * Mathf.Clamp01((Peak - Depth) / Mathf.Max(.01f, Peak - StandDepth));
                return 0;
            }
        }
        // Debug and drawing values (normalized screen v, up).
        public float LegLength => baseLeg;
        public float FloorHeight => baseFloor;
        public float StandingHipHeight => baseHip;
        public float TargetHipHeight => baseFloor + baseLeg * (1 - TargetDepth);
        public bool FeetSeen { get; private set; }
        public bool FloorFromFeet => floorFromFeet;
        public bool Bending { get; private set; }
        public string Reason { get; private set; } = "";
        // Why the latest incomplete squat did not count: kurang dalam, terlalu cepat, kaki terangkat,
        // lutut di lantai or duduk.
        public string LastMiss { get; private set; } = "";

        // Standing baseline: running means while calibrating, then slowly adapted while standing still.
        int baseSamples, floorSamples, kneeSamples, shoulderSamples;
        float sumHip, sumFloor, sumKnee, sumShoulder;
        float baseHip, baseFloor, baseLeg, baseKnee, baseShoulder;
        bool floorFromFeet;
        // Running state.
        readonly float[] recent = new float[3]; int recentCount;
        float lastGood = float.NegativeInfinity, lastTime, start, lowestConfidence;
        int badStreak; bool spoiled; string spoilReason = "";

        struct Sample { public float hip, floor, footTop, knee, shoulder, confidence; public bool feet, knees, shoulders; }

        public void Reset()
        {
            ClearBaseline();
            Count = Incomplete = 0; Last = default; Confidence = 0; Reason = LastMiss = "";
        }
        public void ClearBaseline()
        {
            baseSamples = floorSamples = kneeSamples = shoulderSamples = 0;
            sumHip = sumFloor = sumKnee = sumShoulder = 0;
            baseHip = baseFloor = baseLeg = baseKnee = baseShoulder = 0; floorFromFeet = false;
            State = Phase.Waiting; Depth = Peak = 0; Direction = 0; recentCount = 0; lastGood = float.NegativeInfinity;
        }

        // Adds a standing sample to the baseline (call while the child stands still).
        public void Calibrate(PoseResult pose, float time)
        {
            // The leg length needs the feet, or the shoulders to guess it from the torso.
            if (!Read(pose, out var s) || !(s.feet || s.shoulders)) return;
            lastGood = lastTime = time;
            baseSamples++; sumHip += s.hip;
            if (s.feet) { floorSamples++; sumFloor += s.floor; }
            if (s.feet && s.knees) { kneeSamples++; sumKnee += s.knee - s.floor; }
            if (s.shoulders) { shoulderSamples++; sumShoulder += s.shoulder; }
            baseHip = sumHip / baseSamples;
            baseShoulder = shoulderSamples > 0 ? sumShoulder / shoulderSamples : 0;
            baseKnee = kneeSamples > 0 ? sumKnee / kneeSamples : 0;
            // Feet seen in most samples: they are the floor. Otherwise guess the leg from the torso.
            floorFromFeet = floorSamples * 2 > baseSamples;
            if (floorFromFeet) { baseFloor = sumFloor / floorSamples; baseLeg = baseHip - baseFloor; }
            else { baseLeg = LegFromTorso * (baseShoulder - baseHip); baseFloor = baseHip - baseLeg; }
            if (baseLeg < .05f) { ClearBaseline(); return; }
            if (HasBaseline && State == Phase.Waiting) State = Phase.Up;
        }

        public void Wait(float time)
        {
            if (HasBaseline && State != Phase.Lost && time - lastGood > GapTolerance) Lose();
        }

        public void Step(PoseResult pose, float time)
        {
            if (!Read(pose, out var s))
            {
                // Missed frame: keep going for a short gap, give up after a long one.
                Confidence *= .7f; Direction = 0; lastTime = time;
                if (HasBaseline && State != Phase.Lost && time - lastGood > GapTolerance) Lose();
                return;
            }
            lastTime = time;
            // Without a calibration the first samples are taken as standing.
            if (!HasBaseline) { Calibrate(pose, time); Confidence = s.confidence; return; }
            FeetSeen = s.feet;

            float floor = floorFromFeet && s.feet ? s.floor : baseFloor;
            float raw = (baseLeg - (s.hip - floor)) / baseLeg;
            // Median of the last three samples removes single-frame jumps of a joint.
            recent[recentCount % 3] = raw; recentCount++;
            float depth = recentCount >= 3 ? Median(recent[0], recent[1], recent[2]) : raw;
            float dt = Mathf.Max(.001f, time - lastGood);
            float speed = (depth - Depth) / dt;
            Direction = speed > .25f ? 1 : speed < -.25f ? -1 : 0;
            bool wasLost = State == Phase.Lost;
            Depth = depth; lastGood = time;
            Confidence = Mathf.Lerp(Confidence, s.confidence, .5f);

            // Movements that look like a squat but are not one.
            Reason = "";
            if (s.feet && floorFromFeet && s.footTop - baseFloor > FloorTolerance * baseLeg) Reason = "kaki terangkat";
            else if (s.feet && s.knees && floorFromFeet && baseKnee > 0 && s.knee - s.floor < KneeFloor * baseKnee) Reason = "lutut di lantai";
            else if (depth > SitDepth) Reason = "duduk";
            float shoulderDrop = s.shoulders && baseShoulder > 0 ? (baseShoulder - s.shoulder) / baseLeg : 0;
            Bending = depth < StandDepth && shoulderDrop - Mathf.Max(0, depth) > .2f;
            if (Bending && Reason == "") Reason = "membungkuk";
            bool valid = Reason == "" || Reason == "membungkuk";
            if (!valid && (State == Phase.Lowering || State == Phase.Low)) spoilReason = Reason;

            if (wasLost)
            {
                // After a long gap a squat only starts again from standing.
                if (depth < StandDepth) State = Phase.Up;
                return;
            }
            switch (State)
            {
                case Phase.Up:
                    if (depth >= StandDepth && valid) { State = Phase.Lowering; start = time; Peak = depth; lowestConfidence = s.confidence; spoiled = false; badStreak = 0; spoilReason = ""; }
                    else if (Reason == "") Adapt(s, depth, speed);
                    break;
                case Phase.Lowering:
                case Phase.Low:
                    Peak = Mathf.Max(Peak, depth); lowestConfidence = Mathf.Min(lowestConfidence, s.confidence);
                    badStreak = valid ? 0 : badStreak + 1;
                    if (badStreak >= 2) spoiled = true;
                    if (State == Phase.Lowering && depth >= TargetDepth && valid && !spoiled) State = Phase.Low;
                    if (depth < StandDepth)
                    {
                        if (State == Phase.Low && !spoiled && time - start >= MinSquatTime)
                        {
                            Count++;
                            Last = new MoveRecord { move = Move, start = start, end = time, peak = Peak, confidence = lowestConfidence };
                        }
                        else if (Peak >= AttemptDepth)
                        {
                            Incomplete++;
                            LastMiss = spoiled ? spoilReason : State != Phase.Low ? "kurang dalam" : "terlalu cepat";
                        }
                        State = Phase.Up;
                    }
                    break;
            }
        }

        void Lose()
        {
            // A squat in progress is dropped, never counted.
            State = Phase.Lost; Direction = 0; recentCount = 0; Reason = "badan hilang";
        }

        // Standing still: follow slow changes (the child steps a little nearer or further).
        void Adapt(Sample s, float depth, float speed)
        {
            if (depth > .05f || Mathf.Abs(speed) > .15f) return;
            const float rate = .1f;
            baseHip += (s.hip - baseHip) * rate;
            if (s.feet && !floorFromFeet && Mathf.Abs(depth) < .1f) { floorFromFeet = true; baseFloor = s.floor; }
            else if (s.feet && floorFromFeet) baseFloor += (s.floor - baseFloor) * rate;
            if (s.feet && s.knees) baseKnee = baseKnee > 0 ? baseKnee + (s.knee - s.floor - baseKnee) * rate : s.knee - s.floor;
            if (s.shoulders) baseShoulder += (s.shoulder - baseShoulder) * rate;
            if (floorFromFeet) baseLeg = Mathf.Max(.05f, baseHip - baseFloor);
            else baseFloor = baseHip - baseLeg;
        }

        // Heights of the hips (needed), ankles, knees and shoulders. The floor is the lower ankle, so one
        // lifted foot does not move it; footTop is the higher ankle, to see a foot leave the floor.
        static bool Read(PoseResult p, out Sample s)
        {
            s = default;
            if (p == null || !p.found) return false;
            if (!Pair(p, PoseJoint.LeftHip, PoseJoint.RightHip, out float hipLow, out float hipHigh, out float hipSeen)) return false;
            s.hip = (hipLow + hipHigh) * .5f;
            s.feet = Pair(p, PoseJoint.LeftAnkle, PoseJoint.RightAnkle, out s.floor, out s.footTop, out _);
            s.knees = Pair(p, PoseJoint.LeftKnee, PoseJoint.RightKnee, out s.knee, out _, out _);
            s.shoulders = Pair(p, PoseJoint.LeftShoulder, PoseJoint.RightShoulder, out float shoulderLow, out float shoulderHigh, out _);
            s.shoulder = (shoulderLow + shoulderHigh) * .5f;
            s.confidence = hipSeen * (s.feet ? 1 : .6f) * (s.knees ? 1 : .85f);
            return true;
        }
        // Lower and higher v of a left/right pair (the same when only one is seen) and their visibility.
        static bool Pair(PoseResult p, PoseJoint a, PoseJoint b, out float low, out float high, out float seen)
        {
            bool va = p.Visible(a), vb = p.Visible(b);
            float ya = p[a].y, yb = p[b].y;
            low = va && vb ? Mathf.Min(ya, yb) : va ? ya : yb;
            high = va && vb ? Mathf.Max(ya, yb) : va ? ya : yb;
            seen = va && vb ? (p.Visibility(a) + p.Visibility(b)) * .5f : va ? p.Visibility(a) : vb ? p.Visibility(b) : 0;
            return va || vb;
        }
        static float Median(float a, float b, float c) => Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));
    }
}
