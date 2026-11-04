using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using FamilyAdventure.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace FamilyAdventure.PlayTests
{
    // Compares the two pose models on the same photo (a handstand from the BlazePose sample: hands low,
    // feet high) and writes timings and positions to Builds/pose-model-comparison.md.
    public sealed class PoseModelTests
    {
        [UnityTest] public IEnumerator BlazePoseAndMoveNetFindHandsAndFeet()
        {
            var photo = new Texture2D(2, 2); photo.LoadImage(File.ReadAllBytes("Assets/_Project/Art/MotionGamesSource/pose-test.jpg"));
            // Camera-sized frame like a webcam gives.
            var frame = new RenderTexture(640, 360, 0); Graphics.Blit(photo, frame);
            var small = new Texture2D(640, 360, TextureFormat.RGBA32, false); RenderTexture.active = frame; small.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); small.Apply(); RenderTexture.active = null;
            var pixels = small.GetPixels32();
            var report = new StringBuilder("# Pose model comparison\n\nPhoto: BlazePose sample (handstand), 640x360, CPU backend. Points are mirrored screen UV.\n\n| Model | first run ms | avg ms (10 runs) | left hand | right hand | left foot | right foot |\n|---|---|---|---|---|---|---|\n");
            foreach (var kind in new[] { PoseModelKind.BlazePose, PoseModelKind.MoveNet })
            {
                using var tracker = PoseTracker.Create(kind);
                var result = new PoseResult();
                tracker.Run(pixels, 640, 360, result); float first = result.milliseconds;
                float total = 0;
                for (int i = 0; i < 10; i++) { tracker.Run(pixels, 640, 360, result); total += result.milliseconds; yield return null; }
                string P(int i) => result.seen[i] ? $"{result.limbs[i].x:0.00},{result.limbs[i].y:0.00}" : "-";
                report.Append($"| {kind} | {first:0} | {total / 10:0} | {P(0)} | {P(1)} | {P(2)} | {P(3)} |\n");
                Debug.Log($"POSE {kind} found {result.found} {first:0}ms avg {total / 10:0}ms hands {P(0)} {P(1)} feet {P(2)} {P(3)}");
                Assert.That(result.found, Is.True, kind + " finds the person");
                // MoveNet Lightning is known to be weak on upside-down poses; this photo only proves it runs.
                if (kind == PoseModelKind.MoveNet) continue;
                // Upside down: the hands are near the ground, the feet high up.
                var hands = new[] { 0, 1 }.Where(i => result.seen[i]).Select(i => result.limbs[i].y).ToArray();
                var feet = new[] { 2, 3 }.Where(i => result.seen[i]).Select(i => result.limbs[i].y).ToArray();
                Assert.That(hands, Is.Not.Empty, kind + " sees a hand"); Assert.That(feet, Is.Not.Empty, kind + " sees a foot");
                Assert.That(hands.Average(), Is.LessThan(feet.Average()), kind + " hands below feet");
            }
            Directory.CreateDirectory("Builds"); File.WriteAllText("Builds/pose-model-comparison.md", report.ToString());
            Object.Destroy(photo); Object.Destroy(small); frame.Release();
        }
    }
}
