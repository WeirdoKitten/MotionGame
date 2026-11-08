using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace FamilyAdventure.Presentation
{
    // 4. Pick the Fruit (basic movement: squat). A fruit lies on the ground beside the feet; lowering the hips
    // to the line picks it up and standing up again puts it in the basket. SquatDetector finds the squat from
    // the pose model; this class only shows it. Without a pose model the game falls back to camera motion or
    // the pointer: touch the fruit low down, then move at head height (a rough stand-in, see docs/49).
    public sealed class PickFruitGame : PoseMoveGame
    {
        // Seconds between fruits, and a longer rest after every few so the legs do not tire.
        public const float Rest = 1.2f, Break = 4f;
        public const int BreakEvery = 6;
        public static readonly Vector2 HeadPoint = new Vector2(.5f, .85f);
        static readonly Color FruitColor = new Color(1, .42f, .28f), LineColor = new Color(1, 1, 1, .8f), LineReached = new Color(1, .85f, .3f, 1);
        protected override string Instruction => UsingPose ? "Jongkok sampai garis, ambil buahnya, lalu berdiri!" : "Sentuh buah di bawah, lalu berdiri tegak!";
        protected override string Safety => "Main di tempat yang lapang, jauh dari meja dan kursi. Lantainya jangan licin ya!";
        // The whole body moves; nothing is ignored.
        protected override bool HandsAndFeetOnly => false;
        public SquatDetector Squat { get; } = new SquatDetector();
        protected override IBasicMoveDetector Detector => Squat;
        public bool Holding { get; private set; }
        public bool FruitReady => fruit != null && !Holding;
        public Vector2 FruitPoint { get; private set; }
        public float TargetLine => Squat.TargetHipHeight;
        Image fruit, hipMark, line;
        RectTransform basket;
        readonly List<Image> basketFruits = new List<Image>();
        int squats, misses, side;
        float wait, heldAt, lostFor, hintWait;

        protected override void Setup()
        {
            StartDetector(); squats = misses = 0; side = 0; Holding = false; fruit = null;
            wait = .8f; lostFor = 0; hintWait = 0; basketFruits.Clear();
            // Target line at the hip height of a half squat, and a marker on the child's hips.
            line = UiKit.Rect("Squat line", area).gameObject.AddComponent<Image>(); line.color = LineColor; line.raycastTarget = false;
            line.gameObject.SetActive(false);
            hipMark = Sprite("Hips", "star", new Vector2(.5f, .5f), .06f); hipMark.color = new Color(.5f, 1, .6f, .95f); hipMark.gameObject.SetActive(false);
            basket = UiKit.Rect("Basket", area); UiKit.Skinned(basket.gameObject, UiKit.Skin.Wood); UiKit.Anchor(basket, .8f, .05f, .96f, .2f);
            var label = UiKit.Label(basket, "Keranjang", 22, Color.white); UiKit.Anchor(label.rectTransform, 0, .62f, 1, 1); label.alignment = TextAnchor.MiddleCenter;
        }

        protected override void Tick(float dt)
        {
            hintWait -= dt;
            if (UsingPose) PoseTick(dt); else MotionTick(dt);
            if (fruit != null)
            {
                // On the ground it bobs a little; picked up it rides along with the hips.
                var at = Holding && UsingPose && Sensor.PoseActive ? Sensor.Pose.hips + new Vector2(0, .04f) : FruitPoint + new Vector2(0, Mathf.Sin(Time.unscaledTime * 3) * .006f);
                if (Holding && !UsingPose) at = Vector2.Lerp(FruitPoint, HeadPoint, .5f);
                Place(fruit.rectTransform, at, .09f);
            }
            if (fruit == null && (wait -= dt) <= 0 && (!UsingPose || Squat.State == SquatDetector.Phase.Up)) Spawn();
        }

        void PoseTick(float dt)
        {
            Feed();
            bool baseline = Squat.HasBaseline;
            line.gameObject.SetActive(baseline);
            if (baseline)
            {
                var r = line.rectTransform; r.anchorMin = new Vector2(.34f, TargetLine); r.anchorMax = new Vector2(.66f, TargetLine);
                r.sizeDelta = new Vector2(0, 10); r.anchoredPosition = Vector2.zero;
                line.color = Squat.State == SquatDetector.Phase.Low ? LineReached : LineColor;
            }
            hipMark.gameObject.SetActive(Sensor.PoseActive);
            if (Sensor.PoseActive) Place(hipMark.rectTransform, Sensor.Pose.hips, .06f);

            // Body out of view: a light hint, and a fruit being carried goes back to the ground.
            if (baseline && !Squat.Tracking)
            {
                Holding = false; lostFor += dt;
                if (lostFor > 1 && hintWait <= 0) { Cue("Mundur sedikit, seluruh badan harus terlihat!", 1.5f); hintWait = 3; }
            }
            else lostFor = 0;

            if (FruitReady && Squat.State == SquatDetector.Phase.Low) { Holding = true; Cue("Berdiri, masukkan ke keranjang!", 1.5f); }
            if (Squat.Count > squats) { squats = Squat.Count; if (Holding) Collect(); }
            if (Squat.Incomplete > misses)
            {
                misses = Squat.Incomplete;
                if (fruit != null) { Holding = false; Cue(MissCue(Squat.LastMiss), 1.2f); }
            }
            if (Squat.Bending && FruitReady && hintWait <= 0) { Cue("Tekuk lututmu, badan tetap tegak!", 1.5f); hintWait = 3; }
        }

        // Fallback without a pose model: motion on the fruit picks it, motion at head height after a moment
        // counts as standing up again. It cannot tell a squat from bending over or waving (see docs/49).
        void MotionTick(float dt)
        {
            line.gameObject.SetActive(false); hipMark.gameObject.SetActive(false);
            if (fruit == null) return;
            float now = Time.unscaledTime;
            if (!Holding && Sensor.Energy(FruitPoint, .08f) > .35f) { Holding = true; heldAt = now; Cue("Sekarang berdiri tegak!", 1.5f); }
            else if (Holding && now - heldAt > .6f && Sensor.Energy(HeadPoint, .08f) > .35f) Collect();
        }

        static string MissCue(string why)
        {
            switch (why)
            {
                case "lutut di lantai": return "Jongkok saja, lututnya jangan di lantai!";
                case "kaki terangkat": return "Kakinya tetap di lantai ya!";
                case "duduk": return "Jongkok saja, jangan duduk!";
                case "terlalu cepat": return "Pelan-pelan saja!";
                default: return "Turun sedikit lagi!";
            }
        }

        void Spawn()
        {
            // Beside the feet, left and right in turn, at floor height when the floor is known.
            side = 1 - side;
            float x = UsingPose && Sensor.PoseActive ? Sensor.Pose.hips.x : .5f;
            float floor = UsingPose && Squat.HasBaseline ? Mathf.Clamp(Squat.FloorHeight, .06f, .3f) : .12f;
            FruitPoint = new Vector2(Mathf.Clamp(x + (side == 0 ? -.14f : .14f), .1f, .9f), floor);
            fruit = Sprite("Fruit", "star", FruitPoint, .09f); fruit.color = FruitColor;
            Holding = false;
            if (hintWait <= 0) Cue(Instruction, 1.5f);
        }

        void Collect()
        {
            Score++; Holding = false;
            var centre = new Vector2(.88f, .12f);
            Burst(centre, new Color(1, .9f, .4f)); Cue("Hebat!", .8f);
            Destroy(fruit.gameObject); fruit = null;
            // Fruits pile up in the basket (up to two rows).
            if (basketFruits.Count < 12)
            {
                int i = basketFruits.Count;
                var f = Sprite("Basket fruit", "star", new Vector2(.82f + (i % 6) * .024f, .08f + (i / 6) * .035f), .035f); f.color = FruitColor;
                basketFruits.Add(f);
            }
            wait = Rest;
            if (Score % BreakEvery == 0) { wait = Break; Cue("Istirahat sebentar, tarik napas...", Break - .5f); hintWait = Break; }
        }

        protected override string DebugText
        {
            get
            {
                var s = Squat;
                if (!UsingPose)
                    return "Jongkok (cadangan tanpa pose)\nsumber: " + (Sensor.HasCamera ? "gerak kamera" : "pointer") +
                        $"\nbuah: {(Holding ? "dipegang" : FruitReady ? "di tanah" : "-")}\nkepercayaan: rendah\nbintang {Score}";
                string model = Sensor.Model.HasValue ? $"{Sensor.Model.Value} {Sensor.Pose.milliseconds:0} ms" : "pose uji";
                return $"Jongkok | {model}\n" +
                    $"fase {s.State}  arah {s.Direction:+0;-0;0}\n" +
                    $"kedalaman {s.Depth:0.00}  puncak {s.Peak:0.00}\n" +
                    $"target {s.TargetDepth:0.00}  berdiri {s.StandDepth:0.00}\n" +
                    $"progres {s.Progress:0.00}  kepercayaan {s.Confidence:0.00}\n" +
                    $"jumlah {s.Count}  tidak lengkap {s.Incomplete}\n" +
                    $"meleset terakhir: {(s.LastMiss == "" ? "-" : s.LastMiss)}\n" +
                    $"tolak: {(s.Reason == "" ? "-" : s.Reason)}\n" +
                    $"kaki {(s.FeetSeen ? "terlihat" : "tidak terlihat")}, lantai dari {(s.FloorFromFeet ? "kaki" : "badan")}\n" +
                    $"kaki (v) {s.LegLength:0.000}  lantai {s.FloorHeight:0.000}\n" +
                    $"pinggul berdiri {s.StandingHipHeight:0.000}  garis {s.TargetHipHeight:0.000}";
            }
        }
    }
}
