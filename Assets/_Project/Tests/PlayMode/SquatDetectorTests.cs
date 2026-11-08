using System;
using FamilyAdventure.Presentation;
using NUnit.Framework;
using UnityEngine;
namespace FamilyAdventure.PlayTests
{
    // SquatDetector on hand-made pose streams at the real pose rate (about 12 samples per second).
    public sealed class SquatDetectorTests
    {
        const float Rate = MotionSensor.PoseInterval, Noise = .003f;
        float time;
        System.Random random;

        // One second of standing still gives the baseline.
        SquatDetector Standing(bool feet = true)
        {
            var detector = new SquatDetector(); time = 0; random = new System.Random(7);
            Hold(detector, 1, () => Body(0, feet: feet));
            Assert.That(detector.HasBaseline, Is.True);
            Assert.That(detector.State, Is.EqualTo(SquatDetector.Phase.Up));
            return detector;
        }
        PoseResult Body(float depth, float lift = 0, float bend = 0, float kneel = 0, bool feet = true) =>
            FakePose.Body(depth, lift, bend, kneel, feet, .5f, Noise, random);
        void Feed(SquatDetector detector, PoseResult pose) { detector.Step(pose, time); time += Rate; }
        // Moves over some seconds; t runs from 0 to 1.
        void Move(SquatDetector detector, float seconds, Func<float, PoseResult> pose)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / Rate));
            for (int i = 1; i <= steps; i++) Feed(detector, pose((float)i / steps));
        }
        void Hold(SquatDetector detector, float seconds, Func<PoseResult> pose) => Move(detector, seconds, _ => pose());
        void Squat(SquatDetector detector, float depth, float down = 1, float hold = .5f, float up = 1, bool feet = true)
        {
            Move(detector, down, t => Body(depth * t, feet: feet));
            Hold(detector, hold, () => Body(depth, feet: feet));
            Move(detector, up, t => Body(depth * (1 - t), feet: feet));
            Hold(detector, .5f, () => Body(0, feet: feet));
        }

        [Test] public void FullSquatCountsOnce()
        {
            var d = Standing();
            Move(d, 1, t => Body(.45f * t));
            Assert.That(d.State, Is.EqualTo(SquatDetector.Phase.Low));
            // Wobbling at the bottom (still well below the line) does not count again.
            Move(d, 1.5f, t => Body(.45f + .04f * Mathf.Sin(t * 30)));
            Assert.That(d.Count, Is.Zero, "Nothing counts before standing up again");
            Move(d, 1, t => Body(.45f * (1 - t)));
            Hold(d, 1, () => Body(0));
            Assert.That(d.Count, Is.EqualTo(1)); Assert.That(d.Incomplete, Is.Zero);
            Assert.That(d.Last.peak, Is.InRange(.4f, .52f)); Assert.That(d.Last.end - d.Last.start, Is.GreaterThan(2));
            Assert.That(d.Last.confidence, Is.GreaterThan(.8f)); Assert.That(d.State, Is.EqualTo(SquatDetector.Phase.Up));
        }

        [Test] public void BouncingAroundTheTargetCountsOnce()
        {
            var d = Standing();
            Move(d, 1, t => Body(.3f * t));
            // Up and down across the target line, never back to standing.
            Move(d, 2, t => Body(.27f + .07f * Mathf.Sin(t * 25)));
            Move(d, 1, t => Body(.3f * (1 - t)));
            Hold(d, .5f, () => Body(0));
            Assert.That(d.Count, Is.EqualTo(1)); Assert.That(d.Incomplete, Is.Zero);
        }

        [Test] public void ThreeSquatsCountThree()
        {
            var d = Standing();
            for (int i = 0; i < 3; i++) Squat(d, .4f);
            Assert.That(d.Count, Is.EqualTo(3));
        }

        [Test] public void ShallowSquatIsIncomplete()
        {
            var d = Standing();
            Squat(d, .18f);
            Assert.That(d.Count, Is.Zero); Assert.That(d.Incomplete, Is.EqualTo(1)); Assert.That(d.LastMiss, Is.EqualTo("kurang dalam"));
        }

        [Test] public void GoingDownWithoutStandingUpIsNotCounted()
        {
            var d = Standing();
            Move(d, 1, t => Body(.45f * t));
            Hold(d, 3, () => Body(.45f));
            Assert.That(d.Count, Is.Zero); Assert.That(d.State, Is.EqualTo(SquatDetector.Phase.Low));
            Assert.That(d.Progress, Is.InRange(.45f, .6f));
        }

        [Test] public void BendingOverIsNotASquat()
        {
            var d = Standing();
            Move(d, 1, t => Body(.03f * t, bend: .7f * t));
            Assert.That(d.Bending, Is.True); Assert.That(d.Reason, Is.EqualTo("membungkuk"));
            Hold(d, 1, () => Body(.03f, bend: .7f));
            Move(d, 1, t => Body(.03f * (1 - t), bend: .7f * (1 - t)));
            Assert.That(d.Count, Is.Zero); Assert.That(d.Incomplete, Is.Zero);
        }

        [Test] public void TiptoeAndJumpsAreNotSquats()
        {
            var d = Standing();
            // Tiptoe: the whole body rises a little and stays up.
            Move(d, .5f, t => Body(0, lift: .03f * t)); Hold(d, 1, () => Body(0, lift: .03f)); Move(d, .5f, t => Body(0, lift: .03f * (1 - t)));
            // Small jumps: a short knee dip, take-off, landing.
            for (int i = 0; i < 3; i++)
            {
                Move(d, .25f, t => Body(.12f * t)); Move(d, .15f, t => Body(.12f * (1 - t)));
                Move(d, .15f, t => Body(0, lift: .06f * t)); Move(d, .15f, t => Body(0, lift: .06f * (1 - t)));
                Move(d, .15f, t => Body(.1f * t)); Move(d, .25f, t => Body(.1f * (1 - t)));
                Hold(d, .8f, () => Body(0));
            }
            // A jump with a deep dip is too quick to be a squat.
            Move(d, .25f, t => Body(.3f * t)); Move(d, .15f, t => Body(.3f * (1 - t)));
            Move(d, .2f, t => Body(0, lift: .08f * t)); Move(d, .2f, t => Body(0, lift: .08f * (1 - t)));
            Hold(d, 1, () => Body(0));
            Assert.That(d.Count, Is.Zero);
        }

        [Test] public void KneelingIsNotASquat()
        {
            var d = Standing();
            Move(d, 1.2f, t => Body(.47f * t, kneel: t));
            Hold(d, 1, () => Body(.47f, kneel: 1));
            Move(d, 1.2f, t => Body(.47f * (1 - t), kneel: 1 - t));
            Hold(d, .5f, () => Body(0));
            Assert.That(d.Count, Is.Zero); Assert.That(d.LastMiss, Is.EqualTo("lutut di lantai"));
        }

        [Test] public void ShortDropoutIsTolerated()
        {
            var d = Standing();
            Move(d, .5f, t => Body(.2f * t));
            // A third of a second without a body while going down.
            for (int i = 0; i < 4; i++) Feed(d, FakePose.Missing());
            Assert.That(d.State, Is.Not.EqualTo(SquatDetector.Phase.Lost));
            Move(d, .5f, t => Body(.2f + .25f * t));
            for (int i = 0; i < 4; i++) Feed(d, FakePose.Missing());
            Move(d, 1, t => Body(.45f * (1 - t)));
            Hold(d, .5f, () => Body(0));
            Assert.That(d.Count, Is.EqualTo(1));
        }

        [Test] public void LongDropoutDropsTheSquatThenRecovers()
        {
            var d = Standing();
            Move(d, 1, t => Body(.45f * t));
            for (int i = 0; i < 24; i++) Feed(d, FakePose.Missing());
            Assert.That(d.State, Is.EqualTo(SquatDetector.Phase.Lost)); Assert.That(d.Tracking, Is.False);
            // Seen again while still low: nothing counts until standing.
            Hold(d, .5f, () => Body(.45f));
            Move(d, 1, t => Body(.45f * (1 - t)));
            Hold(d, .5f, () => Body(0));
            Assert.That(d.Count, Is.Zero); Assert.That(d.State, Is.EqualTo(SquatDetector.Phase.Up));
            Squat(d, .4f);
            Assert.That(d.Count, Is.EqualTo(1));
        }

        [Test] public void NoSamplesAtAllMeansLost()
        {
            var d = Standing();
            d.Wait(time + .3f); Assert.That(d.Tracking, Is.True);
            d.Wait(time + 1); Assert.That(d.State, Is.EqualTo(SquatDetector.Phase.Lost));
        }

        [Test] public void FeetOutOfViewStillCountsWithLessConfidence()
        {
            var d = Standing(feet: false);
            Assert.That(d.FloorFromFeet, Is.False);
            Squat(d, .4f, feet: false);
            Assert.That(d.Count, Is.EqualTo(1)); Assert.That(d.Last.confidence, Is.LessThan(.7f));
        }

        [Test] public void StandingWithJitterCountsNothing()
        {
            var d = Standing();
            random = new System.Random(3);
            Move(d, 10, _ => FakePose.Body(0, noise: .008f, random: random));
            Assert.That(d.Count, Is.Zero); Assert.That(d.Incomplete, Is.Zero);
        }

        [Test] public void CalibrationThenSquatsReportDirectionAndProgress()
        {
            // The games calibrate explicitly while the child stands still; no jitter here.
            var d = new SquatDetector(); time = 0; random = null;
            for (int i = 0; i < SquatDetector.BaselineSamples; i++) { d.Calibrate(FakePose.Body(), time); time += Rate; }
            Assert.That(d.HasBaseline, Is.True); Assert.That(d.LegLength, Is.EqualTo(FakePose.Leg).Within(.001f));
            Assert.That(d.TargetHipHeight, Is.EqualTo(FakePose.Floor + FakePose.Leg * (1 - d.TargetDepth)).Within(.001f));
            Move(d, 1, t => FakePose.Body(.4f * t));
            Assert.That(d.Direction, Is.EqualTo(1)); Assert.That(d.Progress, Is.EqualTo(.5f).Within(.06f));
            Move(d, .5f, t => FakePose.Body(.4f * (1 - t * .5f)));
            Assert.That(d.Direction, Is.EqualTo(-1)); Assert.That(d.Progress, Is.InRange(.6f, 1));
            Move(d, .5f, t => FakePose.Body(.2f * (1 - t)));
            Hold(d, .5f, () => FakePose.Body());
            Assert.That(d.Count, Is.EqualTo(1)); Assert.That(d.Direction, Is.EqualTo(0)); Assert.That(d.Progress, Is.Zero);
        }
    }
}
