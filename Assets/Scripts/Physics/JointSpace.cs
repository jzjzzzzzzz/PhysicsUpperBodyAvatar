// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using UnityEngine;

namespace ALS.HDT
{
    public static class JointSpace
    {
        public static Quaternion Basis(ConfigurableJoint joint)
        {
            Vector3 right = joint.axis.normalized;
            Vector3 forward = Vector3.Cross(right, joint.secondaryAxis).normalized;
            return Quaternion.LookRotation(forward, Vector3.Cross(forward, right).normalized);
        }

        // PhysX expects the inverse rotation error expressed in the joint's basis.
        // The desired rotation is relative to connectedBody, not Transform.parent.
        public static Quaternion TargetRotation(Quaternion desiredRelative, Quaternion bindRelative, Quaternion basis)
            => Quaternion.Inverse(basis) * Quaternion.Inverse(desiredRelative) * bindRelative * basis;

        public static Quaternion Compose(Vector3 degrees)
            => Quaternion.AngleAxis(degrees.z, Vector3.forward)
             * Quaternion.AngleAxis(degrees.y, Vector3.up)
             * Quaternion.AngleAxis(degrees.x, Vector3.right);

        public static Vector3 SignedEuler(Quaternion value)
        {
            // ZYX decomposition matches Compose, unlike Unity's ZXY eulerAngles.
            // In particular a flexed elbow must read -132, not (-48,180,180).
            var q = value.normalized;
            return new Vector3(
                Mathf.Atan2(2 * (q.w * q.x + q.y * q.z), 1 - 2 * (q.x * q.x + q.y * q.y)),
                Mathf.Asin(Mathf.Clamp(2 * (q.w * q.y - q.z * q.x), -1, 1)),
                Mathf.Atan2(2 * (q.w * q.z + q.x * q.y), 1 - 2 * (q.y * q.y + q.z * q.z))) * Mathf.Rad2Deg;
        }

        public static Vector3 RotationVector(Quaternion value)
        {
            if (value.w < 0) value = new Quaternion(-value.x, -value.y, -value.z, -value.w);
            value.ToAngleAxis(out float degrees, out Vector3 axis);
            return degrees < .001f || !float.IsFinite(axis.x) ? Vector3.zero : axis * (Mathf.DeltaAngle(0, degrees) * Mathf.Deg2Rad);
        }
    }
}
