using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace FamilyAdventure.Presentation
{
    public enum JumpDuckState { Standing, Airborne, Landing, Ducking, Recovered }

    // Lower-body kinematics for Jump and Duck (docs/motion GDD 04, sections 4-6). Heights are normalized
    // screen v (up). Rise is the hip height above the calibrated standing line in units of the standing
    // hip-to-ankle length, so children of any height and distance give the same numbers.
    public sealed class JumpDuckBody
    {
        // State machine thresholds: Rise in hip-to-ankle lengths, angles in degrees.
        public const float JumpRise = .10f, AnkleLift = .05f, LandRise = .04f;
        public const float DuckDrop = -.15f, DuckKnee = 135, RecoverDrop = -.08f, RecoverKnee = 155;
        // A pre-jump dip lasts under .25 s: a squat becomes a duck only after .3 s, and a fast lift
        // (hip lengths per second) cancels it.
        public const float DuckHold = .3f, DipLift = 1.5f;
        // Scoring is a little kinder than the state machine (GDD 8.1).
        public const float ScoreRise = .10f, ScoreDrop = -.12f;
        // The filters work in reference screen pixels and degrees: the GDD's beta (.007) is tuned for pixels.
        const float Pixels = 900;
        public float Baseline { get; private set; }
        public float Floor { get; private set; }
        public float Length { get; private set; }
        public bool Calibrated => Length > 0;
        public float Rise { get; private set; }
        public float Knee { get; private set; } = 180;
        public float Lift { get; private set; }
        public JumpDuckState State { get; private set; }
        public bool Up => State == JumpDuckState.Airborne || Rise > ScoreRise;
        public bool Down => State == JumpDuckState.Ducking || Rise < ScoreDrop;
        readonly OneEuroFilter hip = new OneEuroFilter(), knee = new OneEuroFilter(), ankle = new OneEuroFilter();
        float squat, lastTime = -1, sumHip, sumAnkle;
        public int CalibrationSamples { get; private set; }

        public void Clear() { Length = 0; ResetCalibration(); Restart(); }
        public void ResetCalibration() { sumHip = sumAnkle = 0; CalibrationSamples = 0; }
        public void AddCalibration(float hipY, float ankleY) { sumHip += hipY; sumAnkle += ankleY; CalibrationSamples++; }
        // Averages the standing samples into Y_baseline, Y_floor and L_ref; false when there were none.
        public bool FinishCalibration()
        {
            if (CalibrationSamples == 0) return false;
            Calibrate(sumHip / CalibrationSamples, sumAnkle / CalibrationSamples); ResetCalibration(); return true;
        }
        public void Calibrate(float hipY, float ankleY)
        {
            Baseline = hipY; Floor = ankleY; Length = Mathf.Max(.05f, hipY - ankleY); Restart();
        }
        void Restart()
        {
            hip.Reset(); knee.Reset(); ankle.Reset();
            Rise = 0; Knee = 180; Lift = 0; State = JumpDuckState.Standing; squat = 0; lastTime = -1;
        }
        // One body sample taken at `time` (seconds). NaN knee angle or ankle = not seen; the rule that needs
        // it is then left out, so a child whose feet are out of view can still play.
        public JumpDuckState Step(float hipY, float kneeAngle, float ankleY, float time)
        {
            if (!Calibrated || (lastTime >= 0 && time <= lastTime)) return State;
            float dt = lastTime < 0 ? 0 : time - lastTime; lastTime = time;
            Rise = (hip.Filter(hipY * Pixels, time) / Pixels - Baseline) / Length;
            float speed = hip.Derivative / Pixels / Length;
            bool kneeSeen = !float.IsNaN(kneeAngle), ankleSeen = !float.IsNaN(ankleY);
            if (kneeSeen) Knee = knee.Filter(kneeAngle, time);
            if (ankleSeen) Lift = (ankle.Filter(ankleY * Pixels, time) / Pixels - Floor) / Length;
            switch (State)
            {
                case JumpDuckState.Airborne:
                    if (Rise < LandRise) State = JumpDuckState.Landing;
                    break;
                case JumpDuckState.Ducking:
                    if (Rise > RecoverDrop && (!kneeSeen || Knee > RecoverKnee)) State = JumpDuckState.Recovered;
                    break;
                default:
                    // Landing and Recovered last one sample, then the child is standing again.
                    State = JumpDuckState.Standing;
                    if (Rise > JumpRise && (!ankleSeen || Lift > AnkleLift)) { State = JumpDuckState.Airborne; squat = 0; }
                    else if (Rise < DuckDrop && (!kneeSeen || Knee < DuckKnee))
                    {
                        squat = speed > DipLift ? 0 : squat + dt;
                        if (squat >= DuckHold) State = JumpDuckState.Ducking;
                    }
                    else squat = 0;
                    break;
            }
            return State;
        }
        // Knee flexion from three joints: 180 = straight leg.
        public static float KneeAngle(Vector2 hip, Vector2 knee, Vector2 ankle) => Vector2.Angle(hip - knee, ankle - knee);
    }

    // 4. Jump and Duck: logs roll in low (jump over them) and rocks swing in on a vine at head height (duck
    // under them). The pose model's hips, knees and ankles drive JumpDuckBody; without a pose the camera
    // motion above or around the head stands in, and keys or clicks simulate the moves (testing mode).
    public sealed class JumpDuckGame : MotionGame
    {
        protected override string Instruction => "Lompat untuk rintangan bawah, merunduk untuk rintangan atas!";
        protected override bool HandsAndFeetOnly => false;
        public const float Travel = 2.4f, HorizonY = .65f, LowY = .14f, HighY = .75f, StartSize = .05f, EndSize = .2f;
        // The obstacle is judged from a little before it arrives to a little after, so a child who moves a
        // moment early or late still clears it.
        public const float Early = .35f, Late = .25f;
        public const float FirstInterval = 2.5f, LastInterval = 1.8f, SimulatedJump = .4f;
        public JumpDuckBody Body { get; } = new JumpDuckBody();
        public int Cleared { get; private set; }
        public int Misses { get; private set; }
        sealed class Obstacle { public Image image; public RectTransform vine; public bool high, warned; public float age; }
        readonly List<Obstacle> obstacles = new List<Obstacle>();
        Image floorPad, skyPad, marker;
        float wait, elapsed, jumpTime, duckTime, lastPose, headMotion, motionRest, lastCalibration;
        int wave, run;
        bool lastHigh, poseCalibrated;

        protected override void Setup()
        {
            obstacles.Clear(); Body.Clear(); poseCalibrated = false;
            Cleared = Misses = 0; wave = run = 0; wait = .8f; elapsed = 0; jumpTime = duckTime = 0;
            lastPose = -1; headMotion = motionRest = 0; lastCalibration = 0;
            // Warning pads: the floor flashes red for a log, the sky over the head flashes yellow for a rock.
            floorPad = Pad("Floor pad", .3f, .02f, .7f, .27f);
            skyPad = Pad("Sky pad", .3f, .63f, .7f, .88f);
            marker = Sprite("Player", "star", new Vector2(.5f, .45f), .07f); marker.color = new Color(.85f, 1, .9f, .9f);
        }
        Image Pad(string name, float x0, float y0, float x1, float y1)
        {
            var pad = UiKit.Panel(name, area, Color.clear); var image = pad.GetComponent<Image>(); image.raycastTarget = false;
            pad.anchorMin = new Vector2(x0, y0); pad.anchorMax = new Vector2(x1, y1); pad.offsetMin = pad.offsetMax = Vector2.zero;
            return image;
        }

        // Baseline: average standing hips and ankles while the child keeps still in the guide.
        protected override void Calibrating()
        {
            if (Calibration < lastCalibration) Body.ResetCalibration();
            lastCalibration = Calibration;
            if (NewPose() && Legs(out float hip, out _, out float ankle)) Body.AddCalibration(hip, Standing(hip, ankle));
        }
        // Ankles out of view: assume the usual hip-to-ankle length of a child standing in the guide.
        public const float TypicalLeg = .32f;
        static float Standing(float hip, float ankle) => float.IsNaN(ankle) ? hip - TypicalLeg : ankle;
        protected override void Calibrated() { poseCalibrated = Body.FinishCalibration(); }

        // Keys and clicks (GDD 9.2): up/space = jump, down/ctrl held = duck; click high = duck, low = jump.
        public void SimulateJump() { jumpTime = SimulatedJump; duckTime = 0; }
        public void SimulateDuck(float seconds = .9f) { duckTime = Mathf.Max(duckTime, seconds); jumpTime = 0; }

        protected override void Tick(float dt)
        {
            elapsed += dt;
            ReadInput(dt);
            Sense();
            jumpTime = Mathf.Max(0, jumpTime - dt); duckTime = Mathf.Max(0, duckTime - dt);
            Indicators();
            if ((wait -= dt) <= 0) Spawn();
            for (int i = obstacles.Count - 1; i >= 0; i--) if (Move(obstacles[i], dt)) { Remove(obstacles[i]); obstacles.RemoveAt(i); }
        }

        void ReadInput(float dt)
        {
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.upArrowKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame) SimulateJump();
                if (keys.downArrowKey.isPressed || keys.leftCtrlKey.isPressed || keys.rightCtrlKey.isPressed) SimulateDuck(.1f);
            }
            Vector2? press = null;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) press = touch.primaryTouch.position.ReadValue();
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) press = Mouse.current.position.ReadValue();
            if (press.HasValue && Screen.height > 0) { if (press.Value.y / Screen.height > .5f) SimulateDuck(); else SimulateJump(); }
            // Camera without a pose: motion above the head is a jump; motion only around the head (it is
            // moving down) for a moment is a duck. A rough stand-in, kept quiet for a moment after each move.
            if (Sensor.HasCamera && !Sensor.PoseActive)
            {
                var z = BodyZone;
                float above = Sensor.Total(new Rect(z.xMin - .04f, z.yMax, z.width + .08f, 1 - z.yMax));
                float head = Sensor.Total(new Rect(z.xMin, z.yMax - .18f, z.width, .18f));
                headMotion = head > 4 && above < .5f ? headMotion + dt : 0;
                if ((motionRest -= dt) <= 0)
                {
                    if (above > 3) { SimulateJump(); motionRest = .6f; }
                    else if (headMotion > .15f) { SimulateDuck(); motionRest = 1; }
                }
            }
        }

        bool NewPose()
        {
            if (!Sensor.PoseActive || Sensor.PoseSeenAt == lastPose) return false;
            lastPose = Sensor.PoseSeenAt; return true;
        }
        // Hip centre, knee angle (average of the legs fully seen) and ankle height from the pose; NaN = not seen.
        bool Legs(out float hip, out float kneeAngle, out float ankle)
        {
            var p = Sensor.Pose; hip = kneeAngle = ankle = float.NaN;
            bool l = p.legSeen[PoseResult.LeftHip], r = p.legSeen[PoseResult.RightHip];
            if (!l && !r) return false;
            hip = l && r ? (p.legs[PoseResult.LeftHip].y + p.legs[PoseResult.RightHip].y) * .5f : p.legs[l ? PoseResult.LeftHip : PoseResult.RightHip].y;
            // Joint positions in camera pixels' proportions, so the angle is not stretched by the screen.
            float aspect = Sensor.Preview != null ? (float)Sensor.Preview.width / Sensor.Preview.height : 4 / 3f;
            Vector2 Joint(int i) => new Vector2(p.legs[i].x * aspect, p.legs[i].y);
            float angles = 0, ankles = 0; int legs = 0, feet = 0;
            for (int side = 0; side < 2; side++)
            {
                int h = PoseResult.LeftHip + side, k = PoseResult.LeftKnee + side, a = PoseResult.LeftAnkle + side;
                if (p.legSeen[a]) { ankles += p.legs[a].y; feet++; }
                if (p.legSeen[h] && p.legSeen[k] && p.legSeen[a]) { angles += JumpDuckBody.KneeAngle(Joint(h), Joint(k), Joint(a)); legs++; }
            }
            if (legs > 0) kneeAngle = angles / legs;
            if (feet > 0) ankle = ankles / feet;
            return true;
        }

        void Sense()
        {
            float now = Time.unscaledTime;
            bool simulated = jumpTime > 0 || duckTime > 0;
            if (!simulated && NewPose() && Legs(out float hip, out float kneeAngle, out float ankle))
            {
                // Pose found only after the calibration: take the first standing moments as the baseline.
                if (!poseCalibrated)
                {
                    Body.AddCalibration(hip, Standing(hip, ankle));
                    if (Body.CalibrationSamples >= 8) poseCalibrated = Body.FinishCalibration();
                    return;
                }
                Body.Step(hip, kneeAngle, ankle, Sensor.PoseSeenAt);
                return;
            }
            if (!simulated && Sensor.PoseActive) return; // waiting for the next pose frame
            // Simulated body (keys, clicks, camera motion, or standing still when nothing is seen).
            if (!Body.Calibrated) Body.Calibrate(.45f, .45f - TypicalLeg);
            float rise = 0, knee = 175, lift = 0;
            if (jumpTime > 0) { rise = lift = .3f * Mathf.Sin((1 - jumpTime / SimulatedJump) * Mathf.PI); }
            else if (duckTime > 0) { rise = -.3f; knee = 100; }
            Body.Step(Body.Baseline + rise * Body.Length, knee, Body.Floor + lift * Body.Length, now);
        }

        void Indicators()
        {
            float blink = .5f + .5f * Mathf.Sin(Time.unscaledTime * 10);
            bool low = obstacles.Exists(o => !o.high && o.age > Travel * .4f), high = obstacles.Exists(o => o.high && o.age > Travel * .4f);
            floorPad.color = low ? new Color(1, .25f, .2f, .18f + .17f * blink) : new Color(.4f, 1, .5f, .08f);
            skyPad.color = high ? new Color(1, .85f, .2f, .18f + .17f * blink) : new Color(.6f, .8f, 1, .05f);
            // The star on the body follows the hip: up when jumping, down when ducking.
            Place(marker.rectTransform, new Vector2(.5f, .45f + Mathf.Clamp(Body.Rise, -.5f, .5f) * .4f), .07f);
            marker.color = Body.Up ? new Color(1, .9f, .35f) : Body.Down ? new Color(.5f, 1, .6f) : new Color(.85f, 1, .9f, .9f);
        }

        void Spawn()
        {
            // Waves 1-3 teach: low, high, low. Then random, never three of a kind in a row.
            bool high = wave < 3 ? wave == 1 : Random.value < .5f;
            if (wave >= 3 && run >= 2 && high == lastHigh) high = !high;
            run = high == lastHigh && wave > 0 ? run + 1 : 1; lastHigh = high;
            var o = new Obstacle { high = high };
            if (high)
            {
                o.vine = UiKit.Panel("Vine", area, new Color(.36f, .25f, .13f, .95f));
                o.vine.GetComponent<Image>().raycastTarget = false;
            }
            o.image = Sprite(high ? "High obstacle" : "Low obstacle", high ? "rock-high" : "log", new Vector2(.5f, HorizonY), StartSize);
            // Farther obstacles are drawn behind nearer ones (after the two pads), the vine behind its rock.
            o.image.transform.SetSiblingIndex(2); if (o.vine != null) o.vine.SetSiblingIndex(2);
            obstacles.Add(o);
            wave++; wait = Mathf.Lerp(FirstInterval, LastInterval, elapsed / PlayTime);
        }

        // Moves one obstacle toward the child and judges it on arrival; true when it is done.
        bool Move(Obstacle o, float dt)
        {
            o.age += dt;
            float t = o.age / Travel, y = Mathf.Lerp(HorizonY, o.high ? HighY : LowY, t), size = Mathf.LerpUnclamped(StartSize, EndSize, t);
            var point = new Vector2(.5f, y);
            if (o.high)
            {
                // Hangs from a vine and swings a little.
                Place(o.image.rectTransform, point, size);
                o.image.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(o.age * 4) * 8);
                o.vine.anchorMin = new Vector2(.5f, y + size * .35f); o.vine.anchorMax = new Vector2(.5f, 1);
                o.vine.sizeDelta = new Vector2(Mathf.Lerp(3, 9, t), 0); o.vine.anchoredPosition = Vector2.zero;
            }
            else
            {
                // Rolls along the path with a small bump.
                Place(o.image.rectTransform, point + new Vector2(0, Mathf.Abs(Mathf.Sin(o.age * 6)) * .01f), size);
            }
            if (!o.warned && t > .5f) { o.warned = true; Cue(o.high ? "Merunduk!" : "Lompat!", 1); }
            if (o.age < Travel - Early) return false;
            if (o.high ? Body.Down : Body.Up)
            {
                Score++; Cleared++;
                Burst(point, o.high ? new Color(.6f, 1, .6f) : new Color(1, .9f, .4f)); Cue(o.high ? "Keren!" : "Hebat!", .8f);
                return true;
            }
            if (o.age < Travel + Late) return false;
            Misses++; Cue(o.high ? "Awas kepalanya!" : "Lompat lebih tinggi!", .9f);
            return true;
        }
        static void Remove(Obstacle o)
        {
            if (o.image != null) Destroy(o.image.gameObject);
            if (o.vine != null) Destroy(o.vine.gameObject);
        }

        // For tests: the next obstacle to arrive (-1 none, 0 low, 1 high) and seconds until it arrives.
        public int NextObstacle => obstacles.Count > 0 ? (obstacles[0].high ? 1 : 0) : -1;
        public float NextArrival => obstacles.Count > 0 ? Travel - obstacles[0].age : -1;
    }
}
