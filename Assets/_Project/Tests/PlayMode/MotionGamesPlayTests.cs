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
        static IEnumerator Shot(string name)
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
            Assert.That(menu.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b => b.name.StartsWith("Mission ")), Is.EqualTo(4));

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
        // 4. Jump and Duck: jumping clears logs, ducking clears hanging rocks; standing still or the wrong move
        // is only a miss. Moves are simulated (keys/clicks path), since the test runs without a camera.
        [UnityTest] public IEnumerator JumpDuckGame_RespondsToJumpAndDuck()
        {
            LogAssert.ignoreFailingMessages = true; MotionSensor.AllowCamera = false;
            events = new GameObject("Events", typeof(EventSystem));
            menu = MotionMissionMenu.Open(null); yield return null;
            menu.Play(3); var game = (JumpDuckGame)menu.Current;
            yield return Started(game);
            Assert.That(game.Sensor.Ignore, Is.EqualTo(default(Rect)), "Whole body counts");

            // Standing still while the first log (always low) rolls in: a miss, not a game over.
            Assert.That(game.NextObstacle, Is.EqualTo(-1));
            for (float t = 0; t < 6 && game.NextObstacle < 0; t += Time.unscaledDeltaTime) yield return null;
            Assert.That(game.NextObstacle, Is.EqualTo(0), "Wave 1 is a low obstacle");
            for (float t = 0; t < 6 && game.Misses == 0; t += Time.unscaledDeltaTime) yield return null;
            Assert.That(game.Misses, Is.EqualTo(1)); Assert.That(game.Score, Is.Zero); Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Play));

            // The right move just before each obstacle arrives clears it: duck under the rock, jump over the log.
            int lows = 0, highs = 0, acted = -1; bool shot = false;
            for (float t = 0; t < 15 && (lows == 0 || highs == 0 || game.Cleared < 3); t += Time.unscaledDeltaTime)
            {
                int kind = game.NextObstacle, cleared = game.Cleared;
                if (!shot && kind == 1 && game.NextArrival < 1) { shot = true; yield return Shot("motion-jump.png"); }
                if (kind >= 0 && game.NextArrival < .15f && acted != cleared + game.Misses)
                {
                    acted = cleared + game.Misses;
                    if (kind == 0) game.SimulateJump(); else game.SimulateDuck();
                }
                yield return null;
                if (game.Cleared > cleared) { if (kind == 0) lows++; else highs++; }
            }
            Assert.That(game.Misses, Is.EqualTo(1)); Assert.That(lows, Is.GreaterThan(0)); Assert.That(highs, Is.GreaterThan(0));
            Assert.That(game.Score, Is.EqualTo(game.Cleared));

            // The wrong move is a miss: jumping at everything clears logs but not a hanging rock.
            int missedKind = -1; acted = -1;
            for (float t = 0; t < 10 && game.Misses == 1; t += Time.unscaledDeltaTime)
            {
                int kind = game.NextObstacle, key = game.Cleared + game.Misses;
                if (kind >= 0 && game.NextArrival < .15f && acted != key) { acted = key; game.SimulateJump(); }
                yield return null;
                if (game.Misses > 1) missedKind = kind;
            }
            Assert.That(game.Misses, Is.EqualTo(2)); Assert.That(missedKind, Is.EqualTo(1), "Jumping under a rock is the miss");
            game.EndNow(); yield return null;
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Done));
            game.Quit(); yield return null;
            Assert.That(menu.Current, Is.Null);
        }

        // Kinematics without the game: jitter, the pre-jump dip and tiptoes are not moves; a jump with the feet
        // off the floor and a held squat are.
        [Test] public void JumpDuckBody_RecognisesJumpAndDuck()
        {
            var body = new JumpDuckBody(); body.Calibrate(.5f, .18f);
            Assert.That(body.Length, Is.EqualTo(.32f).Within(1e-5f));
            float time = 0;
            var seen = new System.Collections.Generic.List<JumpDuckState>();
            void Feed(float seconds, float rise, float knee, float lift, float jitter = 0)
            {
                for (float e = 0; e < seconds; e += 1 / 30f)
                {
                    time += 1 / 30f;
                    seen.Add(body.Step(body.Baseline + rise * body.Length + jitter * Mathf.Sin(time * 47), knee, body.Floor + lift * body.Length, time));
                }
            }
            // Standing with landmark jitter (about 2 px): nothing happens.
            Feed(1, 0, 175, 0, .0025f);
            Assert.That(seen, Is.All.EqualTo(JumpDuckState.Standing)); Assert.That(Mathf.Abs(body.Rise), Is.LessThan(.05f));
            // Rising onto tiptoes (hips up, ankles on the floor) is not a jump.
            seen.Clear(); Feed(.5f, .15f, 178, 0); Feed(.5f, 0, 175, 0);
            Assert.That(seen, Has.No.Member(JumpDuckState.Airborne));
            // Pre-jump dip (.2 s squat), then the jump: airborne, never ducking; then landing and standing.
            seen.Clear(); Feed(.2f, -.25f, 120, 0); Feed(.35f, .3f, 175, .3f);
            Assert.That(seen, Has.No.Member(JumpDuckState.Ducking)); Assert.That(body.State, Is.EqualTo(JumpDuckState.Airborne)); Assert.That(body.Up, Is.True);
            Feed(.6f, 0, 175, 0);
            Assert.That(seen, Has.Member(JumpDuckState.Landing)); Assert.That(body.State, Is.EqualTo(JumpDuckState.Standing));
            // A held squat becomes a duck only after it is held; standing up recovers.
            seen.Clear(); Feed(.25f, -.3f, 105, 0);
            Assert.That(seen, Has.No.Member(JumpDuckState.Ducking));
            Feed(.6f, -.3f, 105, 0);
            Assert.That(body.State, Is.EqualTo(JumpDuckState.Ducking)); Assert.That(body.Down, Is.True);
            Feed(.8f, 0, 175, 0);
            Assert.That(seen, Has.Member(JumpDuckState.Recovered)); Assert.That(body.State, Is.EqualTo(JumpDuckState.Standing));
            // Hips down with straight knees (bending over) is not a squat.
            seen.Clear(); Feed(.8f, -.3f, 170, 0);
            Assert.That(seen, Has.No.Member(JumpDuckState.Ducking));
        }

        [Test] public void OneEuroFilter_SmoothsStillAndFollowsFast()
        {
            var filter = new OneEuroFilter(1, .007f, 1);
            float t = 0, value = 0;
            for (int i = 0; i < 60; i++) { t += 1 / 30f; value = filter.Filter(450 + (i % 2 == 0 ? 2 : -2), t); }
            Assert.That(Mathf.Abs(value - 450), Is.LessThan(1), "Jitter of 2 px is damped");
            for (int i = 0; i < 5; i++) { t += 1 / 30f; value = filter.Filter(550, t); }
            Assert.That(value, Is.GreaterThan(530), "A fast 100 px move is followed within 5 frames");
        }

        [UnityTearDown] public IEnumerator Clean() { MotionSensor.AllowCamera = true; if (menu != null) Object.Destroy(menu.gameObject); Object.Destroy(events); yield return null; }
    }
}
