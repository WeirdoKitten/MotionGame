using FamilyAdventure.Presentation;
using UnityEngine;
namespace FamilyAdventure.PlayTests
{
    // Hand-made poses of a child facing the camera (mirrored screen space, v up), standing with ankles at .12,
    // knees at .30, hips at .48 and shoulders at .72.
    static class FakePose
    {
        public const float Floor = .12f, Leg = .36f, Shin = .18f, Torso = .24f, Aspect = 4f / 3;
        public static readonly Vector2 LeftShoulder = new Vector2(.43f, .72f), RightShoulder = new Vector2(.57f, .72f);
        // depth: hip drop as a fraction of the leg; lift: whole body up (jump); bend: torso folded forward
        // (0..1); kneel: knees going down to the floor (0..1); feet: ankles in view; noise: random jitter of
        // every joint; wrists: hand positions (default hanging down); tiptoe: heels and body up, toes on the
        // floor; footUp: the left foot lifted; toes: whether the model gives toes (BlazePose yes, MoveNet no).
        public static PoseResult Body(float depth = 0, float lift = 0, float bend = 0, float kneel = 0, bool feet = true,
            float x = .5f, float noise = 0, System.Random random = null, Vector2? leftWrist = null, Vector2? rightWrist = null,
            float tiptoe = 0, float footUp = 0, bool toes = true)
        {
            var p = new PoseResult { found = true, aspect = Aspect };
            float Jitter() => random == null ? 0 : ((float)random.NextDouble() * 2 - 1) * noise;
            void Set(PoseJoint joint, float u, float v, float seen = 1) => p.Set(joint, new Vector2(u + Jitter(), v + Jitter()), seen);
            float floor = Floor + lift, ankle = floor + tiptoe, hip = ankle + Leg * (1 - depth);
            float knee = Mathf.Lerp(ankle + Shin, ankle + .01f, kneel);
            float shoulder = hip + Torso * (1 - bend), head = shoulder + .1f * (1 - bend * .5f);
            float dx = x - .5f;
            Set(PoseJoint.Nose, x, head);
            Set(PoseJoint.LeftEye, x - .015f, head + .015f); Set(PoseJoint.RightEye, x + .015f, head + .015f);
            Set(PoseJoint.LeftEar, x - .03f, head + .005f); Set(PoseJoint.RightEar, x + .03f, head + .005f);
            Set(PoseJoint.LeftShoulder, x - .07f, shoulder); Set(PoseJoint.RightShoulder, x + .07f, shoulder);
            Set(PoseJoint.LeftElbow, x - .1f, shoulder - .1f); Set(PoseJoint.RightElbow, x + .1f, shoulder - .1f);
            // Wrists given for a standing child (shoulders at .72) move with the body.
            float rise = shoulder - LeftShoulder.y;
            var lw = leftWrist ?? new Vector2(.39f, LeftShoulder.y - .2f); var rw = rightWrist ?? new Vector2(.61f, RightShoulder.y - .2f);
            Set(PoseJoint.LeftWrist, lw.x + dx, lw.y + rise); Set(PoseJoint.RightWrist, rw.x + dx, rw.y + rise);
            Set(PoseJoint.LeftHip, x - .045f, hip); Set(PoseJoint.RightHip, x + .045f, hip);
            Set(PoseJoint.LeftKnee, x - .05f, knee + footUp); Set(PoseJoint.RightKnee, x + .05f, knee);
            float seenFeet = feet ? 1 : 0, seenToes = feet && toes ? 1 : 0;
            Set(PoseJoint.LeftAnkle, x - .05f, ankle + footUp, seenFeet); Set(PoseJoint.RightAnkle, x + .05f, ankle, seenFeet);
            Set(PoseJoint.LeftHeel, x - .05f, ankle + footUp - .01f, seenToes); Set(PoseJoint.RightHeel, x + .05f, ankle - .01f, seenToes);
            // Toes stay on the floor on tiptoe.
            Set(PoseJoint.LeftToe, x - .06f, floor + footUp - .02f, seenToes); Set(PoseJoint.RightToe, x + .06f, floor - .02f, seenToes);
            p.DeriveLimbs();
            return p;
        }

        // Wrist on a circle round a shoulder: angle in degrees, counterclockwise on screen, 0 = to the right.
        public static Vector2 Around(Vector2 shoulder, float degrees, float radius = .19f)
        {
            float a = degrees * Mathf.Deg2Rad;
            return shoulder + new Vector2(Mathf.Cos(a) * radius / Aspect, Mathf.Sin(a) * radius);
        }

        // A standing child turned by yaw degrees (negative = to their own left, -180/180 = back to the camera),
        // seen by an orthographic camera. headYaw turns only the head. depth: the model gives z (BlazePose);
        // swapAtBack: the model swaps left and right labels when the child faces away; farShoulder: the
        // shoulder behind the body is still found in a side view.
        public static PoseResult Turned(float yaw, float headYaw = 0, bool depth = true, bool swapAtBack = false, bool farShoulder = true,
            float noise = 0, System.Random random = null)
        {
            var p = new PoseResult { found = true, aspect = Aspect, hasDepth = depth };
            float Jitter() => random == null ? 0 : ((float)random.NextDouble() * 2 - 1) * noise;
            // Turning left by a: forward = (-sin a, 0, cos a), right = (cos a, 0, sin a); x to screen right,
            // z toward the camera, in height units.
            float a = -yaw * Mathf.Deg2Rad, h = -(yaw + headYaw) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            void Put(PoseJoint joint, float bx, float bz, float v, float seen = 1, bool head = false)
            {
                float c = head ? Mathf.Cos(h) : cos, s = head ? Mathf.Sin(h) : sin;
                float x = bx * c - bz * s, z = bx * s + bz * c;
                p.Set(joint, new Vector2(.5f + x / Aspect + Jitter(), v + Jitter()), seen, depth ? -z / Aspect : 0);
            }
            float face = Mathf.Cos(h), side = Mathf.Sin(h);
            Put(PoseJoint.Nose, 0, .06f, .84f, face > -.2f ? 1 : .1f, true);
            Put(PoseJoint.LeftEye, -.025f, .045f, .855f, face > .25f ? 1 : .1f, true); Put(PoseJoint.RightEye, .025f, .045f, .855f, face > .25f ? 1 : .1f, true);
            Put(PoseJoint.LeftEar, -.05f, 0, .845f, side < .3f ? 1 : .1f, true); Put(PoseJoint.RightEar, .05f, 0, .845f, side > -.3f ? 1 : .1f, true);
            float far = farShoulder ? 1 : .2f;
            Put(PoseJoint.LeftShoulder, -.09f, 0, .72f, sin > .85f ? far : 1); Put(PoseJoint.RightShoulder, .09f, 0, .72f, sin < -.85f ? far : 1);
            Put(PoseJoint.LeftElbow, -.12f, 0, .62f); Put(PoseJoint.RightElbow, .12f, 0, .62f);
            Put(PoseJoint.LeftWrist, -.13f, 0, .52f); Put(PoseJoint.RightWrist, .13f, 0, .52f);
            Put(PoseJoint.LeftHip, -.06f, 0, .48f); Put(PoseJoint.RightHip, .06f, 0, .48f);
            Put(PoseJoint.LeftKnee, -.06f, .01f, .3f); Put(PoseJoint.RightKnee, .06f, .01f, .3f);
            Put(PoseJoint.LeftAnkle, -.06f, 0, .12f); Put(PoseJoint.RightAnkle, .06f, 0, .12f);
            Put(PoseJoint.LeftHeel, -.06f, -.02f, .11f); Put(PoseJoint.RightHeel, .06f, -.02f, .11f);
            Put(PoseJoint.LeftToe, -.07f, .05f, .1f); Put(PoseJoint.RightToe, .07f, .05f, .1f);
            // Seen from behind, some models label the child's left as right.
            if (swapAtBack && cos < -.5f)
                for (int j = (int)PoseJoint.LeftEye; j < PoseResult.JointCount; j += 2)
                {
                    var (pa, va, da) = (p.points[j], p.visibility[j], p.depth[j]);
                    p.Set((PoseJoint)j, p.points[j + 1], p.visibility[j + 1], p.depth[j + 1]);
                    p.Set((PoseJoint)(j + 1), pa, va, da);
                }
            p.DeriveLimbs();
            return p;
        }

        // A frame where the model found nobody.
        public static PoseResult Missing() => new PoseResult { found = false };
    }
}
