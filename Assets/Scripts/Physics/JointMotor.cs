// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using UnityEngine;

namespace ALS.HDT
{
    [RequireComponent(typeof(PhysicsBone))]
    public class JointMotor : MonoBehaviour
    {
        public PhysicsBone bone;
        public AvatarPhysicsProfile profile;
        public Vector3 EstimatedTorque { get; private set; }
        public Vector3 MeasuredConstraintTorque => bone && bone.joint ? bone.joint.currentTorque : Vector3.zero;
        public Vector3 MeasuredConstraintForce => bone && bone.joint ? bone.joint.currentForce : Vector3.zero;
        private Quaternion basis;
        private JointTuning tuning;
        private PhysicsBone[] distalBodies;
        private Rigidbody distalActuator;

        public void Initialize()
        {
            if (!bone) bone = GetComponent<PhysicsBone>();
            tuning = profile.Get(bone.jointKind);
            var j = bone.joint;
            if (!j) return;
            basis = JointSpace.Basis(j);
            j.configuredInWorldSpace = false;
            j.swapBodies = false;
            j.rotationDriveMode = RotationDriveMode.XYAndZ;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = ConfigurableJointMotion.Limited;
            j.angularYMotion = tuning.lockY ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
            j.angularZMotion = tuning.lockZ ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
            var owner = GetComponentInParent<AvatarController>();
            if (bone.role == BodyRole.Hand && owner.servoBridge && owner.servoBridge.enforceSevenArmSixHand)
                j.angularYMotion = ConfigurableJointMotion.Locked;
            // ConfigurableJoint's relative twist limit has the opposite sign to
            // child-relative-to-parent anatomy. Keep profiles in anatomical angles.
            j.lowAngularXLimit = new SoftJointLimit { limit = -tuning.highX, contactDistance = profile.limitContactDegrees };
            j.highAngularXLimit = new SoftJointLimit { limit = -tuning.lowX, contactDistance = profile.limitContactDegrees };
            j.angularYLimit = new SoftJointLimit { limit = tuning.limitY, contactDistance = profile.limitContactDegrees };
            j.angularZLimit = new SoftJointLimit { limit = tuning.limitZ, contactDistance = profile.limitContactDegrees };
            j.enableCollision = false;
            j.enablePreprocessing = true;
            j.projectionMode = JointProjectionMode.None;
            j.targetAngularVelocity = Vector3.zero;
            if (bone.role == BodyRole.Elbow) {
                var avatar = GetComponentInParent<AvatarController>();
                if (avatar.bones != null) {
                    distalBodies = System.Array.FindAll(avatar.bones, b => b.side == bone.side
                        && (b.role == BodyRole.Forearm || b.role == BodyRole.Hand || b.role == BodyRole.Finger));
                    var forearm = System.Array.Find(distalBodies, b => b.role == BodyRole.Forearm);
                    if (forearm) distalActuator = forearm.body;
                }
            }
        }

        public void Step(float strength)
        {
            if (!bone || !bone.joint || !bone.target) return;
            if (tuning == null) Initialize();
            var j = bone.joint;
            if (bone.role == BodyRole.Elbow) {
                // A tiny intermediary separates elbow flexion from forearm roll.
                // Applying the flexion drive to that intermediary alone makes an
                // implicit PhysX drive see its tiny sphere inertia, not the arm's.
                // Instead apply a bounded equal/opposite joint torque pair to the
                // two physical links. The carrier still enforces the hinge limits.
                j.angularXDrive = j.angularYZDrive = new JointDrive();
                StepElbowTorque(strength);
                return;
            }
            var drive = new JointDrive {
                positionSpring = tuning.stiffness * strength,
                positionDamper = tuning.damping * Mathf.Sqrt(strength),
                maximumForce = tuning.torqueLimit * strength,
                useAcceleration = false
            };
            j.angularXDrive = j.angularYZDrive = drive;
            j.targetRotation = JointSpace.TargetRotation(bone.target.localRotation, bone.bindRelative, basis);
            Vector3 relativeOmega = bone.body.angularVelocity - (j.connectedBody ? j.connectedBody.angularVelocity : Vector3.zero);
            Quaternion worldError = (j.connectedBody ? j.connectedBody.rotation : Quaternion.identity)
                * bone.target.localRotation * Quaternion.Inverse(bone.body.rotation);
            Vector3 pd = tuning.stiffness * strength * JointSpace.RotationVector(worldError)
                - tuning.damping * Mathf.Sqrt(strength) * relativeOmega;
            // A diagnostic approximation only. currentTorque includes constraint reactions,
            // whereas this vector is NOT a readback of PhysX's motor impulse.
            EstimatedTorque = Vector3.ClampMagnitude(pd, tuning.torqueLimit * strength);
        }

        private void StepElbowTorque(float strength)
        {
            var proximal = bone.joint.connectedBody;
            Vector3 axis = proximal.rotation * bone.joint.axis;
            Quaternion q = Quaternion.Inverse(bone.bindRelative) * bone.CurrentRelative;
            Quaternion desired = Quaternion.Inverse(bone.bindRelative) * bone.target.localRotation;
            float angle = 2 * Mathf.Atan2(q.x, q.w) * Mathf.Rad2Deg;
            float target = 2 * Mathf.Atan2(desired.x, desired.w) * Mathf.Rad2Deg;
            float error = Mathf.DeltaAngle(angle, target) * Mathf.Deg2Rad;
            float omega = Vector3.Dot(distalActuator.angularVelocity - proximal.angularVelocity, axis);
            Vector3 anchor = bone.body.position;
            float distalInertia = 0;
            foreach (var b in distalBodies) distalInertia += InertiaAbout(b.body, axis, anchor);
            float inverseInertia = 1 / Mathf.Max(.0001f, distalInertia)
                + 1 / Mathf.Max(.0001f, InertiaAbout(proximal, axis, anchor));
            float k = tuning.stiffness * strength, d = tuning.damping * Mathf.Sqrt(strength);
            float dt = profile.fixedTimeStep;
            // Stable implicit-PD discretisation before the physical torque cap.
            float torque = (k * error - d * omega) / (1 + (d * dt + k * dt * dt) * inverseInertia);
            torque = Mathf.Clamp(torque, -tuning.torqueLimit * strength, tuning.torqueLimit * strength);
            EstimatedTorque = axis * torque;
            distalActuator.AddTorque(EstimatedTorque, ForceMode.Force);
            if (!proximal.isKinematic) proximal.AddTorque(-EstimatedTorque, ForceMode.Force);
        }

        private static float InertiaAbout(Rigidbody body, Vector3 axis, Vector3 anchor)
        {
            Vector3 localAxis = Quaternion.Inverse(body.rotation * body.inertiaTensorRotation) * axis;
            float rotational = Vector3.Dot(Vector3.Scale(localAxis, localAxis), body.inertiaTensor);
            return rotational + body.mass * Vector3.Cross(body.worldCenterOfMass - anchor, axis).sqrMagnitude;
        }
    }
}
