using UnityEngine;
namespace FamilyAdventure.Presentation
{
    // One Euro Filter (Casiez, Roussel, Vogel 2012) for one real-time signal. The cutoff rises with the
    // signal's speed: still values are smoothed hard (no jitter), fast moves pass through with little lag.
    // Speed is in signal units per second, so beta depends on the units fed in.
    public sealed class OneEuroFilter
    {
        public float MinCutoff, Beta, DerivativeCutoff;
        float value, derivative, lastTime;
        bool started;
        public OneEuroFilter(float minCutoff = 1, float beta = .007f, float derivativeCutoff = 1)
        {
            MinCutoff = minCutoff; Beta = beta; DerivativeCutoff = derivativeCutoff;
        }
        public float Value => value;
        // Speed of the filtered signal (units per second).
        public float Derivative => derivative;
        public void Reset() { started = false; derivative = 0; }
        // Filters a sample taken at `time` (seconds). Samples at the same time return the last value.
        public float Filter(float sample, float time)
        {
            if (!started) { started = true; value = sample; derivative = 0; lastTime = time; return value; }
            float dt = time - lastTime;
            if (dt <= 0) return value;
            lastTime = time;
            float speed = (sample - value) / dt;
            derivative += Alpha(DerivativeCutoff, dt) * (speed - derivative);
            float cutoff = MinCutoff + Beta * Mathf.Abs(derivative);
            value += Alpha(cutoff, dt) * (sample - value);
            return value;
        }
        static float Alpha(float cutoff, float dt)
        {
            float tau = 1 / (2 * Mathf.PI * cutoff);
            return 1 / (1 + tau / dt);
        }
    }
}
