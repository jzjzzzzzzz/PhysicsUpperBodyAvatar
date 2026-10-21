using System.Linq;
using UnityEngine;

namespace ALS.HDT
{
    [DefaultExecutionOrder(-100)]
    public class AvatarController : MonoBehaviour
    {
        public AvatarPhysicsProfile profile;
        public GesturePlayer gestures;
        public PhysicsBone[] bones;
        public JointMotor[] motors;
        public SkinnedMeshRenderer visibleSkin;
        public ServoAvatarBridge servoBridge;
        [Range(0, 1)] public float motorStrength = 1;
        public bool IsPaused { get; private set; }
        public bool ExternalStepping { get; set; }
        public int PhysicsSteps { get; private set; }
        private bool initialized;

        void Awake() => Initialize();
        public void Initialize()
        {
            if (initialized) return;
            Time.fixedDeltaTime = profile.fixedTimeStep;
            Time.maximumDeltaTime = .10f;
            if (bones == null || bones.Length == 0) bones = GetComponentsInChildren<PhysicsBone>(true);
            if (motors == null || motors.Length == 0) motors = GetComponentsInChildren<JointMotor>(true);
            foreach (var bone in bones) {
                var rb = bone.body;
                rb.solverIterations = profile.solverIterations;
                rb.solverVelocityIterations = profile.solverVelocityIterations;
                rb.linearDamping = profile.linearDamping; rb.angularDamping = profile.angularDamping;
                rb.maxAngularVelocity = profile.maxAngularVelocity;
                // Manual Editor simulation has no rendered interpolation cycle.
                rb.interpolation = ExternalStepping ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = rb.isKinematic ? CollisionDetectionMode.Discrete : CollisionDetectionMode.ContinuousDynamic;
            }
            if (!servoBridge) servoBridge = GetComponent<ServoAvatarBridge>();
            if (servoBridge) servoBridge.Initialize(this);
            foreach (var motor in motors) motor.Initialize();
            ConfigureSelfCollisions();
            gestures.Initialize(); ApplyTargets();
            initialized = true;
        }

        private void ConfigureSelfCollisions()
        {
            for (int i = 0; i < bones.Length; i++) for (int j = i + 1; j < bones.Length; j++) {
                var a = bones[i]; var b = bones[j];
                bool sameHand = a.side != BodySide.None && a.side == b.side
                    && (a.role == BodyRole.Hand || a.role == BodyRole.Finger)
                    && (b.role == BodyRole.Hand || b.role == BodyRole.Finger);
                bool adjacent = a.joint && a.joint.connectedBody == b.body || b.joint && b.joint.connectedBody == a.body;
                bool elbowNeighbour = a.side == b.side && (a.role == BodyRole.Elbow || b.role == BodyRole.Elbow)
                    && a.role != BodyRole.Finger && b.role != BodyRole.Finger;
                // The zero-length elbow carrier separates flexion from pronation.
                // Upper arm and forearm are still anatomically adjacent, so their
                // rounded collider ends intentionally overlap and must not collide.
                bool sharedElbow = a.side == b.side && a.side != BodySide.None
                    && (a.role == BodyRole.UpperArm && b.role == BodyRole.Forearm
                        || b.role == BodyRole.UpperArm && a.role == BodyRole.Forearm);
                if (sameHand || adjacent || elbowNeighbour || sharedElbow) {
                    foreach (var ac in a.GetComponents<Collider>()) foreach (var bc in b.GetComponents<Collider>())
                        UnityEngine.Physics.IgnoreCollision(ac, bc, true);
                }
            }
        }

        void FixedUpdate() { if (!ExternalStepping) StepPhysics(profile.fixedTimeStep); }
        public void StepPhysics(float dt)
        {
            if (!initialized) Initialize();
            gestures.Step(dt); if (servoBridge) servoBridge.Step(dt); ApplyTargets();
            foreach (var motor in motors) motor.Step(motorStrength);
            PhysicsSteps++;
        }

        private void ApplyTargets()
        {
            var right = servoBridge ? servoBridge.Resolve(BodySide.Right, gestures.Pose.right) : gestures.Pose.right;
            var left = servoBridge ? servoBridge.Resolve(BodySide.Left, gestures.Pose.left) : gestures.Pose.left;
            foreach (var bone in bones) {
                if (!bone.target) continue;
                ArmPose arm = bone.side == BodySide.Right ? right : left;
                Quaternion delta = Quaternion.identity;
                switch (bone.role) {
                    case BodyRole.UpperArm: delta = JointSpace.Compose(arm.shoulder); break;
                    case BodyRole.Elbow: delta = Quaternion.AngleAxis(arm.elbow, Vector3.right); break;
                    case BodyRole.Forearm: delta = Quaternion.AngleAxis(arm.pronation, Vector3.up); break;
                    case BodyRole.Hand: delta = JointSpace.Compose(arm.wrist); break;
                    case BodyRole.Finger:
                        if (bone.fingerIndex == 4) {
                            float sign = bone.side == BodySide.Right ? 1 : -1;
                            delta = Quaternion.AngleAxis(-arm.thumbCurl * (servoBridge ? servoBridge.Flexion(bone.side, true, bone.phalanxIndex) : bone.phalanxIndex == 0 ? 45 : 55), Vector3.right);
                            if (bone.phalanxIndex == 0) delta = Quaternion.AngleAxis(sign * arm.thumbSpread, Vector3.forward) * delta;
                        } else delta = Quaternion.AngleAxis(-arm.FingerCurl(bone.fingerIndex) * (servoBridge ? servoBridge.Flexion(bone.side, false, bone.phalanxIndex) : bone.phalanxIndex == 0 ? 75 : bone.phalanxIndex == 1 ? 85 : 55), Vector3.right);
                        break;
                }
                // Only the NON-RENDERED target hierarchy is written here. Physical bones
                // are moved by PhysX through ConfigurableJoint drives, never by this code.
                bone.target.localRotation = bone.targetRest * delta;
            }
        }

        public void PlayGesture(GestureType type) => PlayGesture(type, 0);
        public void PlayGesture(GestureType type, float duration) { if (servoBridge) servoBridge.ReleaseForGesture(); gestures.Play(type, duration); }
        public void ReturnToIdle() { if (servoBridge) servoBridge.ReleaseForGesture(); gestures.ReturnToIdle(); }
        public PhysicsBone Find(string id) => bones.FirstOrDefault(b => b.id == id);
        public void TogglePause() { IsPaused = !IsPaused; Time.timeScale = IsPaused ? 0 : 1; }
        public void ApplyTestImpulse() { Find("R_Hand").body.AddForce(new Vector3(.4f, .12f, -.12f), ForceMode.Impulse); }

        public void ResetPhysics()
        {
            // Explicit diagnostic RESET is the only deliberate pose reset. It is never
            // called to perform gestures or to overcome contacts.
            foreach (var bone in bones) {
                // Flush interpolation history during this explicit diagnostic
                // teleport; stale render poses must not be synced back to PhysX.
                bone.body.interpolation = RigidbodyInterpolation.None;
                if (!bone.body.isKinematic) { bone.body.linearVelocity = Vector3.zero; bone.body.angularVelocity = Vector3.zero; }
                bone.transform.SetPositionAndRotation(bone.resetPosition, bone.resetRotation);
                bone.body.position = bone.resetPosition; bone.body.rotation = bone.resetRotation;
            }
            if (servoBridge) servoBridge.Clear();
            gestures.Initialize(); motorStrength = 1; ApplyTargets();
            UnityEngine.Physics.SyncTransforms();
            foreach (var bone in bones) {
                bone.body.interpolation = ExternalStepping ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
                if (!bone.body.isKinematic) bone.body.WakeUp();
            }
        }
        void OnDestroy() { if (Application.isPlaying) Time.timeScale = 1; }
    }
}
