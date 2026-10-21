// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;

namespace ALS.HDT
{
    /// <summary>Single source of truth for logical channel order and prototype anatomy.</summary>
    public static class ServoRigSchema
    {
        public const int ChannelCount = 13;
        private static readonly string[] Ids = { "A1", "A2", "A3", "A4", "A5", "A6", "A7", "H1", "H2", "H3", "H4", "H5", "H6" };
        private static readonly string[] Labels = { "Shoulder flex", "Shoulder rotate", "Shoulder raise", "Elbow flex", "Forearm rotate", "Wrist flex", "Wrist deviate", "Index curl", "Middle curl", "Ring curl", "Little curl", "Thumb curl", "Thumb spread" };
        private static readonly float[] Minima = { -105, -100, -112, -145, -90, -45, -28, 0, 0, 0, 0, 0, -35 };
        private static readonly float[] Maxima = { 65, 100, 112, 0, 90, 45, 28, 1, 1, 1, 1, 1, 35 };

        public static string Id(int index) => Ids[index];
        public static float Minimum(int index) => Minima[index];
        public static float Maximum(int index) => Maxima[index];
        public static void RequireSide(BodySide side)
        {
            if (side != BodySide.Left && side != BodySide.Right) throw new ArgumentException("side must be left or right.");
        }
        public static ServoChannel[] CreateChannels()
        {
            var result = new ServoChannel[ChannelCount];
            for (int i = 0; i < result.Length; i++) result[i] = new ServoChannel {
                id = Ids[i], label = Labels[i], function = (ServoFunction)i, targetMin = Minima[i], targetMax = Maxima[i]
            };
            return result;
        }
    }
}
