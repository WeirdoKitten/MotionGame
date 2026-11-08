using UnityEngine;
namespace FamilyAdventure.Presentation
{
    // One finished basic movement with its times, so later steps can chain movements into advanced ones.
    public struct MoveRecord
    {
        public string move;
        public float start, end;
        // Largest Amount reached, lowest sample confidence, and direction (-1, 0, +1) of the movement.
        public float peak, confidence;
        public int direction;
    }

    // Detector for one basic movement in a stream of pose samples. Games only read it; advanced movements
    // (fixed sequences from expert recordings) are meant to be built later by chaining several detectors.
    // Reach, trace and shift (the first three games) can follow the same shape; see docs/49.
    public interface IBasicMoveDetector
    {
        string Move { get; }
        // Standing baseline: samples while the child stands still facing the camera.
        bool HasBaseline { get; }
        void Calibrate(PoseResult pose, float time);
        void ClearBaseline();
        // One pose sample: call once per new model result. pose.found = false is a missed frame.
        void Step(PoseResult pose, float time);
        // Between samples, so a body that stops being seen is noticed even without new samples.
        void Wait(float time);
        void Reset();
        bool Tracking { get; }
        // -1, 0 or +1 along the movement's axis.
        int Direction { get; }
        // Size of the movement now (angle, height or depth) and the largest size in the current or last one.
        float Amount { get; }
        float Peak { get; }
        // Finished movements, and movements that were started but not finished.
        int Count { get; }
        int Incomplete { get; }
        // 0..1 through one movement, and 0..1 trust in the latest samples.
        float Progress { get; }
        float Confidence { get; }
        MoveRecord Last { get; }
    }

    // A game driven by one basic-movement detector. The standing baseline is taken while the child keeps
    // still in the guide and during the countdown; during play every new pose sample is fed exactly once.
    // Without a pose model (camera motion or pointer only) UsingPose is false and the game uses its own
    // rough fallback.
    public abstract class PoseMoveGame : MotionGame
    {
        protected abstract IBasicMoveDetector Detector { get; }
        public bool UsingPose => Sensor.Model.HasValue || Sensor.PoseInjected;
        int samples = -1;
        float lastCalibration;

        // Call from Setup.
        protected void StartDetector() { Detector.Reset(); samples = -1; lastCalibration = 0; }

        protected override void Standing()
        {
            if (!UsingPose) return;
            // Moving during calibration restarts the five still seconds, and the baseline with them.
            if (State == Phase.Calibrate && Calibration < lastCalibration) Detector.ClearBaseline();
            lastCalibration = State == Phase.Calibrate ? Calibration : 0;
            if (State == Phase.Calibrate && Calibration <= 0) return;
            if (Sensor.PoseSamples != samples) { samples = Sensor.PoseSamples; Detector.Calibrate(Sensor.Pose, Time.unscaledTime); }
        }

        // Feeds the newest pose sample to the detector, or tells it none came. True when a sample was taken.
        protected bool Feed()
        {
            float now = Time.unscaledTime;
            if (Sensor.PoseSamples == samples) { Detector.Wait(now); return false; }
            samples = Sensor.PoseSamples; Detector.Step(Sensor.Pose, now); return true;
        }

        // Header lines of the research overlay shared by all detectors.
        protected string DebugHeader(string name)
        {
            var d = Detector;
            string model = Sensor.Model.HasValue ? $"{Sensor.Model.Value} {Sensor.Pose.milliseconds:0} ms{(Sensor.Pose.tracked ? " (lacak)" : "")}" : "pose uji";
            return $"{name} | {model}\n" +
                $"arah {d.Direction:+0;-0;0}  besar {d.Amount:0.00}  puncak {d.Peak:0.00}\n" +
                $"progres {d.Progress:0.00}  kepercayaan {d.Confidence:0.00}\n" +
                $"jumlah {d.Count}  tidak lengkap {d.Incomplete}\n";
        }
    }
}
