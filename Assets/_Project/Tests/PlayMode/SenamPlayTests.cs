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
    // Ikuti Senam with the senam converted from the video (Resources/Motion/Senam/senam-pemanasan.txt). The
    // "child" is the instructor's own pose track placed on a smaller child body standing a little to the
    // left, so a child who copies the video must reach every move and a child who stands still almost none.
    public sealed class SenamPlayTests
    {
        static readonly PoseJoint[] Joints =
        {
            PoseJoint.Nose, PoseJoint.LeftEar, PoseJoint.RightEar, PoseJoint.LeftShoulder, PoseJoint.RightShoulder,
            PoseJoint.LeftElbow, PoseJoint.RightElbow, PoseJoint.LeftWrist, PoseJoint.RightWrist, PoseJoint.LeftHip, PoseJoint.RightHip,
            PoseJoint.LeftKnee, PoseJoint.RightKnee, PoseJoint.LeftAnkle, PoseJoint.RightAnkle,
        };
        static readonly SenamBody Child = new SenamBody { Hip = new Vector2(.47f, .40f), Torso = .21f, Leg = .26f, Aspect = 4f / 3 };

        // The child copying the instructor at a time of the senam (screen-left joints are the child's left, as in the mirror).
        static PoseResult Copy(SenamChoreography senam, float time, float jitter = 0, System.Random random = null)
        {
            var guide = new Vector2[SenamChoreography.Joints]; senam.Guide(time, guide);
            var pose = new PoseResult { found = true, aspect = 4f / 3 };
            for (int j = 0; j < Joints.Length; j++)
            {
                var point = Child.ToScreen(guide[j], SenamChoreography.IsLeg(j), senam.LegRatio);
                if (random != null) point += new Vector2((float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble() * 2 - 1)) * jitter;
                pose.Set(Joints[j], point, 1);
            }
            pose.DeriveLimbs();
            return pose;
        }

        static float Follow(SenamChoreography senam, System.Func<float, PoseResult> child, out SenamJudge judge)
        {
            var body = new SenamBody();
            for (float t = 0; t < .5f; t += 1 / 30f) body.Add(child(t));
            judge = new SenamJudge(senam, body);
            for (float t = 0; t <= senam.Duration + 1; t += 1 / 30f) judge.Update(t, child(t), null);
            int moves = judge.Targets.Select(x => x.move).Distinct().Count();
            return (float)judge.MovesCompleted / moves;
        }

        [Test] public void ChoreographyFromTheVideoLoads()
        {
            var senam = SenamGame.Load("senam-pemanasan");
            Assert.That(senam, Is.Not.Null);
            Assert.That(senam.Duration, Is.EqualTo(126).Within(1));
            Assert.That(senam.GuideFrames, Is.GreaterThan(1500));
            foreach (var kind in new[] { SenamChoreography.Kind.Hands, SenamChoreography.Kind.Foot, SenamChoreography.Kind.Head, SenamChoreography.Kind.Hips, SenamChoreography.Kind.March })
                Assert.That(senam.Moves.Count(m => m.kind == kind), Is.GreaterThan(0), kind + " moves");
            // Arms straight up around 10 s, hands on the hips from about 22 s to 37 s, knee raises at about 72 s.
            Assert.That(senam.Moves.Any(m => m.kind == SenamChoreography.Kind.Hands && Mathf.Abs(m.time - 9.9f) < .3f && m.left.y > 1.8f && m.right.y > 1.8f));
            Assert.That(senam.Moves.Any(m => m.kind == SenamChoreography.Kind.Hips && m.time < 23 && m.end > 36));
            Assert.That(senam.Moves.Any(m => m.kind == SenamChoreography.Kind.Foot && m.raise && m.time > 71 && m.time < 76));
        }

        [Test] public void ChildWhoCopiesTheVideoReachesTheMovesAndStillChildDoesNot()
        {
            var senam = SenamGame.Load("senam-pemanasan");
            var random = new System.Random(3);
            Assert.That(Follow(senam, t => Copy(senam, t), out var exact), Is.GreaterThan(.98f), "copying exactly");
            Assert.That(Follow(senam, t => Copy(senam, t - .4f, .02f, random), out _), Is.GreaterThan(.95f), "copying late and shaky");
            Assert.That(Follow(senam, t => Copy(senam, 0), out var still), Is.LessThan(.05f), "standing still");
            Assert.That(exact.Steps, Is.GreaterThan(80), "marching steps counted"); Assert.That(still.Steps, Is.Zero);
            // Research log: one line per target plus header and march summary.
            var log = new StringWriter(); exact.WriteCsv(log);
            Assert.That(log.ToString().Split('\n').Count(l => l.Length > 0), Is.EqualTo(exact.Targets.Count + 2));
        }

        [UnityTest] public IEnumerator SenamGamePlaysWithACopyingChild()
        {
            LogAssert.ignoreFailingMessages = true; MotionSensor.AllowCamera = false;
            var events = new GameObject("Events", typeof(EventSystem));
            var menu = MotionMissionMenu.Open(null);
            yield return null;
            menu.Play(System.Array.IndexOf(MotionMissionMenu.Titles, "Ikuti Senam"));
            var game = (SenamGame)menu.Current; game.Sensor.UsePointer = false;
            Assert.That(game.Senam.Moves.Count, Is.GreaterThan(100));
            // The child stands in the guide (the instructor's first pose), keeps still, then copies the video.
            for (float t = 0; t < 15 && game.State != MotionGame.Phase.Play; t += Time.unscaledDeltaTime)
            {
                game.Sensor.InjectPose(Copy(game.Senam, 0)); yield return null;
            }
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Play));
            Assert.That(game.Body.Measured, Is.True); Assert.That(game.Body.Torso, Is.EqualTo(Child.Torso).Within(.03f));
            bool shot = false; int shown = 0;
            while (game.SongTime < 12)
            {
                game.Sensor.InjectPose(Copy(game.Senam, game.SongTime));
                shown = Mathf.Max(shown, game.PicturesShown);
                if (!shot && game.SongTime > 9.75f) { shot = true; yield return MotionGamesPlayTests.Shot("motion-senam.png"); }
                yield return null;
            }
            Assert.That(shown, Is.GreaterThan(0), "target pictures were shown");
            Assert.That(game.Judge.MovesCompleted, Is.GreaterThan(5)); Assert.That(game.Score, Is.GreaterThan(5));
            game.EndNow(); yield return null;
            Assert.That(game.State, Is.EqualTo(MotionGame.Phase.Done));
            yield return MotionGamesPlayTests.Shot("motion-senam-result.png");
            game.Quit(); yield return null;
            Object.Destroy(menu.gameObject); Object.Destroy(events);
            MotionSensor.AllowCamera = true;
        }
    }
}
