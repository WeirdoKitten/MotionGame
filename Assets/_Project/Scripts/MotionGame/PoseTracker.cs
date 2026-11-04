using System;
using System.Diagnostics;
using System.Globalization;
using Unity.InferenceEngine;
using UnityEngine;
namespace FamilyAdventure.Presentation
{
    public enum PoseModelKind { BlazePose, MoveNet }

    // Hands and feet found by a pose model, in mirrored normalized screen space (u right, v up), like the
    // motion grid. Left/right are the child's own sides.
    public sealed class PoseResult
    {
        public const int LeftHand = 0, RightHand = 1, LeftFoot = 2, RightFoot = 3;
        public readonly Vector2[] limbs = new Vector2[4];
        public readonly bool[] seen = new bool[4];
        public Vector2 hips;
        public bool found;
        public float milliseconds;
        public void Clear() { found = false; for (int i = 0; i < 4; i++) seen[i] = false; }
    }

    // Runs a pose model (Unity Inference Engine) on a camera frame. Frames are prepared on the CPU (the
    // pictures are small), models run on the CPU backend so every device works.
    // BlazePose (Google MediaPipe, via unity/inference-engine-blaze-pose): person detector + 33-point
    // landmarker (hands include finger tips, feet include toes). MoveNet Lightning (Google, via
    // Xenova/movenet-singlepose-lightning): one 17-point network (wrists and ankles only), faster.
    public abstract class PoseTracker : IDisposable
    {
        const string Folder = "Motion/Pose/";
        public abstract PoseModelKind Kind { get; }
        public static PoseTracker Create(PoseModelKind kind) => kind == PoseModelKind.MoveNet ? new MoveNetTracker() : new BlazePoseTracker();
        public abstract void Run(Color32[] pixels, int width, int height, PoseResult result);
        public abstract void Dispose();
        protected static Model Load(string name)
        {
            var asset = Resources.Load<ModelAsset>(Folder + name);
            if (asset == null) throw new InvalidOperationException("Missing pose model " + name);
            return ModelLoader.Load(asset);
        }
        protected static TextAsset Text(string name) => Resources.Load<TextAsset>(Folder + name);

        // 2x3 affine map from tensor (column, row) to image pixels (x right, y up from the bottom row).
        protected struct Affine
        {
            public float a, b, c, d, e, f; // x = a*col + b*row + c ; y = d*col + e*row + f
            public Vector2 Map(float col, float row) => new Vector2(a * col + b * row + c, d * col + e * row + f);
            public static Affine Square(int width, int height, int size)
            {
                // Centred square covering the longer side; row 0 is the top of the picture.
                float side = Mathf.Max(width, height), scale = side / size;
                return new Affine { a = scale, b = 0, c = (width - side) * .5f, d = 0, e = -scale, f = (height + side) * .5f };
            }
        }
        // Bilinear sample into an NHWC buffer with values 0..1 (or 0..255); outside the picture is black.
        protected static void Sample(Color32[] pixels, int width, int height, float[] target, int size, Affine m, float scale)
        {
            for (int row = 0; row < size; row++)
                for (int col = 0; col < size; col++)
                {
                    var p = m.Map(col, row); int o = (row * size + col) * 3;
                    if (p.x < 0 || p.y < 0 || p.x > width - 1 || p.y > height - 1) { target[o] = target[o + 1] = target[o + 2] = 0; continue; }
                    int x0 = (int)p.x, y0 = (int)p.y, x1 = Mathf.Min(x0 + 1, width - 1), y1 = Mathf.Min(y0 + 1, height - 1);
                    float fx = p.x - x0, fy = p.y - y0;
                    var c00 = pixels[y0 * width + x0]; var c10 = pixels[y0 * width + x1]; var c01 = pixels[y1 * width + x0]; var c11 = pixels[y1 * width + x1];
                    float w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
                    target[o] = (c00.r * w00 + c10.r * w10 + c01.r * w01 + c11.r * w11) * scale;
                    target[o + 1] = (c00.g * w00 + c10.g * w10 + c01.g * w01 + c11.g * w11) * scale;
                    target[o + 2] = (c00.b * w00 + c10.b * w10 + c01.b * w01 + c11.b * w11) * scale;
                }
        }
        // Image pixel -> mirrored screen point.
        protected static Vector2 Screen(Vector2 pixel, int width, int height) => new Vector2(1 - pixel.x / width, pixel.y / height);
    }

    sealed class BlazePoseTracker : PoseTracker
    {
        const int DetectorSize = 224, LandmarkSize = 256, Anchors = 2254;
        // Landmark indices (MediaPipe pose): index fingers, foot tips and hips.
        const int LeftIndex = 19, RightIndex = 20, LeftFootTip = 31, RightFootTip = 32, LeftHip = 23, RightHip = 24;
        readonly Worker detector, landmarker;
        readonly float[,] anchors = new float[Anchors, 2];
        readonly float[] detectorInput = new float[DetectorSize * DetectorSize * 3], landmarkInput = new float[LandmarkSize * LandmarkSize * 3];
        readonly string landmarkOutput;
        public override PoseModelKind Kind => PoseModelKind.BlazePose;
        public BlazePoseTracker()
        {
            detector = new Worker(Load("pose_detection"), BackendType.CPU);
            var landmarks = Load("pose_landmarks_lite");
            landmarkOutput = landmarks.outputs.Exists(o => o.name == "Identity") ? "Identity" : landmarks.outputs[0].name;
            landmarker = new Worker(landmarks, BackendType.CPU);
            var lines = Text("anchors").text.Split('\n');
            for (int i = 0; i < Anchors; i++)
            {
                var v = lines[i].Split(',');
                anchors[i, 0] = float.Parse(v[0], CultureInfo.InvariantCulture); anchors[i, 1] = float.Parse(v[1], CultureInfo.InvariantCulture);
            }
        }
        public override void Run(Color32[] pixels, int width, int height, PoseResult result)
        {
            var clock = Stopwatch.StartNew(); result.Clear();
            var m = Affine.Square(width, height, DetectorSize);
            Sample(pixels, width, height, detectorInput, DetectorSize, m, 1 / 255f);
            using (var input = new Tensor<float>(new TensorShape(1, DetectorSize, DetectorSize, 3), detectorInput))
                detector.Schedule(input);
            using var boxes = (detector.PeekOutput(0) as Tensor<float>).ReadbackAndClone();
            using var scores = (detector.PeekOutput(1) as Tensor<float>).ReadbackAndClone();
            int best = 0; float top = float.MinValue;
            for (int i = 0; i < Anchors; i++) if (scores[i] > top) { top = scores[i]; best = i; }
            float score = 1 / (1 + Mathf.Exp(-Mathf.Clamp(top, -100, 100)));
            if (score >= .5f)
            {
                // Two detector keypoints (hip centre, above the head) give the body centre, size and tilt.
                int row = best * boxes.shape[2];
                float ax = DetectorSize * anchors[best, 0], ay = DetectorSize * anchors[best, 1];
                var hip = m.Map(ax + boxes[row + 4], ay + boxes[row + 5]);
                var top2 = m.Map(ax + boxes[row + 6], ay + boxes[row + 7]);
                var delta = top2 - hip; float radius = 1.25f * delta.magnitude, theta = Mathf.Atan2(delta.y, delta.x);
                float s = radius / (.5f * LandmarkSize), rot = .5f * Mathf.PI - theta, cos = Mathf.Cos(rot), sin = Mathf.Sin(rot);
                // M2 = T(hip) * S(s,-s) * R(rot) * T(-half)
                float h = .5f * LandmarkSize;
                var m2 = new Affine { a = s * cos, b = -s * sin, d = -s * sin, e = -s * cos };
                m2.c = hip.x - (m2.a * h + m2.b * h); m2.f = hip.y - (m2.d * h + m2.e * h);
                Sample(pixels, width, height, landmarkInput, LandmarkSize, m2, 1 / 255f);
                using (var input = new Tensor<float>(new TensorShape(1, LandmarkSize, LandmarkSize, 3), landmarkInput))
                    landmarker.Schedule(input);
                using var points = (landmarker.PeekOutput(landmarkOutput) as Tensor<float>).ReadbackAndClone();
                Vector2 Point(int k) => Screen(m2.Map(points[5 * k], points[5 * k + 1]), width, height);
                bool Visible(int k) => points[5 * k + 3] > .5f && points[5 * k + 4] > .5f;
                int[] limbs = { LeftIndex, RightIndex, LeftFootTip, RightFootTip };
                for (int i = 0; i < 4; i++) { result.limbs[i] = Point(limbs[i]); result.seen[i] = Visible(limbs[i]); }
                result.hips = (Point(LeftHip) + Point(RightHip)) * .5f; result.found = true;
            }
            result.milliseconds = (float)clock.Elapsed.TotalMilliseconds;
        }
        public override void Dispose() { detector.Dispose(); landmarker.Dispose(); }
    }

    sealed class MoveNetTracker : PoseTracker
    {
        const int Size = 192;
        // Keypoint indices (COCO order): wrists, ankles, hips.
        const int LeftWrist = 9, RightWrist = 10, LeftAnkle = 15, RightAnkle = 16, LeftHip = 11, RightHip = 12;
        readonly Worker worker;
        readonly bool integer;
        readonly float[] input = new float[Size * Size * 3];
        readonly int[] inputInt = new int[Size * Size * 3];
        public override PoseModelKind Kind => PoseModelKind.MoveNet;
        public MoveNetTracker()
        {
            var model = Load("movenet_lightning");
            integer = model.inputs[0].dataType == DataType.Int;
            worker = new Worker(model, BackendType.CPU);
        }
        public override void Run(Color32[] pixels, int width, int height, PoseResult result)
        {
            var clock = Stopwatch.StartNew(); result.Clear();
            var m = Affine.Square(width, height, Size);
            Sample(pixels, width, height, input, Size, m, 1);
            var shape = new TensorShape(1, Size, Size, 3);
            if (integer)
            {
                for (int i = 0; i < input.Length; i++) inputInt[i] = (int)input[i];
                using var t = new Tensor<int>(shape, inputInt); worker.Schedule(t);
            }
            else { using var t = new Tensor<float>(shape, input); worker.Schedule(t); }
            using var output = (worker.PeekOutput(0) as Tensor<float>).ReadbackAndClone(); // (1,1,17,3): y, x, score
            Vector2 Point(int k) => Screen(m.Map(output[k * 3 + 1] * Size, output[k * 3] * Size), width, height);
            bool Visible(int k) => output[k * 3 + 2] > .3f;
            int[] limbs = { LeftWrist, RightWrist, LeftAnkle, RightAnkle };
            for (int i = 0; i < 4; i++) { result.limbs[i] = Point(limbs[i]); result.seen[i] = Visible(limbs[i]); }
            result.found = Visible(LeftHip) || Visible(RightHip) || result.seen[0] || result.seen[1];
            result.hips = (Point(LeftHip) + Point(RightHip)) * .5f;
            result.milliseconds = (float)clock.Elapsed.TotalMilliseconds;
        }
        public override void Dispose() { worker.Dispose(); }
    }
}
