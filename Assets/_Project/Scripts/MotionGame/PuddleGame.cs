using UnityEngine;
using UnityEngine.UI;
namespace FamilyAdventure.Presentation
{
    // 6. Jump the Puddle (basic movement: jump). A puddle slides toward the child's feet; jumping while it is
    // at the feet splashes it for a star. Puddles come in sets of three with a rest between sets, so jumps are
    // never long in a row. JumpDetector does the counting; a small jump is enough.
    // Without a pose model, motion on the puddle while it is at the feet stands in for the jump.
    public sealed class PuddleGame : PoseMoveGame
    {
        public const int SetSize = 3;
        // Seconds: travel from the back of the path to the feet, the jump window around the arrival, the gap
        // between puddles and the rest after a set.
        public const float Travel = 2.2f, Early = .7f, Late = .5f, Between = 1.2f, SetRest = 5f;
        static readonly Color Water = new Color(.45f, .75f, 1f, .9f);
        protected override string Instruction => UsingPose ? "Lompat kecil saat genangan sampai di kakimu!" : "Sentuh genangan saat sampai di kakimu!";
        protected override string Safety => "Lompat kecil saja sudah cukup! Jauh dari meja dan kursi, lantai tidak licin, jangan pakai kaus kaki licin.";
        protected override bool HandsAndFeetOnly => false;
        public JumpDetector Jumps { get; } = new JumpDetector();
        protected override IBasicMoveDetector Detector => Jumps;
        public bool PuddleNear => puddle != null && age >= Travel - Early;
        public float NextArrival => puddle != null ? Travel - age : -1;
        public Vector2 Feet { get; private set; } = new Vector2(.5f, .12f);
        public bool Resting => rest > 0;
        public int Splashes => Score;
        public int Missed { get; private set; }
        Image puddle, feetMark;
        int jumps, inSet;
        float age, wait, rest, hintWait;

        protected override void Setup()
        {
            StartDetector(); jumps = 0; inSet = 0; age = 0; wait = 1; rest = 0; hintWait = 0; Missed = 0; puddle = null;
            feetMark = UiKit.Rect("Feet line", area).gameObject.AddComponent<Image>(); feetMark.color = new Color(1, 1, 1, .5f); feetMark.raycastTarget = false;
        }

        protected override void Tick(float dt)
        {
            hintWait -= dt;
            if (UsingPose)
            {
                Feed();
                if (Jumps.HasBaseline) Feet = new Vector2(Sensor.PoseActive ? Sensor.Pose.hips.x : Feet.x, Mathf.Clamp(Jumps.FloorHeight, .06f, .35f));
            }
            var r = feetMark.rectTransform; r.anchorMin = new Vector2(Feet.x - .12f, Feet.y); r.anchorMax = new Vector2(Feet.x + .12f, Feet.y);
            r.sizeDelta = new Vector2(0, 8); r.anchoredPosition = Vector2.zero;
            if (Resting)
            {
                if ((rest -= dt) <= 0) { inSet = 0; wait = .5f; Cue(Instruction, 2); }
                jumps = Jumps.Count; return;
            }
            if (puddle == null)
            {
                jumps = Jumps.Count;
                if ((wait -= dt) <= 0) { puddle = Sprite("Puddle", "rock", Feet, .05f); puddle.color = Water; puddle.preserveAspect = false; age = 0; }
                return;
            }
            age += dt;
            float t = Mathf.Clamp01(age / Travel);
            // From the back of the path to the feet, flat like water, growing as it comes near.
            var at = Vector2.Lerp(new Vector2(.5f, .55f), Feet, t);
            Place(puddle.rectTransform, at, Mathf.Lerp(.05f, .2f, t));
            puddle.rectTransform.sizeDelta = new Vector2(puddle.rectTransform.sizeDelta.x * 1.6f, puddle.rectTransform.sizeDelta.y * .45f);
            if (PuddleNear && age - dt < Travel - Early) Cue("Lompat!", Early + Late);
            bool jumped = UsingPose ? Jumps.Count > jumps : Sensor.Energy(Feet, .1f) > .35f;
            jumps = Jumps.Count;
            if (jumped && PuddleNear) { Score++; Burst(Feet + new Vector2(0, .05f), Water); Cue("Hebat!", .8f); Done(); return; }
            if (age > Travel + Late) { Missed++; Cue("Coba lagi!", .8f); Done(); }
            if (UsingPose && Jumps.HasBaseline && !Jumps.Tracking && hintWait <= 0) { Cue("Mundur sedikit, kakimu harus terlihat!", 1.5f); hintWait = 3; }
        }

        void Done()
        {
            Destroy(puddle.gameObject); puddle = null; wait = Between; inSet++;
            if (inSet >= SetSize) { rest = SetRest; Cue("Istirahat dulu, tarik napas...", SetRest - .5f); }
        }

        protected override string DebugText
        {
            get
            {
                var j = Jumps;
                if (!UsingPose)
                    return "Lompat (cadangan tanpa pose)\nsumber: " + (Sensor.HasCamera ? "gerak kamera" : "pointer") + $"\nkepercayaan: rendah\nbintang {Score}  terlewat {Missed}";
                return DebugHeader("Lompat") +
                    $"fase {j.State}  tinggi pinggul {j.Height:0.00}  kaki {j.FeetHeight:0.00}\n" +
                    $"kaki {(j.FeetSeen ? "terlihat" : "tidak terlihat")}, ujung kaki {(j.ToesSeen ? "terlihat" : "tidak ada")}\n" +
                    $"tolak: {(j.Reason == "" ? "-" : j.Reason)}  meleset terakhir: {(j.LastMiss == "" ? "-" : j.LastMiss)}\n" +
                    $"lompatan terakhir {j.Last.peak:0.00}, {(j.Last.end - j.Last.start):0.00} s\n" +
                    $"kaki (v) {j.LegLength:0.000}  lantai {j.FloorHeight:0.000}\n" +
                    $"bintang {Score}  terlewat {Missed}";
            }
        }
    }
}
