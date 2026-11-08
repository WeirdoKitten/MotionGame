using System;
using System.Collections;
using System.Linq;
using FamilyAdventure.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
namespace FamilyAdventure.PlayTests
{
    // Pick the Fruit driven by injected poses (as if a pose model saw the child) and, without a pose model,
    // by injected motion. No camera is used.
    public sealed class PickFruitPlayTests
    {
        GameObject events;
        MotionMissionMenu menu;

        PickFruitGame Open()
        {
            LogAssert.ignoreFailingMessages = true; MotionSensor.AllowCamera = false; MotionGame.ShowDebug = false;
            events = new GameObject("Events", typeof(EventSystem));
            menu = MotionMissionMenu.Open(null);
            Assert.That(menu.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b => b.name.StartsWith("Mission ")), Is.EqualTo(MotionMissionMenu.Titles.Length));
            menu.Play(3); var game = (PickFruitGame)menu.Current; game.Sensor.UsePointer = false;
            return game;
        }
        // Feeds a pose every frame for some seconds; t runs from 0 to 1.
        // each runs after every frame (to catch short cues).
        static IEnumerator Poses(PickFruitGame game, float seconds, Func<float, PoseResult> pose, Action each = null)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) { game.Sensor.InjectPose(pose(t / seconds)); yield return null; each?.Invoke(); }
        }
        static IEnumerator Squat(PickFruitGame game, float depth, Action each = null)
        {
            yield return Poses(game, 1, t => FakePose.Body(depth * t), each);
            yield return Poses(game, .4f, _ => FakePose.Body(depth), each);
            yield return Poses(game, 1, t => FakePose.Body(depth * (1 - t)), each);
            yield return Poses(game, .3f, _ => FakePose.Body(), each);
        }
        static IEnumerator UntilFruit(PickFruitGame game)
        {
            for (float t = 0; t < 6 && !game.FruitReady; t += Time.unscaledDeltaTime) { game.Sensor.InjectPose(FakePose.Body()); yield return null; }
            Assert.That(game.FruitReady, Is.True, "A fruit appears while the child stands");
        }

        [UnityTest] public IEnumerator SquatsFromInjectedPosesPickFruit()
        {
            var game = Open();
            yield return null; yield return MotionGamesPlayTests.Shot("motion-fruit-calibration.png");
            // A standing child is seen at once; five still seconds and the countdown follow.
            for (float t = 0; t < 12 && game.State != MotionGame.Phase.Play; t += Time.unscaledDeltaTime) { game.Sensor.InjectPose(FakePose.Body()); yield return null; }
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Play)); Assert.That(game.UsingPose, Is.True);
            Assert.That(game.Squat.HasBaseline, Is.True); Assert.That(game.Squat.LegLength, Is.EqualTo(FakePose.Leg).Within(.01f));
            // The research overlay is off unless switched on.
            Assert.That(game.DebugShown, Is.Null);

            // A full squat picks the fruit and standing up puts it in the basket: exactly one star.
            yield return UntilFruit(game);
            yield return Poses(game, 1, t => FakePose.Body(.45f * t));
            Assert.That(game.Holding, Is.True); Assert.That(game.Score, Is.Zero);
            yield return MotionGamesPlayTests.Shot("motion-fruit.png");
            yield return Poses(game, .4f, _ => FakePose.Body(.45f));
            yield return Poses(game, 1, t => FakePose.Body(.45f * (1 - t)));
            yield return Poses(game, .3f, _ => FakePose.Body());
            Assert.That(game.Score, Is.EqualTo(1)); Assert.That(game.Squat.Count, Is.EqualTo(1));

            // Too shallow: no star, only a light cue.
            yield return UntilFruit(game);
            bool cued = false;
            yield return Squat(game, .18f, () => cued |= game.CueText == "Turun sedikit lagi!");
            Assert.That(game.Score, Is.EqualTo(1)); Assert.That(game.Squat.LastMiss, Is.EqualTo("kurang dalam")); Assert.That(cued, Is.True);
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Play));

            // Bending over instead of squatting: no star.
            yield return UntilFruit(game);
            yield return Poses(game, 1, t => FakePose.Body(.03f * t, bend: .7f * t));
            yield return Poses(game, 1, t => FakePose.Body(.03f * (1 - t), bend: .7f * (1 - t)));
            Assert.That(game.Score, Is.EqualTo(1));

            // The body is missed for a moment in the middle of a squat: it still counts.
            yield return UntilFruit(game);
            yield return Poses(game, 1, t => FakePose.Body(.45f * t));
            yield return Poses(game, .3f, _ => FakePose.Missing());
            yield return Poses(game, 1, t => FakePose.Body(.45f * (1 - t)));
            yield return Poses(game, .3f, _ => FakePose.Body());
            Assert.That(game.Score, Is.EqualTo(2));

            // Research overlay on: detector values are shown.
            MotionGame.ShowDebug = true; yield return null; yield return null;
            Assert.That(game.DebugShown, Does.Contain("kedalaman")); Assert.That(game.DebugShown, Does.Contain("jumlah 2"));
            yield return MotionGamesPlayTests.Shot("motion-fruit-debug.png");
            MotionGame.ShowDebug = false; yield return null;
            Assert.That(game.DebugShown, Is.Null);

            game.EndNow(); yield return null;
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Done));
            game.Quit(); yield return null;
            Assert.That(menu.Current, Is.Null);
        }

        [UnityTest] public IEnumerator WithoutPoseModelMotionStillPlays()
        {
            var game = Open();
            yield return null;
            Assert.That(game.UsingPose, Is.False, "No camera, so no pose model");
            // Calibrate like the other games: step in, then stand still.
            game.Sensor.Inject(MotionGame.BodyZone.center, 1, .08f); yield return null;
            Assert.That(game.Present, Is.True);
            for (float t = 0; t < 12 && game.State != MotionGame.Phase.Play; t += Time.unscaledDeltaTime) yield return null;
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Play));
            for (float t = 0; t < 4 && !game.FruitReady; t += Time.unscaledDeltaTime) yield return null;
            Assert.That(game.FruitReady, Is.True);
            // Motion at head height alone does nothing.
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime) { game.Sensor.Inject(PickFruitGame.HeadPoint); yield return null; }
            Assert.That(game.Score, Is.Zero); Assert.That(game.Holding, Is.False);
            // Touch the fruit low down, then move at head height: one star.
            for (float t = 0; t < .3f; t += Time.unscaledDeltaTime) { game.Sensor.Inject(game.FruitPoint); yield return null; }
            Assert.That(game.Holding, Is.True);
            for (float t = 0; t < 1.5f && game.Score == 0; t += Time.unscaledDeltaTime) { game.Sensor.Inject(PickFruitGame.HeadPoint); yield return null; }
            Assert.That(game.Score, Is.EqualTo(1));
            MotionGame.ShowDebug = true; yield return null;
            Assert.That(game.DebugShown, Does.Contain("cadangan"));
            MotionGame.ShowDebug = false;
            game.Quit(); yield return null;
        }

        [UnityTearDown] public IEnumerator Clean()
        {
            MotionSensor.AllowCamera = true; MotionGame.ShowDebug = false;
            if (menu != null) UnityEngine.Object.Destroy(menu.gameObject);
            if (events != null) UnityEngine.Object.Destroy(events);
            yield return null;
        }
    }
}
