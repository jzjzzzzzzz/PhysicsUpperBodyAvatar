using UnityEngine;

namespace ALS.HDT
{
    public class GesturePlayer : MonoBehaviour
    {
        public GestureLibrary library;
        public GestureType CurrentGesture { get; private set; }
        public GestureState State { get; private set; }
        public UpperBodyPose Pose { get; private set; }
        public float Progress => activeDuration > 0 ? Mathf.Clamp01(elapsed / activeDuration) : 0;
        private UpperBodyPose start;
        private GestureDefinition active;
        private GestureType pending;
        private float pendingDuration, elapsed, activeDuration;
        private bool explicitReturn;

        public void HoldPose(UpperBodyPose pose)
        {
            Pose = pose.Copy(); start = Pose.Copy(); CurrentGesture = GestureType.Idle;
            State = GestureState.Idle; pending = GestureType.Idle; elapsed = 0; activeDuration = 0; explicitReturn = false;
        }

        public void Initialize()
        {
            Pose = library.idle.Copy();
            start = Pose.Copy();
            CurrentGesture = GestureType.Idle;
            State = GestureState.Idle;
            pending = GestureType.Idle; elapsed = 0; activeDuration = 0; explicitReturn = false;
        }

        public void Play(GestureType gesture, float duration = 0)
        {
            if (Pose == null) Initialize();
            if (gesture == GestureType.Idle) { ReturnToIdle(); return; }
            if (gesture == CurrentGesture && State != GestureState.Returning) return;
            if (State != GestureState.Idle) {
                pending = gesture; pendingDuration = duration;
                BeginReturn(); return;
            }
            active = library.Find(gesture);
            if (active == null) return;
            start = Pose.Copy();
            activeDuration = duration > 0 ? duration : active.duration;
            elapsed = 0; explicitReturn = false;
            CurrentGesture = gesture; State = GestureState.BlendingIn;
        }

        public void ReturnToIdle() { pending = GestureType.Idle; BeginReturn(); }
        private void BeginReturn()
        {
            if (Pose == null) Initialize();
            start = Pose.Copy(); elapsed = 0; activeDuration = library.returnDuration;
            explicitReturn = true; State = GestureState.Returning;
        }

        public void Step(float dt)
        {
            if (Pose == null) Initialize();
            if (State == GestureState.Idle) return;
            elapsed += dt;
            float u = Mathf.Clamp01(elapsed / activeDuration);
            if (explicitReturn) Pose = UpperBodyPose.Blend(start, library.idle, Mathf.SmoothStep(0, 1, u));
            else {
                var desired = active.pose.Copy();
                float work = Mathf.Clamp01((u - active.blendFraction) / (1 - 2 * active.blendFraction));
                float oscillation = Mathf.Sin(work * active.cycles * 2 * Mathf.PI);
                if (CurrentGesture == GestureType.Wave) {
                    desired.right.wrist.z += active.amplitude * oscillation;
                    desired.right.shoulder.z += .16f * active.amplitude * oscillation;
                }
                else if (CurrentGesture == GestureType.Handshake) desired.right.shoulder.x += active.amplitude * oscillation;
                if (u < active.blendFraction) {
                    State = GestureState.BlendingIn;
                    Pose = UpperBodyPose.Blend(start, desired, Mathf.SmoothStep(0, 1, u / active.blendFraction));
                } else if (u > 1 - active.blendFraction) {
                    State = GestureState.Returning;
                    Pose = UpperBodyPose.Blend(desired, library.idle, Mathf.SmoothStep(0, 1, (u - 1 + active.blendFraction) / active.blendFraction));
                } else { State = GestureState.Performing; Pose = desired; }
            }
            if (u >= 1) {
                Pose = library.idle.Copy(); CurrentGesture = GestureType.Idle; State = GestureState.Idle;
                var next = pending; float duration = pendingDuration; pending = GestureType.Idle;
                if (next != GestureType.Idle) Play(next, duration);
            }
        }
    }
}
