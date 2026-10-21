// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;
using UnityEngine;

namespace ALS.HDT
{
    public enum JointKind { Shoulder, Elbow, Pronation, Wrist, Finger, Thumb, Neck }
    public enum BodyRole { Torso, Neck, Head, UpperArm, Elbow, Forearm, Hand, Finger }
    public enum BodySide { None, Left, Right }

    [Serializable]
    public class JointTuning
    {
        public float lowX = -90, highX = 90, limitY = 60, limitZ = 60;
        public bool lockY, lockZ;
        [Tooltip("Angular spring in N m/rad, not an acceleration drive.")]
        public float stiffness = 80;
        [Tooltip("Angular damping in N m s/rad.")]
        public float damping = 8;
        [Tooltip("PhysX maximum motor effort per angular drive, N m.")]
        public float torqueLimit = 15;
    }

    [CreateAssetMenu(menuName = "ALS HDT/Physics Profile")]
    public class AvatarPhysicsProfile : ScriptableObject
    {
        [Header("Integration")]
        public float fixedTimeStep = 1f / 90f;
        public int solverIterations = 40, solverVelocityIterations = 20;
        public float linearDamping = .05f, angularDamping = .15f, maxAngularVelocity = 9;
        public float contactOffset = .003f;
        public float limitContactDegrees = 0;
        [Header("Anatomy in metres and kg")]
        public float shoulderHalfWidth = .22f, shoulderHeight = 1.50f;
        public float upperArmLength = .30f, forearmLength = .26f, palmLength = .115f;
        public float upperArmRadius = .049f, forearmRadius = .037f;
        public float upperArmMass = 1.85f, elbowMass = .12f, forearmMass = 1.05f, handMass = .32f;
        public float fingerMass = .008f, headMass = 4f;
        [Tooltip("Thumb CMC base belongs inside the palm, not beyond its edge (metres).")]
        public float thumbBaseLateral = .025f, thumbBaseDown = .020f;
        [Header("Actuation and anatomical limits")]
        public JointTuning shoulder = new JointTuning { lowX = -105, highX = 65, limitY = 100, limitZ = 112, stiffness = 145, damping = 14, torqueLimit = 34 };
        public JointTuning elbow = new JointTuning { lowX = -145, highX = 0, lockY = true, lockZ = true, stiffness = 85, damping = 7, torqueLimit = 18 };
        public JointTuning pronation = new JointTuning { lowX = -90, highX = 90, lockY = true, lockZ = true, stiffness = 22, damping = 2, torqueLimit = 5 };
        public JointTuning wrist = new JointTuning { lowX = -45, highX = 45, limitY = 15, limitZ = 28, stiffness = 28, damping = 2.8f, torqueLimit = 4 };
        public JointTuning finger = new JointTuning { lowX = -100, highX = 5, lockY = true, lockZ = true, stiffness = .30f, damping = .025f, torqueLimit = .09f };
        public JointTuning thumb = new JointTuning { lowX = -70, highX = 12, limitY = 20, limitZ = 40, stiffness = .35f, damping = .03f, torqueLimit = .12f };
        public JointTuning neck = new JointTuning { lowX = -22, highX = 22, limitY = 35, limitZ = 22, stiffness = 55, damping = 6, torqueLimit = 12 };

        public JointTuning Get(JointKind kind)
        {
            switch (kind)
            {
                case JointKind.Shoulder: return shoulder;
                case JointKind.Elbow: return elbow;
                case JointKind.Pronation: return pronation;
                case JointKind.Wrist: return wrist;
                case JointKind.Finger: return finger;
                case JointKind.Thumb: return thumb;
                default: return neck;
            }
        }
    }
}
