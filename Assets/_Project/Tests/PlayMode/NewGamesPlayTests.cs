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
    // Spin the Windmill, Jump the Puddle and Find the Bird driven by injected poses (as if a pose model saw
    // the child) and, without a pose model, by injected motion. No camera is used.
    public sealed class NewGamesPlayTests
    {
        GameObject events;
        MotionMissionMenu menu;

        T Open<T>(int index) where T : MotionGame
        {
            LogAssert.ignoreFailingMessages = true; MotionSensor.AllowCamera = false; MotionGame.ShowDebug = false;
            events = new GameObject("Events", typeof(EventSystem));
            menu = MotionMissionMenu.Open(null);
            Assert.That(menu.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b => b.name.StartsWith("Mission ")), Is.EqualTo(7));
            menu.Play(index); var game = (T)menu.Current; game.Sensor.UsePointer = false;
            return game;
        }
        // Feeds a pose every frame for some seconds; t runs from 0 to 1. each runs after every frame (to catch
        // short cues).
        static IEnumerator Poses(MotionGame game, float seconds, Func<float, PoseResult> pose, Action each = null)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) { game.Sensor.InjectPose(pose(Mathf.Clamp01(t / seconds))); yield return null; each?.Invoke(); }
        }
        // Standing still through calibration and the countdown.
        static IEnumerator Calibrated(MotionGame game, Func<PoseResult> standing)
        {
            for (float t = 0; t < 12 && game.State != MotionGame.Phase.Play; t += Time.unscaledDeltaTime) { game.Sensor.InjectPose(standing()); yield return null; }
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Play));
        }
        static IEnumerator CalibratedByMotion(MotionGame game)
        {
            game.Sensor.Inject(MotionGame.BodyZone.center, 1, .08f); yield return null;
            Assert.That(game.Present, Is.True);
            for (float t = 0; t < 12 && game.State != MotionGame.Phase.Play; t += Time.unscaledDeltaTime) yield return null;
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Play));
        }

        [UnityTest] public IEnumerator WindmillCountsArmCirclesTheRightWay()
        {
            var game = Open<WindmillGame>(4);
            yield return Calibrated(game, () => FakePose.Body());
            Assert.That(game.UsingPose, Is.True); Assert.That(game.Arms.HasBaseline, Is.True);
            Assert.That(game.Target, Is.EqualTo(ArmCircleDetector.Side.Right)); Assert.That(game.Way, Is.EqualTo(-1));
            // Right arm, clockwise on screen: three circles, three stars, then a rest and the left arm.
            yield return Poses(game, 1.5f, t => FakePose.Body(rightWrist: FakePose.Around(FakePose.RightShoulder, -90 - 400 * t)));
            Assert.That(game.Score, Is.EqualTo(1));
            yield return MotionGamesPlayTests.Shot("motion-windmill.png");
            yield return Poses(game, 2.6f, t => FakePose.Body(rightWrist: FakePose.Around(FakePose.RightShoulder, -130 - 720 * t)));
            Assert.That(game.Score, Is.EqualTo(3)); Assert.That(game.Resting, Is.True);
            Assert.That(game.Target, Is.EqualTo(ArmCircleDetector.Side.Left)); Assert.That(game.Way, Is.EqualTo(1));
            yield return Poses(game, WindmillGame.SwitchRest + .2f, _ => FakePose.Body());
            Assert.That(game.Resting, Is.False);
            // The wrong way round gives no star, only a hint.
            bool hinted = false;
            yield return Poses(game, 1.5f, t => FakePose.Body(leftWrist: FakePose.Around(FakePose.LeftShoulder, -90 - 400 * t)), () => hinted |= game.CueText.Contains("sebaliknya"));
            Assert.That(game.Score, Is.EqualTo(3)); Assert.That(hinted, Is.True);
            yield return Poses(game, 1.5f, t => FakePose.Body(leftWrist: FakePose.Around(FakePose.LeftShoulder, -90 + 400 * t)));
            Assert.That(game.Score, Is.EqualTo(4));
            MotionGame.ShowDebug = true; yield return null; yield return null;
            Assert.That(game.DebugShown, Does.Contain("Putar lengan")); Assert.That(game.DebugShown, Does.Contain("lingkaran kiri"));
            MotionGame.ShowDebug = false;
            game.Quit(); yield return null;
        }

        [UnityTest] public IEnumerator WindmillWithoutPoseFollowsMotionRoundTheRing()
        {
            var game = Open<WindmillGame>(4);
            yield return null;
            Assert.That(game.UsingPose, Is.False);
            yield return CalibratedByMotion(game);
            // Motion (or the pointer) going clockwise round the ring at the guide's right shoulder.
            var centre = WindmillGame.RightShoulder;
            for (float t = 0; t < 6 && game.Score == 0; t += Time.unscaledDeltaTime)
            {
                float a = (-90 - 300 * t) * Mathf.Deg2Rad;
                game.Sensor.Inject(centre + new Vector2(Mathf.Cos(a) * .17f * .75f, Mathf.Sin(a) * .17f), 1, .04f);
                yield return null;
            }
            Assert.That(game.Score, Is.EqualTo(1));
            game.Quit(); yield return null;
        }

        static PoseResult Jump(float t)
        {
            // Over half a second: knee dip, take-off, flight, landing.
            if (t < .3f) return FakePose.Body(.1f * Mathf.Sin(t / .3f * Mathf.PI));
            if (t < .75f) return FakePose.Body(0, .045f * Mathf.Sin((t - .3f) / .45f * Mathf.PI));
            return FakePose.Body(.08f * Mathf.Sin((t - .75f) / .25f * Mathf.PI));
        }

        [UnityTest] public IEnumerator PuddleCountsJumpsAtTheFeetAndRestsAfterThree()
        {
            var game = Open<PuddleGame>(5);
            yield return Calibrated(game, () => FakePose.Body());
            Assert.That(game.UsingPose, Is.True); Assert.That(game.Jumps.HasBaseline, Is.True);
            // 1: jump when the puddle is at the feet.
            yield return UntilNear(game);
            yield return MotionGamesPlayTests.Shot("motion-puddle.png");
            yield return Poses(game, .55f, Jump);
            Assert.That(game.Score, Is.EqualTo(1)); Assert.That(game.Jumps.Count, Is.EqualTo(1));
            // 2: standing on tiptoe is not a jump.
            yield return UntilNear(game);
            yield return Poses(game, .4f, t => FakePose.Body(tiptoe: .035f * t));
            yield return Poses(game, 1.2f, _ => FakePose.Body(tiptoe: .035f));
            yield return Poses(game, .3f, t => FakePose.Body(tiptoe: .035f * (1 - t)));
            Assert.That(game.Score, Is.EqualTo(1)); Assert.That(game.Missed, Is.EqualTo(1));
            // 3: jumping long before the puddle comes gives nothing; jumping at the feet does.
            for (float t = 0; t < 3 && game.NextArrival < 0; t += Time.unscaledDeltaTime) { game.Sensor.InjectPose(FakePose.Body()); yield return null; }
            yield return Poses(game, .55f, Jump);
            Assert.That(game.Score, Is.EqualTo(1));
            yield return UntilNear(game);
            yield return Poses(game, .55f, Jump);
            Assert.That(game.Score, Is.EqualTo(2));
            // After a set of three puddles: a rest, no puddles.
            bool rest = false;
            yield return Poses(game, .5f, _ => FakePose.Body(), () => rest |= game.CueText.Contains("Istirahat"));
            Assert.That(game.Resting, Is.True); Assert.That(rest, Is.True);
            game.Quit(); yield return null;
        }
        static IEnumerator UntilNear(PuddleGame game)
        {
            for (float t = 0; t < 8 && !(game.PuddleNear && game.NextArrival < .4f); t += Time.unscaledDeltaTime) { game.Sensor.InjectPose(FakePose.Body()); yield return null; }
            Assert.That(game.PuddleNear, Is.True);
        }

        [UnityTest] public IEnumerator PuddleWithoutPoseCountsMotionAtTheFeet()
        {
            var game = Open<PuddleGame>(5);
            yield return null;
            Assert.That(game.UsingPose, Is.False);
            yield return CalibratedByMotion(game);
            for (float t = 0; t < 8 && !game.PuddleNear; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0; t < 1.2f && game.Score == 0; t += Time.unscaledDeltaTime) { game.Sensor.Inject(game.Feet, 1, .08f); yield return null; }
            Assert.That(game.Score, Is.EqualTo(1));
            game.Quit(); yield return null;
        }

        [UnityTest] public IEnumerator BirdCountsTurnsToTheBird()
        {
            var game = Open<BirdGame>(6);
            yield return Calibrated(game, () => FakePose.Turned(0));
            Assert.That(game.UsingPose, Is.True); Assert.That(game.Turns.HasBaseline, Is.True);
            // Left: a quarter turn to the left, held.
            yield return UntilAsked(game, BirdGame.Ask.Left);
            yield return MotionGamesPlayTests.Shot("motion-bird.png");
            yield return Poses(game, .8f, t => FakePose.Turned(-90 * t));
            yield return Poses(game, 1.2f, _ => FakePose.Turned(-90));
            Assert.That(game.Score, Is.EqualTo(1)); Assert.That(game.Now, Is.EqualTo(BirdGame.Stage.Returning));
            yield return Poses(game, .8f, t => FakePose.Turned(-90 + 90 * t));
            // Right, but the child turns left first: a hint, no star; then the right way.
            yield return UntilAsked(game, BirdGame.Ask.Right);
            bool wrong = false;
            yield return Poses(game, .8f, t => FakePose.Turned(-90 * t));
            yield return Poses(game, .8f, _ => FakePose.Turned(-90), () => wrong |= game.CueText.Contains("arah burung"));
            Assert.That(game.Score, Is.EqualTo(1)); Assert.That(wrong, Is.True);
            yield return Poses(game, 1.6f, t => FakePose.Turned(-90 + 180 * t));
            yield return Poses(game, 1.2f, _ => FakePose.Turned(90));
            Assert.That(game.Score, Is.EqualTo(2));
            yield return Poses(game, .8f, t => FakePose.Turned(90 - 90 * t));
            // Back: half a turn, held, then back to the front.
            yield return UntilAsked(game, BirdGame.Ask.Back);
            yield return Poses(game, 1.5f, t => FakePose.Turned(-180 * t));
            yield return Poses(game, 1.2f, _ => FakePose.Turned(-180));
            Assert.That(game.Score, Is.EqualTo(3));
            yield return Poses(game, 1.5f, t => FakePose.Turned(-180 + 180 * t));
            yield return Poses(game, .6f, _ => FakePose.Turned(0));
            Assert.That(game.Turns.Settled, Is.EqualTo(TurnDetector.Facing.Front)); Assert.That(game.Turns.FullTurns, Is.Zero);
            MotionGame.ShowDebug = true; yield return null; yield return null;
            Assert.That(game.DebugShown, Does.Contain("hadap"));
            MotionGame.ShowDebug = false;
            game.Quit(); yield return null;
        }
        static IEnumerator UntilAsked(BirdGame game, BirdGame.Ask ask)
        {
            for (float t = 0; t < 6 && !(game.Now == BirdGame.Stage.Asking && game.Current == ask); t += Time.unscaledDeltaTime) { game.Sensor.InjectPose(FakePose.Turned(0)); yield return null; }
            Assert.That(game.Now, Is.EqualTo(BirdGame.Stage.Asking)); Assert.That(game.Current, Is.EqualTo(ask));
        }

        [UnityTest] public IEnumerator BirdWithoutPoseCountsMotionAtTheBird()
        {
            var game = Open<BirdGame>(6);
            yield return null;
            Assert.That(game.UsingPose, Is.False);
            yield return CalibratedByMotion(game);
            for (float t = 0; t < 4 && game.Now != BirdGame.Stage.Asking; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0; t < 1 && game.Score == 0; t += Time.unscaledDeltaTime) { game.Sensor.Inject(game.BirdPoint, 1, .06f); yield return null; }
            Assert.That(game.Score, Is.EqualTo(1));
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
