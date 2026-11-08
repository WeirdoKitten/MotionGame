using UnityEngine;
using UnityEngine.UI;
namespace FamilyAdventure.Presentation
{
    // 7. Find the Bird (basic movement: body turn). A bird lands to the child's left or right, flies behind
    // them, or circles round them; the child turns the body to face it (a quarter turn), turns their back
    // (half a turn) or turns once all the way round, then faces the screen again. TurnDetector does the
    // counting. Full turns are rare (at most two a round), change direction, and are followed by a rest.
    // Without a pose model the camera cannot see which way the body faces: motion at the bird (sides) or a lot
    // of body motion (back, full turn) stands in for it.
    public sealed class BirdGame : PoseMoveGame
    {
        public enum Ask { Left, Right, Back, SpinLeft, SpinRight }
        // Fixed order: quarter and half turns both ways, a full turn now and then, never two in a row.
        public static readonly Ask[] Order =
        {
            Ask.Left, Ask.Right, Ask.Back, Ask.Right, Ask.Left, Ask.Back, Ask.SpinLeft,
            Ask.Left, Ask.Right, Ask.Back, Ask.SpinRight, Ask.Right, Ask.Left, Ask.Back,
        };
        public const float Hold = .5f, GiveUp = 10f, SpinRest = 5f, Pause = 1f;
        public enum Stage { Waiting, Asking, Returning, Resting }
        static readonly Color BirdColor = new Color(.55f, .85f, 1f);
        protected override string Instruction => UsingPose ? "Putar badanmu menghadap burung!" : "Sentuh burungnya!";
        protected override string Safety => "Putar pelan-pelan, jauh dari meja dan kursi. Kalau pusing, berhenti dan duduk dulu.";
        protected override bool HandsAndFeetOnly => false;
        public TurnDetector Turns { get; } = new TurnDetector();
        protected override IBasicMoveDetector Detector => Turns;
        public Ask Current => Order[index % Order.Length];
        public Stage Now { get; private set; }
        public Vector2 BirdPoint { get; private set; }
        public int Spins { get; private set; }
        Image bird;
        int index, startQuarter, startFull;
        float clock, held, busy, hintWait;
        bool wrongSaid;

        protected override void Setup()
        {
            StartDetector(); index = 0; Spins = 0; Now = Stage.Waiting; clock = Pause; held = busy = hintWait = 0; wrongSaid = false;
            bird = Sprite("Bird", "star", new Vector2(.5f, .9f), .1f); bird.color = BirdColor; bird.gameObject.SetActive(false);
        }

        protected override void Tick(float dt)
        {
            hintWait -= dt;
            if (UsingPose) Feed();
            bool front = !UsingPose || Turns.Settled == TurnDetector.Facing.Front;
            switch (Now)
            {
                case Stage.Waiting:
                    bird.gameObject.SetActive(false);
                    if (front && (clock -= dt) <= 0) Begin();
                    else if (!front && hintWait <= 0)
                    {
                        Cue(Turns.Tracking ? "Hadap ke layar ya!" : "Mundur sedikit, badanmu harus terlihat!", 2); hintWait = 3;
                    }
                    break;
                case Stage.Asking:
                    clock += dt; MoveBird();
                    if (Reached(dt)) { Score++; Burst(new Vector2(.5f, .6f), new Color(1, .9f, .4f)); Done(true); }
                    else if (clock > GiveUp) { Cue("Tidak apa-apa, nanti coba lagi!", 1.5f); Done(false); }
                    break;
                case Stage.Returning:
                    bird.gameObject.SetActive(false);
                    if (front) { Now = Stage.Waiting; clock = Pause; }
                    else if (hintWait <= 0) { Cue("Sekarang hadap depan lagi!", 2); hintWait = 2.5f; }
                    break;
                case Stage.Resting:
                    bird.gameObject.SetActive(false);
                    if ((clock -= dt) <= 0) { Now = Stage.Returning; hintWait = 0; }
                    break;
            }
        }

        void Begin()
        {
            Now = Stage.Asking; clock = 0; held = busy = 0; wrongSaid = false;
            startQuarter = Turns.Quarter; startFull = Turns.FullTurns;
            bird.gameObject.SetActive(true);
            switch (Current)
            {
                case Ask.Left: Cue(UsingPose ? "Burung di kirimu! Hadap ke kiri." : "Sentuh burung di kiri!", 3); break;
                case Ask.Right: Cue(UsingPose ? "Burung di kananmu! Hadap ke kanan." : "Sentuh burung di kanan!", 3); break;
                case Ask.Back: Cue("Burung terbang ke belakangmu! Balik badan.", 3); break;
                case Ask.SpinLeft: Cue("Burung berputar! Putar badan satu kali ke kiri, pelan-pelan.", 4); break;
                default: Cue("Burung berputar! Putar badan satu kali ke kanan, pelan-pelan.", 4); break;
            }
        }

        // The bird sits on the side to turn to (mirror view: the child's left is the left of the screen), flies
        // up behind the child, or circles round them the way to turn.
        void MoveBird()
        {
            float bob = Mathf.Sin(Time.unscaledTime * 4) * .01f;
            switch (Current)
            {
                case Ask.Left: BirdPoint = new Vector2(.08f, .62f + bob); break;
                case Ask.Right: BirdPoint = new Vector2(.92f, .62f + bob); break;
                case Ask.Back: BirdPoint = new Vector2(.5f, .92f + bob); break;
                default:
                    float a = Time.unscaledTime * 1.6f * (Current == Ask.SpinLeft ? 1 : -1);
                    BirdPoint = new Vector2(.5f + Mathf.Cos(a) * .38f, .55f + Mathf.Sin(a) * .3f); break;
            }
            Place(bird.rectTransform, BirdPoint, Current == Ask.Back ? .06f : .1f);
        }

        bool Reached(float dt)
        {
            if (!UsingPose) return Fallback(dt);
            int turned = Turns.Quarter - startQuarter;
            var settled = Turns.Settled;
            switch (Current)
            {
                case Ask.Left:
                case Ask.Right:
                    var want = Current == Ask.Left ? TurnDetector.Facing.Left : TurnDetector.Facing.Right;
                    var wrong = Current == Ask.Left ? TurnDetector.Facing.Right : TurnDetector.Facing.Left;
                    if (settled == wrong && !wrongSaid) { wrongSaid = true; Cue("Ke arah burungnya ya!", 1.5f); }
                    return HeldOn(settled == want && Mathf.Abs(turned) == 1, dt);
                case Ask.Back:
                    return HeldOn(settled == TurnDetector.Facing.Back && Mathf.Abs(turned) == 2, dt);
                default:
                    // Either way round counts; the order asks for the other way next time.
                    return Turns.FullTurns > startFull;
            }
        }
        bool HeldOn(bool on, float dt) { held = on ? held + dt : 0; return held >= Hold; }

        // Without a pose: motion at the bird for the sides, plenty of body motion for the back and full turns.
        bool Fallback(float dt)
        {
            if (Current == Ask.Left || Current == Ask.Right) return Sensor.Energy(BirdPoint, .08f) > .35f;
            if (Sensor.Total(BodyZone) > 20) busy += dt;
            return busy > .8f;
        }

        void Done(bool success)
        {
            bool spin = Current == Ask.SpinLeft || Current == Ask.SpinRight;
            if (spin && success) Spins++;
            index++; bird.gameObject.SetActive(false);
            if (spin) { Now = Stage.Resting; clock = SpinRest; Cue(success ? "Hebat! Istirahat dulu supaya tidak pusing." : "Istirahat dulu ya.", SpinRest - .5f); }
            else if (success) { Now = Stage.Returning; hintWait = 1.2f; Cue("Hebat!", 1.2f); }
            else { Now = Stage.Returning; hintWait = 1.5f; }
        }

        protected override string DebugText
        {
            get
            {
                var t = Turns;
                if (!UsingPose)
                    return "Memutar badan (cadangan tanpa pose)\nsumber: " + (Sensor.HasCamera ? "gerak kamera" : "pointer") + $"\nkepercayaan: rendah\nminta {Current}  bintang {Score}";
                return DebugHeader("Memutar badan") +
                    $"hadap {t.Settled} (sampel {t.Raw})  seperempat {t.Quarter}\n" +
                    $"sudut {t.Yaw:0}  lebar bahu {t.Ratio:0.00}  wajah {(t.Face ? "ya" : "tidak")}  arah isyarat {t.Sign:+0;-0;0}\n" +
                    $"putaran penuh {t.FullTurns}  ragu {(t.Ambiguous ? "ya" : "tidak")}\n" +
                    $"minta {Current}  langkah {Now}  bintang {Score}";
            }
        }
    }
}
