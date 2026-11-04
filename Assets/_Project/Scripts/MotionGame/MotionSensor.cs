using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
namespace FamilyAdventure.Presentation
{
    // Camera motion sensing for the motion games. Unity's WebCamTexture gives the camera picture; each new
    // frame is shrunk to a small grey grid and compared with the previous one, so every cell holds how much
    // movement happened there recently (0..1). The grid is mirrored, like a mirror: moving your right hand
    // lights up the right side of the screen. Coordinates are normalized (u to the right, v up).
    // Without a camera (or permission) the pointer counts as motion, so the games stay playable and testable.
    // With a pose model (PoseTracker) the child's hands and feet are found directly; Energy then answers
    // "is a hand or foot at this point" and Centroid is the hips. Motion differencing stays as the fallback.
    public sealed class MotionSensor : MonoBehaviour
    {
        // Model used by new sensors (null = camera motion only). Chosen on the mission menu.
        public static PoseModelKind? PreferredModel = PoseModelKind.BlazePose;
        public PoseModelKind? Model { get; private set; }
        public readonly PoseResult Pose = new PoseResult();
        public float PoseSeenAt = -10;
        public bool PoseActive => Model.HasValue && Time.unscaledTime - PoseSeenAt < .5f;
        public const float PoseInterval = .08f;
        PoseTracker tracker; float nextPose;
        public const int Width = 40, Height = 30;
        const float Noise = .06f, Gain = 6f, Decay = 3.5f;
        WebCamTexture camera;
        Color32[] pixels;
        readonly float[] previous = new float[Width * Height], motion = new float[Width * Height];
        bool primed;
        public bool HasCamera => camera != null && camera.isPlaying && camera.width > 16;
        public WebCamTexture Preview => HasCamera ? camera : null;
        public bool UsePointer = true;
        // Tests turn the real camera off so room movement cannot interfere.
        public static bool AllowCamera = true;
        // Motion inside this area (the body and head of the calibrated standing child) is ignored by Energy,
        // so only hands and feet reaching out count. Empty = nothing ignored.
        public Rect Ignore;
        IEnumerator Start()
        {
            if (!AllowCamera || WebCamTexture.devices.Length == 0) yield break;
            yield return MobilePermissions.RequestCamera();
            if (!MobilePermissions.HasCamera || this == null) yield break;
            var devices = WebCamTexture.devices; var device = devices[0];
            foreach (var d in devices) if (d.isFrontFacing) { device = d; break; }
            camera = new WebCamTexture(device.name, 320, 240, 30);
            camera.Play();
            UseModel(PreferredModel);
        }
        public void UseModel(PoseModelKind? kind)
        {
            tracker?.Dispose(); tracker = null; Model = null;
            if (!kind.HasValue) return;
            try { tracker = PoseTracker.Create(kind.Value); Model = kind; }
            catch (System.Exception e) { Debug.LogWarning("Pose model unavailable, using camera motion: " + e.Message); }
        }
        void Update()
        {
            float fade = Mathf.Exp(-Decay * Time.unscaledDeltaTime);
            for (int i = 0; i < motion.Length; i++) motion[i] *= fade;
            if (HasCamera && camera.didUpdateThisFrame) Sample();
            if (UsePointer) Pointer();
        }
        void Sample()
        {
            int w = camera.width, h = camera.height;
            if (pixels == null || pixels.Length != w * h) { pixels = new Color32[w * h]; primed = false; }
            camera.GetPixels32(pixels);
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    // Average a few pixels per cell; mirror horizontally.
                    int px = (x * w) / Width, py = (y * h) / Height, sx = Mathf.Max(1, w / Width / 2), sy = Mathf.Max(1, h / Height / 2);
                    float sum = 0;
                    for (int oy = 0; oy < 2; oy++) for (int ox = 0; ox < 2; ox++)
                    {
                        var c = pixels[Mathf.Min(h - 1, py + oy * sy) * w + Mathf.Min(w - 1, px + ox * sx)];
                        sum += (c.r * .3f + c.g * .59f + c.b * .11f) / 255f;
                    }
                    float grey = sum / 4; int cell = y * Width + (Width - 1 - x);
                    if (primed) motion[cell] = Mathf.Max(motion[cell], Mathf.Clamp01((Mathf.Abs(grey - previous[cell]) - Noise) * Gain));
                    previous[cell] = grey;
                }
            primed = true;
            if (tracker != null && Time.unscaledTime >= nextPose)
            {
                nextPose = Time.unscaledTime + PoseInterval;
                try { tracker.Run(pixels, w, h, Pose); if (Pose.found) PoseSeenAt = Time.unscaledTime; }
                catch (System.Exception e) { Debug.LogWarning("Pose model failed, using camera motion: " + e.Message); UseModel(null); }
            }
        }
        // Hand or foot within reach of a point (pose model). The reach is generous for small children.
        public float Reach(Vector2 uv, float radius)
        {
            float best = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!Pose.seen[i]) continue;
                var d = Pose.limbs[i] - uv; d.y *= (float)Height / Width;
                float r = radius * 1.6f; best = Mathf.Max(best, Mathf.Clamp01(1.5f - d.magnitude / r));
            }
            return best;
        }
        void Pointer()
        {
            Vector2 position; bool moving;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) { position = touch.primaryTouch.position.ReadValue(); moving = true; }
            else
            {
                var mouse = Mouse.current; if (mouse == null) return;
                position = mouse.position.ReadValue(); moving = mouse.delta.ReadValue().sqrMagnitude > .5f || mouse.leftButton.isPressed;
            }
            if (moving && Screen.width > 0) Inject(new Vector2(position.x / Screen.width, position.y / Screen.height));
        }
        // Adds motion around a point (used by the pointer fallback and by tests).
        public void Inject(Vector2 uv, float amount = 1, float radius = .06f)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    float du = (x + .5f) / Width - uv.x, dv = ((y + .5f) / Height - uv.y) * Height / Width;
                    if (du * du + dv * dv <= radius * radius) motion[y * Width + x] = Mathf.Max(motion[y * Width + x], amount);
                }
        }
        // Strongest motion within a radius of a point.
        public float Energy(Vector2 uv, float radius)
        {
            if (PoseActive) return Reach(uv, radius);
            float best = 0;
            int x0 = Mathf.Max(0, (int)((uv.x - radius) * Width)), x1 = Mathf.Min(Width - 1, (int)((uv.x + radius) * Width));
            int y0 = Mathf.Max(0, (int)((uv.y - radius * 1.4f) * Height)), y1 = Mathf.Min(Height - 1, (int)((uv.y + radius * 1.4f) * Height));
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                if (Ignore.width <= 0 || !Ignore.Contains(new Vector2((x + .5f) / Width, (y + .5f) / Height))) best = Mathf.Max(best, motion[y * Width + x]);
            return best;
        }
        // Where the body is moving (weighted centre of motion); null when almost nothing moves.
        public Vector2? Centroid(float minimum = .8f)
        {
            if (PoseActive) return Pose.hips;
            float total = 0, su = 0, sv = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    float m = motion[y * Width + x]; if (m < .2f) continue;
                    total += m; su += m * (x + .5f) / Width; sv += m * (y + .5f) / Height;
                }
            if (total < minimum) return null;
            return new Vector2(su / total, sv / total);
        }
        // Total recent motion over the whole view (used to check the child is standing still).
        public float Total() { float sum = 0; foreach (var m in motion) sum += m; return sum; }
        // Total motion inside an area.
        public float Total(Rect area)
        {
            float sum = 0;
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
                if (area.Contains(new Vector2((x + .5f) / Width, (y + .5f) / Height))) sum += motion[y * Width + x];
            return sum;
        }
        public void Clear() { System.Array.Clear(motion, 0, motion.Length); }
        void OnDestroy() { tracker?.Dispose(); if (camera != null) { camera.Stop(); Destroy(camera); } }
    }
}
