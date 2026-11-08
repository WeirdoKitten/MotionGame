using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace FamilyAdventure.Presentation
{
    // 5. Spin the Windmill (basic movement: arm circle). A windmill stands on one shoulder inside a ring of
    // dots; a small star runs round the ring to show the way. Turning the long arm round the shoulder spins the
    // sails, and one full circle the right way gives a star. After three circles the other arm takes over
    // (and later the other way round), with a short rest. ArmCircleDetector does the counting.
    // Without a pose model the strongest camera or pointer motion on the ring stands in for the wrist.
    public sealed class WindmillGame : PoseMoveGame
    {
        public const int CirclesPerSet = 3;
        public const float SwitchRest = 2.5f;
        // Arm and way per set: outward circles first (right arm clockwise on screen, left counterclockwise),
        // then inward ones.
        static readonly (ArmCircleDetector.Side arm, int way)[] Sets =
        {
            (ArmCircleDetector.Side.Right, -1), (ArmCircleDetector.Side.Left, 1), (ArmCircleDetector.Side.Right, 1), (ArmCircleDetector.Side.Left, -1),
        };
        // Shoulders of the standing guide, used without a pose model.
        public static readonly Vector2 LeftShoulder = new Vector2(.44f, .665f), RightShoulder = new Vector2(.56f, .665f);
        const float GuideArm = .2f;
        protected override string Instruction => UsingPose ? $"Luruskan lengan {ArmName}, putar membuat lingkaran besar ikuti panah!" : "Gambar lingkaran di sekitar kincir, ikuti panah!";
        protected override string Safety => "Beri jarak satu lengan dari benda dan orang di sekitarmu. Putar pelan-pelan saja.";
        protected override bool HandsAndFeetOnly => false;
        public ArmCircleDetector Arms { get; } = new ArmCircleDetector();
        protected override IBasicMoveDetector Detector => Arms;
        // Without a pose model a second detector follows the motion on the ring.
        public ArmCircleDetector Fallback { get; } = new ArmCircleDetector();
        ArmCircleDetector Counting => UsingPose ? Arms : Fallback;
        public ArmCircleDetector.Side Target => Sets[set % Sets.Length].arm;
        public int Way => Sets[set % Sets.Length].way;
        public bool Resting => rest > 0;
        public Vector2 Centre { get; private set; }
        public float RingRadius { get; private set; }
        public float Aspect => Sensor.PoseActive ? Sensor.Pose.aspect : 4f / 3;
        string ArmName => Target == ArmCircleDetector.Side.Left ? "kiri" : "kanan";
        Image windmill, runner;
        readonly List<Image> ring = new List<Image>();
        readonly PoseResult fake = new PoseResult();
        int set, done, seen, otherSeen;
        float rest, hintWait;

        protected override void Setup()
        {
            StartDetector(); Fallback.Reset(); set = done = seen = otherSeen = 0; rest = 0; hintWait = 0; ring.Clear();
            for (int i = 0; i < 16; i++) { var dot = Sprite("Ring", "star", new Vector2(.5f, .5f), .025f); dot.color = new Color(1, 1, 1, .7f); ring.Add(dot); }
            runner = Sprite("Way", "star", new Vector2(.5f, .5f), .05f); runner.color = new Color(1, .85f, .3f);
            windmill = Sprite("Windmill", "star", new Vector2(.5f, .5f), .12f); windmill.color = new Color(.6f, .9f, 1f);
            // The fallback detector stands still with the arms hanging down at the guide.
            for (int i = 0; i < ArmCircleDetector.BaselineSamples; i++) Fallback.Calibrate(Guide(null), 0);
        }

        protected override void Tick(float dt)
        {
            hintWait -= dt;
            if (UsingPose) Feed(); else FollowMotion();
            var c = Counting;
            // Where the windmill stands: the child's shoulder, or the guide's.
            var shoulder = Target == ArmCircleDetector.Side.Left ? PoseJoint.LeftShoulder : PoseJoint.RightShoulder;
            Centre = UsingPose && Sensor.PoseActive && Sensor.Pose.Visible(shoulder) ? Sensor.Pose[shoulder] : Target == ArmCircleDetector.Side.Left ? LeftShoulder : RightShoulder;
            RingRadius = (UsingPose && Arms.HasBaseline ? Arms.ArmLength : GuideArm) * .85f;
            for (int i = 0; i < ring.Count; i++)
            {
                float a = i * Mathf.PI * 2 / ring.Count;
                Place(ring[i].rectTransform, RingPoint(a), .025f); ring[i].gameObject.SetActive(!Resting);
            }
            runner.gameObject.SetActive(!Resting); windmill.gameObject.SetActive(!Resting);
            Place(runner.rectTransform, RingPoint(Way * Time.unscaledTime * 2.2f), .05f);
            Place(windmill.rectTransform, Centre, .12f);
            windmill.rectTransform.localRotation = Quaternion.Euler(0, 0, c.Turned(Target));
            if (Resting)
            {
                if ((rest -= dt) <= 0) { seen = c.Circles(Target); otherSeen = c.Circles(Other); Cue(Instruction, 2); }
                return;
            }
            if (c.Circles(Target) > seen)
            {
                seen = c.Circles(Target);
                if (c.LastDirection(Target) == Way)
                {
                    Score++; done++; Burst(Centre, new Color(1, .9f, .4f)); Cue("Hebat!", .8f);
                    if (done >= CirclesPerSet)
                    {
                        done = 0; set++; rest = SwitchRest;
                        Cue($"Istirahat sebentar. Lalu ganti lengan {ArmName}!", SwitchRest);
                    }
                }
                else Cue("Arahnya sebaliknya ya, ikuti bintang kuning!", 1.5f);
            }
            if (c.Circles(Other) > otherSeen)
            {
                otherSeen = c.Circles(Other);
                if (hintWait <= 0) { Cue($"Pakai lengan {ArmName} ya!", 1.5f); hintWait = 3; }
            }
            if (UsingPose && Arms.HasBaseline && !Arms.Tracking && hintWait <= 0) { Cue("Mundur sedikit, badanmu harus terlihat!", 1.5f); hintWait = 3; }
        }

        ArmCircleDetector.Side Other => Target == ArmCircleDetector.Side.Left ? ArmCircleDetector.Side.Right : ArmCircleDetector.Side.Left;
        Vector2 RingPoint(float angle) => Centre + new Vector2(Mathf.Cos(angle) * RingRadius / Aspect, Mathf.Sin(angle) * RingRadius);

        // Fallback: the strongest motion on the ring around the guide shoulder becomes the wrist.
        void FollowMotion()
        {
            var centre = Target == ArmCircleDetector.Side.Left ? LeftShoulder : RightShoulder;
            float best = .35f; Vector2? wrist = null;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2 / 16;
                var point = centre + new Vector2(Mathf.Cos(a) * GuideArm * .85f * 3 / 4, Mathf.Sin(a) * GuideArm * .85f);
                float e = Sensor.Energy(point, .05f); if (e > best) { best = e; wrist = point; }
            }
            Fallback.Step(Guide(wrist), Time.unscaledTime);
        }
        // A standing body at the guide with the target wrist moved (null = nothing moved, a missed frame).
        PoseResult Guide(Vector2? wrist)
        {
            fake.Clear(); fake.aspect = 4f / 3;
            if (Calibrated && wrist == null) return fake;
            fake.found = true;
            void Set(PoseJoint j, Vector2 p) => fake.Set(j, p, .6f);
            Set(PoseJoint.LeftShoulder, LeftShoulder); Set(PoseJoint.RightShoulder, RightShoulder);
            Set(PoseJoint.LeftHip, new Vector2(.455f, .42f)); Set(PoseJoint.RightHip, new Vector2(.545f, .42f));
            Set(PoseJoint.LeftWrist, LeftShoulder - new Vector2(0, GuideArm)); Set(PoseJoint.RightWrist, RightShoulder - new Vector2(0, GuideArm));
            if (wrist.HasValue) Set(Target == ArmCircleDetector.Side.Left ? PoseJoint.LeftWrist : PoseJoint.RightWrist, wrist.Value);
            fake.DeriveLimbs();
            return fake;
        }
        bool Calibrated => Fallback.HasBaseline;

        protected override string DebugText
        {
            get
            {
                var c = Counting;
                string head = UsingPose ? DebugHeader("Putar lengan") : "Putar lengan (cadangan tanpa pose)\nsumber: " + (Sensor.HasCamera ? "gerak kamera" : "pointer") + "\nkepercayaan: rendah\n";
                return head +
                    $"target lengan {ArmName}, arah {(Way > 0 ? "berlawanan jarum jam" : "searah jarum jam")} (layar)\n" +
                    $"sudut kiri {c.Turned(ArmCircleDetector.Side.Left):0}  kanan {c.Turned(ArmCircleDetector.Side.Right):0}\n" +
                    $"lingkaran kiri {c.Circles(ArmCircleDetector.Side.Left)}  kanan {c.Circles(ArmCircleDetector.Side.Right)}\n" +
                    $"jari-jari kiri {c.Radius(ArmCircleDetector.Side.Left):0.00}  kanan {c.Radius(ArmCircleDetector.Side.Right):0.00} (min {c.MinRadius:0.00})\n" +
                    $"panjang lengan {c.ArmLength:0.000}  meleset terakhir: {(c.LastMiss == "" ? "-" : c.LastMiss)}\n" +
                    $"bintang {Score}  set {set + 1}";
            }
        }
    }
}
