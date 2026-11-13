using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace FamilyAdventure.Presentation
{
    // Motion missions opened from the egg house on the main menu: Catch the Star, Follow the Line, Avoid the
    // Rock and Ikuti Senam (a whole senam converted from a video, docs/52). Specs: docs/motion. All use
    // MotionSensor (pose model, or camera frame differencing); positions are normalized screen coordinates
    // (u right, v up). No failure states: misses only get a light cue.
    public sealed class MotionMissionMenu : MonoBehaviour
    {
        public static readonly string[] Titles = { "Catch the Star", "Follow the Line", "Avoid the Rock", "Ikuti Senam" };
        // Ikuti Senam has no art of its own yet and borrows the star background.
        static readonly string[] Backgrounds = { "bg-star", "bg-line", "bg-rock", "bg-star" };
        static readonly Func<GameObject, MotionGame>[] Games =
        {
            h => h.AddComponent<StarCatchGame>(), h => h.AddComponent<FollowLineGame>(), h => h.AddComponent<AvoidRockGame>(), h => h.AddComponent<SenamGame>(),
        };
        static readonly UiKit.Skin[] Plates = { UiKit.Skin.Gold, UiKit.Skin.Leaf, UiKit.Skin.Wood, UiKit.Skin.Leaf };
        Action closed;
        RectTransform root;
        public MotionGame Current { get; private set; }
        public static MotionMissionMenu Open(Action onClosed)
        {
            var menu = new GameObject("Motion missions").AddComponent<MotionMissionMenu>(); menu.closed = onClosed; menu.Build(); return menu;
        }
        void Build()
        {
            root = MotionUi.Canvas(transform, "Mission menu", 40);
            var dim = UiKit.Panel("Dim", root, new Color(.05f, .12f, .08f, .45f)); UiKit.Anchor(dim, 0, 0, 1, 1);
            var title = UiKit.Banner(root, "Misi Gerak", 46); UiKit.Anchor((RectTransform)title.transform.parent, .33f, .79f, .67f, .93f);
            // All cards in one row between the margins.
            float gap = .025f, width = (.92f - gap * (Titles.Length - 1)) / Titles.Length;
            for (int i = 0; i < Titles.Length; i++)
            {
                int index = i;
                float x0 = .04f + i * (width + gap);
                var card = UiKit.Panel("Mission " + (i + 1), root, UiKit.Paper); UiKit.Anchor(card, x0, .2f, x0 + width, .74f);
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = card.GetComponent<Image>();
                button.onClick.AddListener(() => Play(index));
                var picture = MotionUi.Picture(card, "Picture", Backgrounds[i]); UiKit.Anchor(picture.rectTransform, 0, .3f, 1, 1, 10);
                picture.preserveAspect = false; picture.raycastTarget = false;
                var plate = UiKit.Rect("Title plate", card); UiKit.Skinned(plate.gameObject, Plates[i % Plates.Length]);
                UiKit.Anchor(plate, .06f, .05f, .94f, .26f);
                var number = UiKit.Label(plate, Titles[i], 30, Color.white); UiKit.Anchor(number.rectTransform, 0, 0, 1, 1, 6);
                number.alignment = TextAnchor.MiddleCenter; number.font = UiKit.Bold;
                // Narrower cards: long titles shrink to fit.
                number.resizeTextForBestFit = true; number.resizeTextMinSize = 18; number.resizeTextMaxSize = 30;
                var o = number.gameObject.AddComponent<Outline>(); o.effectColor = new Color(.25f, .14f, .05f, .85f); o.effectDistance = new Vector2(2, -2);
                var badge = UiKit.Disc("Number", card); badge.anchorMin = badge.anchorMax = new Vector2(.12f, .9f); badge.sizeDelta = new Vector2(64, 64);
                var n = UiKit.Label(badge, (i + 1).ToString(), 34, Color.white); UiKit.Anchor(n.rectTransform, 0, 0, 1, 1); n.alignment = TextAnchor.MiddleCenter; n.font = UiKit.Bold;
            }
            MotionUi.Close(root, Close);
            var model = UiKit.Button(root, ModelName(MotionSensor.PreferredModel), () => { }, false, 60, "hand"); model.name = "Model";
            UiKit.Anchor((RectTransform)model.transform, .35f, .05f, .65f, .14f);
            var label = model.GetComponentInChildren<Text>();
            model.onClick.AddListener(() =>
            {
                // BlazePose -> MoveNet -> camera motion only -> BlazePose (for comparing).
                MotionSensor.PreferredModel = MotionSensor.PreferredModel == PoseModelKind.BlazePose ? PoseModelKind.MoveNet
                    : MotionSensor.PreferredModel == PoseModelKind.MoveNet ? (PoseModelKind?)null : PoseModelKind.BlazePose;
                label.text = ModelName(MotionSensor.PreferredModel);
            });
        }
        public static string ModelName(PoseModelKind? kind) => kind.HasValue ? "Deteksi: " + kind.Value : "Deteksi: Gerak kamera";
        public void Play(int index)
        {
            root.gameObject.SetActive(false);
            var host = new GameObject(Titles[index]);
            host.transform.SetParent(transform, false);
            Current = Games[index](host);
            Current.Begin(Backgrounds[index], Titles[index], () => { Current = null; root.gameObject.SetActive(true); });
        }
        public void Close() { closed?.Invoke(); Destroy(gameObject); }
    }

    static class MotionUi
    {
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static Sprite Sprite(string name)
        {
            if (sprites.TryGetValue(name, out var sprite) && sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>("Motion/" + name);
            if (texture == null) return null;
            sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
            sprites[name] = sprite; return sprite;
        }
        public static RectTransform Canvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = .5f;
            return (RectTransform)go.transform;
        }
        public static Image Picture(Transform parent, string name, string sprite)
        {
            var r = UiKit.Rect(name, parent); var image = r.gameObject.AddComponent<Image>(); image.sprite = Sprite(sprite); image.preserveAspect = true; image.raycastTarget = false; return image;
        }
        public static Button Close(RectTransform parent, Action action)
        {
            var r = UiKit.Disc("Close", parent); r.anchorMin = r.anchorMax = new Vector2(.955f, .925f); r.sizeDelta = new Vector2(78, 78);
            var b = r.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => action());
            var g = UiKit.Rect("Icon", r); UiKit.Anchor(g, 0, 0, 1, 1, 12); var icon = g.gameObject.AddComponent<IntroIcon>(); icon.symbol = "close"; icon.color = Color.white; icon.raycastTarget = false;
            return b;
        }
    }

    // Shared frame: background, mirrored camera preview, countdown, timer, star count and results.
    public abstract class MotionGame : MonoBehaviour
    {
        public const float PlayTime = 60, CameraOpacity = .55f, CalibrationTime = 5, StillLimit = 6;
        // Where the child stands (normalized): body and head inside this box; arms reach out, feet below it.
        public static readonly Rect BodyZone = new Rect(.41f, .28f, .18f, .62f);
        // Games that count only hands and feet ignore motion in the body zone.
        protected virtual bool HandsAndFeetOnly => true;
        public float Calibration { get; private set; }
        // The game never starts on an empty view: the child must first be seen moving inside the guide.
        public bool Present { get; private set; }
        public const float PresenceMotion = 4;
        RectTransform guide; Image guideFill;
        public MotionSensor Sensor { get; private set; }
        public int Score { get; protected set; }
        public enum Phase { Calibrate, Ready, Play, Done }
        public Phase State { get; private set; }
        protected RectTransform area;
        RectTransform root, doneBox;
        Text countdown, scoreText, cue;
        Image timerFill;
        RawImage preview;
        Text modelText;
        readonly Image[] limbMarks = new Image[4];
        float clock, cueTime;
        Action finished;
        protected abstract string Instruction { get; }
        public void Begin(string background, string title, Action onFinished)
        {
            finished = onFinished;
            Sensor = gameObject.AddComponent<MotionSensor>();
            root = MotionUi.Canvas(transform, title, 45);
            var back = MotionUi.Picture(root, "Background", background); back.preserveAspect = false; back.raycastTarget = true; UiKit.Anchor(back.rectTransform, 0, 0, 1, 1);
            // Full-screen mirrored camera picture, see-through over the background, so the child sees their
            // hand exactly where the stars and lines are (the motion grid covers the same full screen).
            preview = UiKit.Rect("Camera", root).gameObject.AddComponent<RawImage>(); UiKit.Anchor(preview.rectTransform, 0, 0, 1, 1);
            preview.uvRect = new Rect(1, 0, -1, 1); preview.raycastTarget = false; preview.color = Color.clear;
            area = UiKit.Rect("Play area", root); UiKit.Anchor(area, 0, 0, 1, 1);
            var bar = UiKit.Rect("Timer", root); UiKit.Skinned(bar.gameObject, UiKit.Skin.Wood); UiKit.Anchor(bar, .3f, .925f, .7f, .975f);
            timerFill = UiKit.Panel("Fill", bar, UiKit.Gold).GetComponent<Image>(); UiKit.Anchor(timerFill.rectTransform, 0, 0, 1, 1, 7);
            var scorePlate = UiKit.Rect("Score plate", root); UiKit.Skinned(scorePlate.gameObject, UiKit.Skin.Wood); UiKit.Anchor(scorePlate, .012f, .885f, .17f, .975f);
            var star = MotionUi.Picture(root, "Score star", "star"); UiKit.Anchor(star.rectTransform, .02f, .885f, .08f, .975f);
            scoreText = Text(root, "0", 46, .085f, .885f, .165f, .975f); scoreText.alignment = TextAnchor.MiddleLeft;
            cue = Text(root, Instruction, 34, .15f, .8f, .85f, .9f);
            countdown = Text(root, "3", 150, .3f, .3f, .7f, .7f);
            MotionUi.Close(root, Quit);
            modelText = Text(root, "", 20, .7f, .01f, .99f, .05f); modelText.alignment = TextAnchor.MiddleRight;
            for (int i = 0; i < 4; i++)
            {
                limbMarks[i] = MotionUi.Picture(root, "Limb " + i, "star"); limbMarks[i].rectTransform.sizeDelta = new Vector2(46, 46);
                limbMarks[i].color = i < 2 ? new Color(1, .95f, .5f, .9f) : new Color(.5f, .85f, 1, .9f); limbMarks[i].gameObject.SetActive(false);
            }
            BuildGuide();
            StartCalibration();
            Setup();
        }
        protected Text Text(Transform parent, string value, int size, float x0, float y0, float x1, float y1)
        {
            var t = UiKit.Label(parent, value, size, Color.white); UiKit.Anchor(t.rectTransform, x0, y0, x1, y1);
            t.alignment = TextAnchor.MiddleCenter; t.font = UiKit.Bold; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = t.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.1f, .2f, .15f, .9f); outline.effectDistance = new Vector2(3, -3);
            return t;
        }
        protected void Cue(string text, float seconds = 1.2f) { cue.text = text; cueTime = seconds; }
        // Places a sprite image centred on a normalized point, sized as a fraction of screen height.
        protected Image Sprite(string name, string sprite, Vector2 uv, float size)
        {
            var image = MotionUi.Picture(area, name, sprite); Place(image.rectTransform, uv, size); return image;
        }
        protected static void Place(RectTransform r, Vector2 uv, float size)
        {
            r.anchorMin = r.anchorMax = uv; r.sizeDelta = new Vector2(size * 900, size * 900); r.anchoredPosition = Vector2.zero;
        }
        protected void Burst(Vector2 uv, Color color)
        {
            for (int i = 0; i < 8; i++)
            {
                var bit = Sprite("Sparkle", "star", uv, .04f); bit.color = color;
                bit.gameObject.AddComponent<MotionSpark>().velocity = new Vector2(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4)) * 260;
            }
        }
        // Standing guide: a body outline to stand in, with a bar that fills while the child keeps still.
        void BuildGuide()
        {
            guide = UiKit.Rect("Calibration", root); UiKit.Anchor(guide, 0, 0, 1, 1);
            var z = BodyZone; var line = new Color(1, 1, 1, .45f);
            void Part(string name, float x0, float y0, float x1, float y1)
            {
                var r = UiKit.Panel(name, guide, new Color(.6f, 1, .7f, .07f)); r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
                r.GetComponent<Image>().raycastTarget = false; var o = r.gameObject.AddComponent<Outline>(); o.effectColor = line; o.effectDistance = new Vector2(2, 2);
            }
            float cx = z.center.x;
            Part("Head", cx - .035f, z.yMax - .15f, cx + .035f, z.yMax);
            Part("Body", z.xMin + .03f, z.yMin + .12f, z.xMax - .03f, z.yMax - .16f);
            Part("Left arm", z.xMin - .12f, z.yMax - .26f, z.xMin + .03f, z.yMax - .21f);
            Part("Right arm", z.xMax - .03f, z.yMax - .26f, z.xMax + .12f, z.yMax - .21f);
            Part("Left leg", cx - .06f, .08f, cx - .02f, z.yMin + .12f);
            Part("Right leg", cx + .02f, .08f, cx + .06f, z.yMin + .12f);
            var bar = UiKit.Panel("Still bar", guide, new Color(1, 1, 1, .6f)); UiKit.Anchor(bar, .3f, .015f, .7f, .05f);
            guideFill = UiKit.Panel("Fill", bar, new Color(.4f, .9f, .5f)).GetComponent<Image>(); UiKit.Anchor(guideFill.rectTransform, 0, 0, 0, 1, 3);
        }
        void StartCalibration()
        {
            State = Phase.Calibrate; Calibration = 0; Present = false; guide.gameObject.SetActive(true); countdown.gameObject.SetActive(false);
            cue.text = "Masuk ke dalam garis dan lambaikan tangan!";
            Sensor.Ignore = default;
        }
        protected abstract void Setup();
        protected abstract void Tick(float dt);
        // Every frame while the child stands still in the guide and during the countdown (for baselines).
        protected virtual void Standing() { }
        // Length of one round in seconds (a senam lasts as long as its video).
        protected virtual float RoundTime => PlayTime;
        void Update()
        {
            if (preview != null && Sensor.Preview != null && preview.texture == null) { preview.texture = Sensor.Preview; preview.color = new Color(1, 1, 1, CameraOpacity); }
            float dt = Time.unscaledDeltaTime;
            bool pose = Sensor.PoseActive;
            modelText.text = Sensor.Model.HasValue ? Sensor.Model.Value + (pose ? $"  {Sensor.Pose.milliseconds:0} ms" : "  mencari...") : (Sensor.HasCamera ? "Gerak kamera" : "Tanpa kamera");
            for (int i = 0; i < 4; i++)
            {
                bool show = pose && Sensor.Pose.seen[i]; limbMarks[i].gameObject.SetActive(show);
                if (show) { var r = limbMarks[i].rectTransform; r.anchorMin = r.anchorMax = Sensor.Pose.limbs[i]; r.anchoredPosition = Vector2.zero; }
            }
            if (cueTime > 0 && (cueTime -= dt) <= 0) cue.text = State == Phase.Play ? Instruction : cue.text;
            if (State == Phase.Calibrate)
            {
                // First the child steps into the guide (motion there); then any movement resets the timer and
                // five still seconds start the game. Until both happen the game does not start.
                if (!Present)
                {
                    var inside = new Rect(BodyZone.xMin - .12f, 0, BodyZone.width + .24f, 1);
                    if (Sensor.Total(inside) > PresenceMotion || Sensor.PoseActive) { Present = true; cue.text = "Berdiri di dalam garis. Tangan dan kaki diam 5 detik!"; }
                    Calibration = 0;
                }
                else if (Sensor.Total() > StillLimit) Calibration = 0; else Calibration += dt;
                if (Present) Standing();
                guideFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(Calibration / CalibrationTime), 1);
                if (Calibration >= CalibrationTime)
                {
                    guide.gameObject.SetActive(false); countdown.gameObject.SetActive(true); cue.text = Instruction;
                    Sensor.Ignore = HandsAndFeetOnly ? BodyZone : default;
                    State = Phase.Ready; clock = 3.5f;
                }
                return;
            }
            if (State == Phase.Ready)
            {
                Standing();
                clock -= dt; countdown.text = clock > 1 ? ((int)clock).ToString() : "Mulai!";
                if (clock <= 0) { countdown.gameObject.SetActive(false); State = Phase.Play; clock = RoundTime; Sensor.Clear(); }
                return;
            }
            if (State != Phase.Play) return;
            clock -= dt; timerFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(clock / RoundTime), 1);
            Tick(dt);
            scoreText.text = Score.ToString();
            if (clock <= 0) Finish();
        }
        // Tests can shorten a round.
        public void EndNow() { if (State == Phase.Play) clock = 0; }
        void Finish()
        {
            State = Phase.Done; cue.text = "";
            foreach (Transform child in area) child.gameObject.SetActive(false);
            doneBox = UiKit.Panel("Result", root, UiKit.Paper); UiKit.Anchor(doneBox, .3f, .22f, .7f, .72f);
            var banner = UiKit.Banner(doneBox, "Hebat!", 52); var plate = (RectTransform)banner.transform.parent;
            plate.anchorMin = new Vector2(.12f, .86f); plate.anchorMax = new Vector2(.88f, 1.12f); plate.offsetMin = plate.offsetMax = Vector2.zero;
            var star = MotionUi.Picture(doneBox, "Result star", "star"); UiKit.Anchor(star.rectTransform, .22f, .36f, .45f, .64f);
            var count = UiKit.Label(doneBox, "x " + Score, 60, UiKit.Ink); UiKit.Anchor(count.rectTransform, .47f, .36f, .85f, .64f); count.alignment = TextAnchor.MiddleLeft;
            var again = UiKit.Button(doneBox, "Main lagi", Restart, true, 72, "retry"); again.name = "Again"; UiKit.Anchor((RectTransform)again.transform, .05f, .07f, .48f, .3f);
            var back = UiKit.Button(doneBox, "Misi lain", Quit, false, 72, "home"); back.name = "Back"; UiKit.Anchor((RectTransform)back.transform, .52f, .07f, .95f, .3f);
        }
        void Restart()
        {
            Destroy(doneBox.gameObject);
            foreach (Transform child in area) Destroy(child.gameObject);
            Score = 0; StartCalibration();
            Setup();
        }
        public void Quit() { finished?.Invoke(); Destroy(gameObject); }
    }

    sealed class MotionSpark : MonoBehaviour
    {
        public Vector2 velocity; float age;
        void Update()
        {
            age += Time.unscaledDeltaTime; var r = (RectTransform)transform; r.anchoredPosition += velocity * Time.unscaledDeltaTime;
            GetComponent<Image>().color *= new Color(1, 1, 1, Mathf.Clamp01(1 - Time.unscaledDeltaTime * 2));
            if (age > .6f) Destroy(gameObject);
        }
    }

    // 1. Catch the Star: a hand or foot picture shows where (and how) to reach; putting a hand or foot on
    // it turns it into a star that flies to the star count. The pictures are mirrored like the camera view:
    // targets on the right are for the right hand. The hand shape is only a cue for now: the pose models do
    // not see fingers yet, so only the position is checked (docs/51).
    public sealed class StarCatchGame : MotionGame
    {
        static readonly (Vector2 point, HandShape shape)[] Pattern =
        {
            // Hands: out to the sides and up; feet: low beside the legs. Never on the body.
            (new Vector2(.28f, .6f), HandShape.Open), (new Vector2(.72f, .6f), HandShape.Open),
            (new Vector2(.25f, .78f), HandShape.Open), (new Vector2(.75f, .78f), HandShape.Open),
            (new Vector2(.36f, .14f), HandShape.Open), (new Vector2(.64f, .14f), HandShape.Open),
            (new Vector2(.2f, .45f), HandShape.Side), (new Vector2(.8f, .45f), HandShape.Side),
            (new Vector2(.34f, .93f), HandShape.Fist), (new Vector2(.66f, .93f), HandShape.Fist),
        };
        // Targets below this height are for the feet.
        public const float FootLine = .3f;
        static readonly Color HandColor = new Color(1, .86f, .3f), FootColor = new Color(.45f, .8f, 1);
        protected override string Instruction => "Taruh tangan atau kakimu di gambarnya, jadi bintang!";
        // The current target picture (named Star because catching it gives a star).
        public Image Star { get; private set; }
        public Vector2 StarPoint { get; private set; }
        public HandShape Shape { get; private set; }
        public bool ForFeet => StarPoint.y < FootLine;
        public bool RightSide => StarPoint.x > .5f;
        float age, wait;
        int next;
        protected override void Setup() { next = 0; wait = .3f; Star = null; }
        string ShapeCue => ForFeet ? "Pakai kaki!" : Shape == HandShape.Fist ? "Kepalkan tangan!" : Shape == HandShape.Side ? "Tangan menyamping!" : "Buka tanganmu!";
        protected override void Tick(float dt)
        {
            if (Star == null)
            {
                if ((wait -= dt) > 0) return;
                var step = Pattern[next % Pattern.Length];
                StarPoint = step.point + (next >= Pattern.Length ? new Vector2(UnityEngine.Random.Range(-.04f, .04f), UnityEngine.Random.Range(-.03f, .03f)) : Vector2.zero);
                Shape = step.shape; next++; age = 0;
                Star = Sprite("Target", "star", StarPoint, .06f);
                Star.sprite = ForFeet ? LimbIcons.Foot() : LimbIcons.Hand(Shape);
                Cue(ShapeCue, 1.5f);
                return;
            }
            age += dt;
            // The picture grows in, then waits; it glows while a hand (or foot) is close.
            float grow = Mathf.Clamp01(age / 1.2f), energy = Sensor.LimbEnergy(StarPoint, .07f, ForFeet);
            bool near = energy > .15f;
            var r = Star.rectTransform;
            Place(r, StarPoint + new Vector2(0, Mathf.Sin(age * 3) * .008f), Mathf.Lerp(.06f, .19f, grow) * (near ? 1.12f : 1));
            r.localScale = new Vector3(RightSide ? 1 : -1, 1, 1);
            r.localRotation = Quaternion.Euler(0, 0, Tilt());
            var color = ForFeet ? FootColor : HandColor; color.a = near ? 1 : .6f + .15f * Mathf.Sin(age * 4); Star.color = color;
            if (grow >= .6f && energy > .35f)
            {
                Score++; Burst(StarPoint, new Color(1, .9f, .4f)); Cue("Hebat!", .8f); FlyToScore(StarPoint);
                Destroy(Star.gameObject); Star = null; wait = .5f;
            }
            else if (age > 6)
            {
                Cue("Coba lagi!", .8f); Destroy(Star.gameObject); Star = null; wait = .5f;
            }
        }
        // Fingers lean away from the shoulder, so a hand out to the side lies sideways. Feet stay upright.
        float Tilt()
        {
            if (ForFeet) return 0;
            var shoulder = new Vector2(RightSide ? BodyZone.xMax : BodyZone.xMin, BodyZone.yMax - .23f);
            var d = StarPoint - shoulder; d.x *= 1.6f; // the screen is wider than it is tall
            float angle = Mathf.DeltaAngle(0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90);
            return Mathf.Clamp(angle * .6f, -70, 70);
        }
        // The caught picture becomes a star flying to the star count (top left).
        void FlyToScore(Vector2 from)
        {
            var star = Sprite("Caught star", "star", from, .12f);
            star.gameObject.AddComponent<MotionSpark>().velocity = Vector2.Scale(new Vector2(.05f, .93f) - from, area.rect.size) / .6f;
        }
    }

    // 2. Follow the Line: a glowing star leads along a dotted line; keep moving your hand on the star to
    // pull it forward. Finishing a line lights it gold and the next shape appears.
    public sealed class FollowLineGame : MotionGame
    {
        static Vector2[] Shape(int index)
        {
            var points = new List<Vector2>();
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f;
                switch (index % 5)
                {
                    // Right hand up the right side; left hand zigzags down the left side.
                    case 0: points.Add(new Vector2(.74f, Mathf.Lerp(.3f, .85f, t))); break;
                    case 1: points.Add(new Vector2(.26f + (Mathf.PingPong(t * 4, 1) - .5f) * .12f, Mathf.Lerp(.85f, .3f, t))); break;
                    // Feet: along the floor under the body.
                    case 2: points.Add(new Vector2(Mathf.Lerp(.2f, .8f, t), .13f)); break;
                    // Right hand waves down the right side; both hands arch over the head.
                    case 3: points.Add(new Vector2(.72f + Mathf.Sin(t * Mathf.PI * 2) * .07f, Mathf.Lerp(.85f, .3f, t))); break;
                    default: points.Add(new Vector2(.5f - Mathf.Cos(t * Mathf.PI) * .3f, .6f + Mathf.Sin(t * Mathf.PI) * .33f)); break;
                }
            }
            return points.ToArray();
        }
        protected override string Instruction => "Ikuti bintang di garis dengan tangan atau kakimu!";
        public Vector2[] Path { get; private set; }
        public float Progress { get; private set; }
        public int Lines { get; private set; }
        readonly List<Image> dots = new List<Image>();
        Image leader;
        public Vector2 Leader => Point(Progress);
        protected override void Setup() { Lines = 0; NewLine(); }
        void NewLine()
        {
            foreach (var d in dots) if (d != null) Destroy(d.gameObject);
            dots.Clear(); if (leader != null) Destroy(leader.gameObject);
            Path = Shape(Lines); Progress = 0;
            for (int i = 0; i < Path.Length; i++) { var dot = Sprite("Line dot", "star", Path[i], .03f); dot.color = new Color(.5f, .9f, 1f, .75f); dots.Add(dot); }
            leader = Sprite("Leader", "star", Path[0], .13f);
        }
        Vector2 Point(float progress)
        {
            float f = Mathf.Clamp01(progress) * (Path.Length - 1); int i = Mathf.Min(Path.Length - 2, (int)f);
            return Vector2.Lerp(Path[i], Path[i + 1], f - i);
        }
        protected override void Tick(float dt)
        {
            var point = Leader;
            // Moving on (or just ahead of) the leader pulls it along; the corridor is generous.
            float energy = Mathf.Max(Sensor.Energy(point, .08f), Sensor.Energy(Point(Progress + .04f), .08f));
            // Where a line passes over the body the star glides on by itself.
            if (BodyZone.Contains(point)) energy = 1;
            if (energy > .3f) Progress = Mathf.Min(1, Progress + dt * .22f);
            Place(leader.rectTransform, Leader, .13f + Mathf.Sin(Time.unscaledTime * 6) * .01f);
            int lit = Mathf.FloorToInt(Progress * (Path.Length - 1));
            for (int i = 0; i <= lit && i < dots.Count; i++) dots[i].color = new Color(1, .85f, .3f, 1);
            if (Progress >= 1)
            {
                Score++; Lines++; Burst(point, new Color(1, .9f, .4f)); Cue("Garis selesai!", 1);
                NewLine();
            }
        }
    }

    // 3. Avoid the Rock: rocks roll toward one or two of three lanes; step your body left or right into a
    // free lane. The body position is the centre of the camera motion.
    public sealed class AvoidRockGame : MotionGame
    {
        public static readonly float[] Lanes = { .22f, .5f, .78f };
        protected override string Instruction => "Geser badanmu ke kiri atau kanan, hindari batunya!";
        // This game follows the whole body.
        protected override bool HandsAndFeetOnly => false;
        public float Body { get; private set; } = .5f;
        public int BodyLane => Body < .36f ? 0 : Body > .64f ? 2 : 1;
        public int Hits { get; private set; }
        sealed class Rock { public Image image; public int lane; public float age; }
        readonly List<Rock> rocks = new List<Rock>();
        Image player;
        readonly Image[] laneMarks = new Image[3];
        float wait;
        int wave;
        public const float Travel = 2.6f;
        protected override void Setup()
        {
            rocks.Clear(); Body = .5f; Hits = 0; wave = 0; wait = .8f;
            // Lane pads at the child's end of the path: red where a rock is coming, green where it is safe.
            for (int lane = 0; lane < 3; lane++)
            {
                var pad = UiKit.Panel("Lane " + lane, area, new Color(.4f, 1, .5f, .2f)); pad.GetComponent<Image>().raycastTarget = false;
                pad.anchorMin = new Vector2(Lanes[lane] - .13f, .03f); pad.anchorMax = new Vector2(Lanes[lane] + .13f, .25f); pad.offsetMin = pad.offsetMax = Vector2.zero;
                laneMarks[lane] = pad.GetComponent<Image>();
            }
            player = Sprite("Player", "star", new Vector2(.5f, .14f), .12f); player.color = new Color(.6f, 1f, .7f);
        }
        protected override void Tick(float dt)
        {
            var centre = Sensor.Centroid();
            if (centre.HasValue) Body = Mathf.Lerp(Body, centre.Value.x, 1 - Mathf.Exp(-dt * 6));
            Place(player.rectTransform, new Vector2(Lanes[BodyLane], .14f), .12f);
            for (int lane = 0; lane < 3; lane++)
            {
                bool danger = rocks.Exists(r => r.lane == lane && r.age > Travel * .25f);
                laneMarks[lane].color = danger ? new Color(1, .25f, .2f, .28f + .12f * Mathf.Sin(Time.unscaledTime * 10)) : new Color(.4f, 1, .5f, lane == BodyLane ? .3f : .15f);
            }
            if ((wait -= dt) <= 0)
            {
                // Early waves send one rock; later ones two, always leaving a free lane.
                int free = UnityEngine.Random.Range(0, 3);
                for (int lane = 0; lane < 3; lane++)
                {
                    bool send = wave < 4 ? lane == (free + 1) % 3 : lane != free;
                    if (!send) continue;
                    var image = Sprite("Rock", "rock", new Vector2(Lanes[lane], .75f), .05f);
                    rocks.Add(new Rock { image = image, lane = lane });
                }
                wave++; wait = Mathf.Max(1.6f, 3f - wave * .08f);
            }
            for (int i = rocks.Count - 1; i >= 0; i--)
            {
                var rock = rocks[i]; rock.age += dt;
                float t = rock.age / Travel;
                // Rolls down the path toward the child, growing as it comes near.
                Place(rock.image.rectTransform, new Vector2(Mathf.Lerp(.5f, Lanes[rock.lane], t), Mathf.Lerp(.72f, .14f, t)), Mathf.Lerp(.05f, .2f, t));
                rock.image.rectTransform.localRotation = Quaternion.Euler(0, 0, -rock.age * 160);
                if (t < 1) continue;
                if (rock.lane == BodyLane) { Hits++; Cue("Awas!", .8f); }
                else { Score++; Burst(new Vector2(Lanes[BodyLane], .2f), new Color(.7f, 1, .7f)); Cue("Hebat!", .6f); }
                Destroy(rock.image.gameObject); rocks.RemoveAt(i);
            }
        }
        public int RockLane => rocks.Count > 0 ? rocks[0].lane : -1;
        public float NextArrival => rocks.Count > 0 ? Travel - rocks[0].age : -1;
    }
}
