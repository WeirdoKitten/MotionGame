using System;
using FamilyAdventure.Presentation;
using NUnit.Framework;
using UnityEngine;
namespace FamilyAdventure.PlayTests
{
    // Shared driver: feeds hand-made poses at the real pose rate (about 12 samples per second).
    public abstract class PoseStream
    {
        protected const float Rate = MotionSensor.PoseInterval;
        protected float time;
        protected System.Random random;
        protected void Feed(IBasicMoveDetector detector, PoseResult pose) { detector.Step(pose, time); time += Rate; }
        // Moves over some seconds; t runs from 0 to 1.
        protected void Move(IBasicMoveDetector detector, float seconds, Func<float, PoseResult> pose)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / Rate));
            for (int i = 1; i <= steps; i++) Feed(detector, pose((float)i / steps));
        }
        protected void Hold(IBasicMoveDetector detector, float seconds, Func<PoseResult> pose) => Move(detector, seconds, _ => pose());
        protected void Miss(IBasicMoveDetector detector, float seconds) => Hold(detector, seconds, FakePose.Missing);
    }

    // ArmCircleDetector: whole circles round the shoulder count, look-alikes do not.
    public sealed class ArmCircleDetectorTests : PoseStream
    {
        const float Noise = .003f;
        ArmCircleDetector Standing()
        {
            var d = new ArmCircleDetector(); time = 0; random = new System.Random(11);
            Hold(d, 1, () => FakePose.Body(noise: Noise, random: random));
            Assert.That(d.HasBaseline, Is.True); Assert.That(d.Tracking, Is.True);
            return d;
        }
        PoseResult Right(float degrees, float radius = .19f) => FakePose.Body(noise: Noise, random: random, rightWrist: FakePose.Around(FakePose.RightShoulder, degrees, radius));
        PoseResult Left(float degrees, float radius = .19f) => FakePose.Body(noise: Noise, random: random, leftWrist: FakePose.Around(FakePose.LeftShoulder, degrees, radius));
        // Circles from the arm hanging down (-90 degrees); positive turns are counterclockwise on screen.
        void Circle(ArmCircleDetector d, Func<float, PoseResult> arm, float turns, float seconds) => Move(d, seconds, t => arm(-90 + 360 * turns * t));

        [Test] public void OneCircleCountsOnce()
        {
            var d = Standing();
            Circle(d, a => Right(a), 1.1f, 1.2f);
            Hold(d, 2, () => Right(-50));
            Assert.That(d.Count, Is.EqualTo(1)); Assert.That(d.Circles(ArmCircleDetector.Side.Right), Is.EqualTo(1));
            Assert.That(d.LastDirection(ArmCircleDetector.Side.Right), Is.EqualTo(1)); Assert.That(d.Last.direction, Is.EqualTo(1));
            Assert.That(d.Circles(ArmCircleDetector.Side.Left), Is.Zero);
        }

        [Test] public void ThreeClockwiseCirclesCountThree()
        {
            var d = Standing();
            Circle(d, a => Left(a), -3.05f, 3);
            Assert.That(d.Circles(ArmCircleDetector.Side.Left), Is.EqualTo(3)); Assert.That(d.LastDirection(ArmCircleDetector.Side.Left), Is.EqualTo(-1));
        }

        [Test] public void FastCirclesStillCount()
        {
            // Two circles a second: 60 degrees between samples.
            var d = Standing();
            Circle(d, a => Right(a), 4.05f, 2);
            Assert.That(d.Count, Is.EqualTo(4));
        }

        [Test] public void HalfCirclesBackAndForthAreNotCircles()
        {
            var d = Standing();
            for (int i = 0; i < 3; i++) { Move(d, .6f, t => Right(-90 + 200 * t)); Move(d, .6f, t => Right(110 - 200 * t)); }
            Hold(d, 2, () => Right(-90));
            Assert.That(d.Count, Is.Zero);
        }

        [Test] public void WavingIsNotACircle()
        {
            var d = Standing();
            Move(d, 3, t => Right(90 + 35 * Mathf.Sin(t * 20)));
            Assert.That(d.Count, Is.Zero); Assert.That(d.Incomplete, Is.Zero);
        }

        [Test] public void SmallCirclesAreNotArmCircles()
        {
            var d = Standing();
            // Hand circling close to the shoulder (bent elbow) ...
            Move(d, 2, t => Right(-90 + 720 * t, .06f));
            // ... and small circles with the hand out to the side.
            var outside = FakePose.Around(FakePose.RightShoulder, 0);
            Move(d, 2, t => FakePose.Body(rightWrist: outside + FakePose.Around(Vector2.zero, 720 * t, .04f)));
            Assert.That(d.Count, Is.Zero);
        }

        [Test] public void CirclesBesideTheBodyAreNotSeen()
        {
            // Arm circling forward beside the body: in the picture the wrist only moves up and down.
            var d = Standing();
            Move(d, 3, t => FakePose.Body(rightWrist: FakePose.RightShoulder + new Vector2(.01f, .19f * Mathf.Sin(t * Mathf.PI * 6))));
            Assert.That(d.Count, Is.Zero);
        }

        [Test] public void StoppingHalfWayIsIncomplete()
        {
            var d = Standing();
            Move(d, .8f, t => Right(-90 + 270 * t));
            Hold(d, 2, () => Right(180));
            Assert.That(d.Count, Is.Zero); Assert.That(d.Incomplete, Is.EqualTo(1)); Assert.That(d.LastMiss, Is.EqualTo("berhenti"));
        }

        [Test] public void ShortDropoutIsTolerated()
        {
            var d = Standing();
            Move(d, .5f, t => Right(-90 + 160 * t));
            Miss(d, .25f);
            Move(d, .75f, t => Right(70 + 240 * t));
            Assert.That(d.Count, Is.EqualTo(1));
        }

        [Test] public void LongDropoutDropsTheCircle()
        {
            var d = Standing();
            Move(d, .8f, t => Right(-90 + 270 * t));
            Miss(d, 1);
            Assert.That(d.Tracking, Is.False);
            Move(d, .4f, t => Right(180 + 100 * t));
            Assert.That(d.Count, Is.Zero);
        }
    }

    // JumpDetector: both feet off the floor and back counts once; tiptoe, one foot, squats and stepping up do not.
    public sealed class JumpDetectorTests : PoseStream
    {
        const float Noise = .002f;
        JumpDetector Standing(bool feet = true, bool toes = true)
        {
            var d = new JumpDetector(); time = 0; random = new System.Random(5);
            Hold(d, 1, () => Body(feet: feet, toes: toes));
            Assert.That(d.HasBaseline, Is.True); Assert.That(d.State, Is.EqualTo(JumpDetector.Phase.Ground));
            return d;
        }
        PoseResult Body(float depth = 0, float lift = 0, float tiptoe = 0, float footUp = 0, bool feet = true, bool toes = true) =>
            FakePose.Body(depth, lift, feet: feet, noise: Noise, random: random, tiptoe: tiptoe, footUp: footUp, toes: toes);
        // Knee dip, take-off, a short flight (lift in screen height), landing dip, standing.
        void Jump(JumpDetector d, float lift = .04f, bool feet = true, bool toes = true, bool missFlight = false)
        {
            Move(d, .17f, t => Body(.1f * t, feet: feet, toes: toes));
            Feed(d, Body(0, feet: feet, toes: toes));
            if (missFlight) { Feed(d, Body(0, lift * .75f, feet: feet, toes: toes)); Miss(d, .17f); }
            else { Feed(d, Body(0, lift * .75f, feet: feet, toes: toes)); Feed(d, Body(0, lift, feet: feet, toes: toes)); Feed(d, Body(0, lift * .5f, feet: feet, toes: toes)); }
            Feed(d, Body(.08f, feet: feet, toes: toes));
            Hold(d, .8f, () => Body(feet: feet, toes: toes));
        }

        [Test] public void SmallJumpCountsOnce()
        {
            var d = Standing();
            Jump(d);
            Hold(d, 2, () => Body());
            Assert.That(d.Count, Is.EqualTo(1)); Assert.That(d.Incomplete, Is.Zero);
            Assert.That(d.Last.peak, Is.InRange(.08f, .14f)); Assert.That(d.Last.end - d.Last.start, Is.LessThan(.5f));
            Assert.That(d.Last.confidence, Is.GreaterThan(.8f));
        }

        [Test] public void ThreeJumpsCountThree()
        {
            var d = Standing();
            for (int i = 0; i < 3; i++) Jump(d);
            Assert.That(d.Count, Is.EqualTo(3));
        }

        [Test] public void TiptoeIsNotAJump()
        {
            // With toes (BlazePose) and without (MoveNet: only the slow rise tells).
            foreach (bool toes in new[] { true, false })
            {
                var d = Standing(toes: toes);
                Move(d, .5f, t => Body(tiptoe: .03f * t, toes: toes)); Hold(d, 1, () => Body(tiptoe: .03f, toes: toes)); Move(d, .5f, t => Body(tiptoe: .03f * (1 - t), toes: toes));
                Assert.That(d.Count, Is.Zero, "toes " + toes);
            }
        }

        [Test] public void QuickTiptoeWithToesSeenIsNotAJump()
        {
            var d = Standing();
            for (int i = 0; i < 3; i++) { Feed(d, Body(tiptoe: .035f)); Feed(d, Body(tiptoe: .035f)); Hold(d, .5f, () => Body()); }
            Assert.That(d.Count, Is.Zero); Assert.That(d.Reason, Is.Empty.Or.EqualTo("jinjit"));
        }

        [Test] public void LiftingOneFootIsNotAJump()
        {
            var d = Standing();
            Move(d, .2f, t => Body(footUp: .1f * t)); Hold(d, .5f, () => Body(footUp: .1f));
            Assert.That(d.Reason, Is.EqualTo("satu kaki"));
            Move(d, .3f, t => Body(footUp: .1f * (1 - t))); Hold(d, .5f, () => Body());
            Assert.That(d.Count, Is.Zero);
        }

        [Test] public void SquatIsNotAJump()
        {
            var d = Standing();
            Move(d, 1, t => Body(.4f * t)); Move(d, 1, t => Body(.4f * (1 - t))); Hold(d, .5f, () => Body());
            Assert.That(d.Count, Is.Zero);
        }

        [Test] public void SteppingUpAndStayingIsNotAJump()
        {
            var d = Standing();
            Feed(d, Body(0, .03f)); Hold(d, 2, () => Body(0, .06f));
            Assert.That(d.Count, Is.Zero); Assert.That(d.Incomplete, Is.EqualTo(1)); Assert.That(d.LastMiss, Is.EqualTo("terlalu lama"));
        }

        [Test] public void FramesMissedInTheAirStillCount()
        {
            // The head leaves the picture at the top of the jump.
            var d = Standing();
            Jump(d, missFlight: true);
            Assert.That(d.Count, Is.EqualTo(1)); Assert.That(d.Last.confidence, Is.LessThan(.8f));
        }

        [Test] public void FeetOutOfViewOnlyClearJumpsCount()
        {
            var d = Standing(feet: false);
            Jump(d, .015f, feet: false);
            Assert.That(d.Count, Is.Zero, "a tiny hop without feet in view is not trusted");
            Jump(d, .06f, feet: false);
            Assert.That(d.Count, Is.EqualTo(1));
        }

        [Test] public void StandingWithJitterCountsNothing()
        {
            var d = Standing();
            random = new System.Random(9);
            Move(d, 10, _ => FakePose.Body(noise: .006f, random: random));
            Assert.That(d.Count, Is.Zero); Assert.That(d.Incomplete, Is.Zero);
        }
    }

    // TurnDetector: quarter, half and full turns from the order of settled facings.
    public sealed class TurnDetectorTests : PoseStream
    {
        const float Noise = .002f;
        bool depth = true, swap, farShoulder = true;
        TurnDetector Standing()
        {
            var d = new TurnDetector(); time = 0; random = new System.Random(3);
            Hold(d, 1, () => Pose(0));
            Assert.That(d.HasBaseline, Is.True); Assert.That(d.Settled, Is.EqualTo(TurnDetector.Facing.Front));
            return d;
        }
        PoseResult Pose(float yaw, float head = 0) => FakePose.Turned(yaw, head, depth, swap, farShoulder, Noise, random);
        void Turn(TurnDetector d, float from, float to, float seconds) => Move(d, seconds, t => Pose(Mathf.Lerp(from, to, t)));

        [Test] public void QuarterTurnLeftAndBack()
        {
            var d = Standing();
            Turn(d, 0, -90, .8f); Hold(d, 1, () => Pose(-90));
            Assert.That(d.Settled, Is.EqualTo(TurnDetector.Facing.Left)); Assert.That(d.Quarter, Is.EqualTo(-1));
            Assert.That(d.Count, Is.EqualTo(1)); Assert.That(d.Direction, Is.EqualTo(-1)); Assert.That(d.Last.peak, Is.EqualTo(90));
            Turn(d, -90, 0, .8f); Hold(d, 1, () => Pose(0));
            Assert.That(d.Settled, Is.EqualTo(TurnDetector.Facing.Front)); Assert.That(d.Quarter, Is.Zero);
            Assert.That(d.Count, Is.EqualTo(2)); Assert.That(d.Direction, Is.EqualTo(1)); Assert.That(d.FullTurns, Is.Zero);
        }

        [Test] public void QuarterTurnRightWithTheFarShoulderHidden()
        {
            farShoulder = false;
            var d = Standing();
            Turn(d, 0, 90, .8f); Hold(d, 1, () => Pose(90));
            Assert.That(d.Settled, Is.EqualTo(TurnDetector.Facing.Right)); Assert.That(d.Quarter, Is.EqualTo(1));
        }

        [Test] public void HalfTurnAndBackTheSameWayIsNotAFullTurn()
        {
            var d = Standing();
            Turn(d, 0, -180, 1.5f); Hold(d, 1, () => Pose(-180));
            Assert.That(d.Settled, Is.EqualTo(TurnDetector.Facing.Back)); Assert.That(d.Quarter, Is.EqualTo(-2));
            Turn(d, -180, 0, 1.5f); Hold(d, 1, () => Pose(0));
            Assert.That(d.Quarter, Is.Zero); Assert.That(d.FullTurns, Is.Zero);
        }

        [Test] public void FullTurnLeftCountsOnce()
        {
            var d = Standing();
            Turn(d, 0, -360, 3); Hold(d, 1, () => Pose(0));
            Assert.That(d.FullTurns, Is.EqualTo(1)); Assert.That(d.Quarter, Is.EqualTo(-4));
            Assert.That(d.Last.move, Is.EqualTo("full turn")); Assert.That(d.Last.direction, Is.EqualTo(-1));
        }

        [Test] public void FullTurnRightWithoutDepthCounts()
        {
            // MoveNet gives no depth: the nose and ears tell the side.
            depth = false;
            var d = Standing();
            Turn(d, 0, 360, 3); Hold(d, 1, () => Pose(0));
            Assert.That(d.FullTurns, Is.EqualTo(1)); Assert.That(d.Quarter, Is.EqualTo(4));
        }

        [Test] public void FullTurnWithSwappedLabelsAndTheBackMissedCounts()
        {
            swap = true;
            var d = Standing();
            Turn(d, 0, -150, 1.25f);
            Miss(d, .5f);
            Turn(d, -210, -360, 1.25f); Hold(d, 1, () => Pose(0));
            Assert.That(d.FullTurns, Is.EqualTo(1)); Assert.That(d.Quarter, Is.EqualTo(-4));
        }

        [Test] public void TurningOnlyTheHeadIsNotABodyTurn()
        {
            var d = Standing();
            Move(d, .5f, t => Pose(0, -80 * t)); Hold(d, 1, () => Pose(0, -80)); Move(d, .5f, t => Pose(0, -80 * (1 - t)));
            Assert.That(d.Count, Is.Zero); Assert.That(d.Settled, Is.EqualTo(TurnDetector.Facing.Front));
        }

        [Test] public void ShortTurnAndBackIsIncomplete()
        {
            var d = Standing();
            Turn(d, 0, -65, .4f); Feed(d, Pose(-65)); Turn(d, -65, 0, .4f); Hold(d, 1, () => Pose(0));
            Assert.That(d.Count, Is.Zero); Assert.That(d.Incomplete, Is.EqualTo(1));
        }

        [Test] public void BriefDropoutKeepsTheFacing()
        {
            var d = Standing();
            Turn(d, 0, -90, .8f); Hold(d, .5f, () => Pose(-90));
            Miss(d, .4f);
            Hold(d, .5f, () => Pose(-90));
            Assert.That(d.Settled, Is.EqualTo(TurnDetector.Facing.Left)); Assert.That(d.Count, Is.EqualTo(1));
        }

        [Test] public void LongLossForgetsTheFacingWithoutCountingATurn()
        {
            var d = Standing();
            Turn(d, 0, -90, .8f); Hold(d, .5f, () => Pose(-90));
            Miss(d, 5);
            Assert.That(d.Tracking, Is.False); Assert.That(d.Settled, Is.EqualTo(TurnDetector.Facing.Unknown));
            Hold(d, 1, () => Pose(0));
            Assert.That(d.Settled, Is.EqualTo(TurnDetector.Facing.Front)); Assert.That(d.Count, Is.EqualTo(1));
        }
    }
}
