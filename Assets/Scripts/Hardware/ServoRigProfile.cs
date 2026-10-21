// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;
using UnityEngine;

namespace ALS.HDT
{
    [CreateAssetMenu(menuName = "ALS HDT/13-channel Servo Rig")]
    public class ServoRigProfile : ScriptableObject
    {
        public string schema = "als-hdt-servo-rig-v1";
        [TextArea] public string calibrationNotes = "PROVISIONAL: 7 arm + 6 hand per side. Port IDs, endpoint angles, direction, handedness and linkage ratios are not measured from hardware. 0..180 is a virtual input example, not a verified servo travel range.";
        public bool hardwareCalibrated;
        public BodySide side;
        public ServoChannel[] channels;
        [Tooltip("Desired flexion degrees per unit curl; each ray remains physically articulated.")]
        public Vector3 fingerFlexion = new Vector3(75, 85, 55);
        public Vector3 thumbFlexion = new Vector3(45, 55, 55);

        public static ServoRigProfile CreateDefault(BodySide side)
        {
            ServoRigSchema.RequireSide(side);
            var result = CreateInstance<ServoRigProfile>();
            result.side = side;
            result.channels = ServoRigSchema.CreateChannels();
            return result;
        }

        public void Validate()
        {
            ServoRigSchema.RequireSide(side);
            if (channels == null || channels.Length != ServoRigSchema.ChannelCount)
                throw new ArgumentException("Exactly 13 ordered channels are required.");
            for (int i = 0; i < channels.Length; i++) {
                var c = channels[i];
                if (c == null || c.id != ServoRigSchema.Id(i) || c.function != (ServoFunction)i
                    || !float.IsFinite(c.inputMin) || !float.IsFinite(c.inputMax)
                    || !float.IsFinite(c.targetMin) || !float.IsFinite(c.targetMax)
                    || !float.IsFinite(c.maxInputDegreesPerSecond) || c.inputMax <= c.inputMin
                    || c.targetMax <= c.targetMin || c.maxInputDegreesPerSecond <= 0
                    || c.targetMin < ServoRigSchema.Minimum(i) || c.targetMax > ServoRigSchema.Maximum(i))
                    throw new ArgumentException("Invalid channel calibration at index " + i);
            }
            for (int i = 0; i < 3; i++)
                if (!float.IsFinite(fingerFlexion[i]) || fingerFlexion[i] < 0 || fingerFlexion[i] > 100
                    || !float.IsFinite(thumbFlexion[i]) || thumbFlexion[i] < 0 || thumbFlexion[i] > (i == 0 ? 70 : 100))
                    throw new ArgumentException("Finger synergy exceeds joint limits.");
        }

        public void ValidateInput(float[] values)
        {
            if (values == null || values.Length != ServoRigSchema.ChannelCount)
                throw new ArgumentException("values must contain exactly 13 angles in A1..A7,H1..H6 order.");
            for (int i = 0; i < values.Length; i++)
                if (!float.IsFinite(values[i]) || values[i] < channels[i].inputMin || values[i] > channels[i].inputMax)
                    throw new ArgumentException("Input out of calibrated range at " + channels[i].id);
        }

        public float[] Encode(ArmPose pose)
        {
            if (pose == null) throw new ArgumentNullException(nameof(pose));
            float[] logical = { pose.shoulder.x, pose.shoulder.y, pose.shoulder.z, pose.elbow, pose.pronation,
                pose.wrist.x, pose.wrist.z, pose.FingerCurl(0), pose.FingerCurl(1), pose.FingerCurl(2), pose.FingerCurl(3), pose.thumbCurl, pose.thumbSpread };
            var result = new float[ServoRigSchema.ChannelCount]; for (int i = 0; i < ServoRigSchema.ChannelCount; i++) result[i] = channels[i].Encode(logical[i]); return result;
        }
        public ArmPose Decode(float[] values)
        {
            ValidateInput(values);
            var q = new float[ServoRigSchema.ChannelCount]; for (int i = 0; i < ServoRigSchema.ChannelCount; i++) q[i] = channels[i].Decode(values[i]);
            return new ArmPose { shoulder = new Vector3(q[0], q[1], q[2]), elbow = q[3], pronation = q[4],
                wrist = new Vector3(q[5], 0, q[6]), fingerCurls = new[] { q[7], q[8], q[9], q[10] }, thumbCurl = q[11], thumbSpread = q[12] };
        }
    }
}
