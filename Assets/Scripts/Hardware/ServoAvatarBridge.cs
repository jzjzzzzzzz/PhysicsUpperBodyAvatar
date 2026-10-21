using System;
using UnityEngine;

namespace ALS.HDT
{
    [RequireComponent(typeof(AvatarController))]
    public class ServoAvatarBridge : MonoBehaviour
    {
        public ServoRigProfile leftProfile, rightProfile;
        public bool enforceSevenArmSixHand = true;
        private float[] leftCurrent, rightCurrent, leftGoal, rightGoal;
        public bool LeftOverride => leftGoal != null;
        public bool RightOverride => rightGoal != null;
        public string Mode => LeftOverride || RightOverride ? "SERVO INPUT / SIMULATION ONLY" : "GESTURES / 7 ARM + 6 HAND";
        private AvatarController avatar;
        public void Initialize(AvatarController owner) { avatar = owner; leftProfile.Validate(); rightProfile.Validate(); }
        public ServoRigProfile Profile(BodySide side) => side == BodySide.Left ? leftProfile : rightProfile;

        public void SetPose(BodySide side, float[] values)
        {
            if (side == BodySide.None) throw new ArgumentException("side must be left or right.");
            var profile = Profile(side);
            if (values == null || values.Length != 13) throw new ArgumentException("values must contain exactly 13 angles in A1..A7,H1..H6 order.");
            // Validate the entire packet before mutating any live target.
            for (int i = 0; i < 13; i++) if (!float.IsFinite(values[i]) || values[i] < profile.channels[i].inputMin || values[i] > profile.channels[i].inputMax)
                throw new ArgumentException("Input out of calibrated range at " + profile.channels[i].id);
            if (!LeftOverride && !RightOverride) avatar.gestures.HoldPose(ResolvedPose());
            if (side == BodySide.Left) { leftCurrent ??= profile.Encode(avatar.gestures.Pose.left); leftGoal = (float[])values.Clone(); }
            else { rightCurrent ??= profile.Encode(avatar.gestures.Pose.right); rightGoal = (float[])values.Clone(); }
        }

        public float[] Targets(BodySide side)
        {
            var goal = side == BodySide.Left ? leftGoal : rightGoal;
            return goal != null ? (float[])goal.Clone() : Profile(side).Encode(side == BodySide.Left ? avatar.gestures.Pose.left : avatar.gestures.Pose.right);
        }
        public void Clear() { leftCurrent = rightCurrent = leftGoal = rightGoal = null; }
        public UpperBodyPose ResolvedPose() => new UpperBodyPose {
            left = Resolve(BodySide.Left, avatar.gestures.Pose.left), right = Resolve(BodySide.Right, avatar.gestures.Pose.right) };
        public void ReleaseForGesture()
        {
            if (LeftOverride || RightOverride) { var pose = ResolvedPose(); Clear(); avatar.gestures.HoldPose(pose); }
        }
        public void Step(float dt) { Advance(leftCurrent, leftGoal, leftProfile, dt); Advance(rightCurrent, rightGoal, rightProfile, dt); }
        private static void Advance(float[] current, float[] goal, ServoRigProfile profile, float dt)
        {
            if (goal == null) return;
            for (int i = 0; i < 13; i++) current[i] = Mathf.MoveTowards(current[i], goal[i], profile.channels[i].maxInputDegreesPerSecond * dt);
        }
        public ArmPose Resolve(BodySide side, ArmPose gesture)
        {
            var current = side == BodySide.Left ? leftCurrent : rightCurrent;
            if (current != null) return Profile(side).Decode(current);
            if (!enforceSevenArmSixHand) return gesture;
            // Project gestures into the exact same 13 logical channels. There is
            // no hidden eighth arm actuator or extra independently commanded digit.
            return Profile(side).Decode(Profile(side).Encode(gesture));
        }
        public float Flexion(BodySide side, bool thumb, int phalanx)
        {
            var profile = Profile(side); return (thumb ? profile.thumbFlexion : profile.fingerFlexion)[phalanx];
        }
    }
}
