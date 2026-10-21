// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;
using UnityEngine;

namespace ALS.HDT
{
    // Logical order, NOT a claim about the physical controller's port numbers.
    public enum ServoFunction { ShoulderFlexion, ShoulderRotation, ShoulderAbduction, ElbowFlexion,
        ForearmRotation, WristFlexion, WristDeviation, IndexCurl, MiddleCurl, RingCurl, LittleCurl, ThumbCurl, ThumbSpread }

    [Serializable]
    public class ServoChannel
    {
        public string id, label;
        public ServoFunction function;
        public int controllerPort = -1;
        public float inputMin = 0, inputMax = 180;
        public bool reversed;
        public float targetMin, targetMax;
        public float maxInputDegreesPerSecond = 180;

        public float Decode(float input)
        {
            float u = Mathf.InverseLerp(inputMin, inputMax, input);
            return Mathf.Lerp(targetMin, targetMax, reversed ? 1 - u : u);
        }
        public float Encode(float target)
        {
            float u = Mathf.InverseLerp(targetMin, targetMax, target);
            return Mathf.Lerp(inputMin, inputMax, reversed ? 1 - u : u);
        }
    }

}
