using System.Collections;
using System.IO;
using System.Linq;
using FamilyAdventure.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
namespace FamilyAdventure.PlayTests
{
    public sealed class MotionGamesPlayTests
    {
        GameObject events;
        MotionMissionMenu menu;
        // Batch mode never reaches end of frame, so overlay canvases are drawn through a temporary camera.
        internal static IEnumerator Shot(string name)
        {
            yield return null;
            var cameraObject = new GameObject("Shot camera"); var shotCamera = cameraObject.AddComponent<Camera>();
            shotCamera.clearFlags = CameraClearFlags.SolidColor; shotCamera.backgroundColor = Color.black; shotCamera.cullingMask = 1 << 5;
            shotCamera.transform.position = new Vector3(0, 0, -5000);
            var target = new RenderTexture(1440, 900, 24); shotCamera.targetTexture = target;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy).OrderBy(c => c.sortingOrder).ToArray();
            foreach (var canvas in canvases)
            {
                foreach (var t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = shotCamera; canvas.planeDistance = 1 + (100 - canvas.sortingOrder) * .01f;
            }
            Canvas.ForceUpdateCanvases(); shotCamera.Render();
            RenderTexture.active = target; var texture = new Texture2D(1440, 900, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); texture.Apply(); RenderTexture.active = null;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
            Directory.CreateDirectory("Builds/Screenshots"); File.WriteAllBytes("Builds/Screenshots/" + name, texture.EncodeToPNG());
            Object.Destroy(texture); shotCamera.targetTexture = null; target.Release(); Object.Destroy(cameraObject);
        }
        static IEnumerator Started(MotionGame game)
        {
            game.Sensor.UsePointer = false;
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Calibrate));
            // An empty, still view never starts the game.
            for (float t = 0; t < 1.5f; t += Time.unscaledDeltaTime) yield return null;
            Assert.That(game.Present, Is.False); Assert.That(game.Calibration, Is.Zero); Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Calibrate));
            // The child steps in, then moving during calibration keeps restarting the five still seconds.
            game.Sensor.Inject(MotionGame.BodyZone.center, 1, .08f); yield return null;
            Assert.That(game.Present, Is.True);
            for (float t = 0; t < 2; t += Time.unscaledDeltaTime) { game.Sensor.Inject(new Vector2(.3f, .6f)); yield return null; }
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Calibrate)); Assert.That(game.Calibration, Is.LessThan(.5f));
            for (float t = 0; t < 12 && game.State != MotionGame.Phase.Play; t += Time.unscaledDeltaTime) yield return null;
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Play));
        }
        // In the full game the menu opens from the egg house; that part of the test stays in Family Adventure.
        [UnityTest] public IEnumerator MissionMenuOpensAndEachGameRespondsToMotion()
        {
            // A busy or missing webcam logs an error; the games fall back to pointer motion, which is what this test uses.
            LogAssert.ignoreFailingMessages = true; MotionSensor.AllowCamera = false;
            events = new GameObject("Events", typeof(EventSystem));

            menu = MotionMissionMenu.Open(null);
            yield return null; yield return Shot("motion-missions.png");
            Assert.That(menu.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b => b.name.StartsWith("Mission ")), Is.EqualTo(MotionMissionMenu.Titles.Length));
            Assert.That(MotionMissionMenu.Titles.Length, Is.EqualTo(7));

            // 1. Catch the Star: moving on the star catches it.
            menu.Play(0); var star = (StarCatchGame)menu.Current;
            yield return null; yield return Shot("motion-calibration.png");
            yield return Started(star);
            Assert.That(star.Sensor.Ignore, Is.EqualTo(MotionGame.BodyZone));
            // Body movement does not catch anything.
            for (float t = 0; t < 2; t += Time.unscaledDeltaTime) { star.Sensor.Inject(MotionGame.BodyZone.center, 1, .05f); yield return null; }
            Assert.That(star.Score, Is.Zero); Assert.That(MotionGame.BodyZone.Contains(star.StarPoint), Is.False);
            for (float t = 0; t < 4 && star.Score == 0; t += Time.unscaledDeltaTime)
            {
                if (star.Star != null) star.Sensor.Inject(star.StarPoint);
                yield return null;
            }
            Assert.That(star.Score, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(1.2f); yield return Shot("motion-star.png");
            star.EndNow(); yield return null;
            Assert.That(star.State, Is.EqualTo(MotionGame.Phase.Done));
            yield return Shot("motion-result.png");
            star.Quit(); yield return null;

            // 2. Follow the Line: moving on the leading star pulls it to the end of the line.
            menu.Play(1); var line = (FollowLineGame)menu.Current; yield return Started(line);
            Assert.That(line.Path.Length, Is.GreaterThan(10));
            float before = line.Progress; yield return new WaitForSecondsRealtime(.5f);
            Assert.That(line.Progress, Is.EqualTo(before), "No motion, no progress");
            bool shotLine = false;
            for (float t = 0; t < 8 && line.Lines == 0; t += Time.unscaledDeltaTime)
            {
                line.Sensor.Inject(line.Leader);
                if (!shotLine && t > 2) { shotLine = true; yield return Shot("motion-line.png"); }
                yield return null;
            }
            Assert.That(line.Lines, Is.EqualTo(1)); Assert.That(line.Score, Is.EqualTo(1));
            line.Quit(); yield return null;

            // 3. Avoid the Rock: standing in the rock's lane is a hit, stepping aside is a dodge.
            menu.Play(2); var rock = (AvoidRockGame)menu.Current; yield return Started(rock);
            bool shotRock = false;
            for (float t = 0; t < 12 && rock.Score < 2; t += Time.unscaledDeltaTime)
            {
                int free = rock.RockLane < 0 ? 1 : (rock.RockLane + 1) % 3;
                rock.Sensor.Inject(new Vector2(AvoidRockGame.Lanes[free], .5f), 1, .1f);
                if (!shotRock && rock.Score == 1 && rock.NextArrival > 0 && rock.NextArrival < 1.2f) { shotRock = true; yield return Shot("motion-rock.png"); }
                yield return null;
            }
            Assert.That(rock.Score, Is.GreaterThanOrEqualTo(2)); Assert.That(rock.Hits, Is.Zero);
            // Standing still in the middle while a rock comes down the middle lane is a hit, not a game over.
            for (float t = 0; t < 12 && rock.Hits == 0; t += Time.unscaledDeltaTime)
            {
                if (rock.RockLane >= 0) rock.Sensor.Inject(new Vector2(AvoidRockGame.Lanes[rock.RockLane], .5f), 1, .1f);
                yield return null;
            }
            Assert.That(rock.Hits, Is.GreaterThan(0)); Assert.That(rock.State, Is.EqualTo(MotionGame.Phase.Play));
            rock.Quit(); yield return null;
            Assert.That(menu.Current, Is.Null);
            menu.Close(); yield return null;
        }
        [UnityTearDown] public IEnumerator Clean() { MotionSensor.AllowCamera = true; if (menu != null) Object.Destroy(menu.gameObject); Object.Destroy(events); yield return null; }
    }
}
