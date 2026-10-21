// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;
using UnityEngine;

namespace ALS.HDT
{
    public enum AvatarCommandKind { Gesture, ServoPose, ServoState }

    [Serializable]
    public sealed class AvatarCommand
    {
        public string action, side;
        public float duration;
        public float[] values;
        [NonSerialized] public AvatarCommandKind kind;
        [NonSerialized] public GestureType gesture;
        [NonSerialized] public BodySide bodySide;
    }

    [Serializable]
    public sealed class AvatarCommandReply
    {
        public bool accepted;
        public string action = "", state = "", error = "", side = "", mode = "";
        public float[] targetValues = Array.Empty<float>();
        public bool hardwareCalibrated;
    }

    /// <summary>Wire-format validation; deliberately independent of sockets and scene objects.</summary>
    public static class AvatarCommandProtocol
    {
        public const int MaximumFrameBytes = 8192;
        public const float MinimumDuration = 1.2f, MaximumDuration = 15;

        public static AvatarCommand Parse(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("Missing command.");
            var command = JsonUtility.FromJson<AvatarCommand>(payload);
            if (command == null || string.IsNullOrWhiteSpace(command.action)) throw new ArgumentException("Missing action.");
            command.action = command.action.ToLowerInvariant();
            if (command.action == "servo_pose" || command.action == "servo_state") {
                command.kind = command.action == "servo_pose" ? AvatarCommandKind.ServoPose : AvatarCommandKind.ServoState;
                command.bodySide = command.side == "left" ? BodySide.Left : command.side == "right" ? BodySide.Right : BodySide.None;
                if (command.bodySide == BodySide.None) throw new ArgumentException("side must be left or right.");
                if (command.kind == AvatarCommandKind.ServoPose) {
                    if (command.values == null || command.values.Length != ServoRigSchema.ChannelCount)
                        throw new ArgumentException("values must contain exactly 13 angles in A1..A7,H1..H6 order.");
                    foreach (float value in command.values)
                        if (!float.IsFinite(value)) throw new ArgumentException("Servo angles must be finite.");
                }
                return command;
            }
            command.kind = AvatarCommandKind.Gesture;
            if (!float.IsFinite(command.duration) || command.duration < 0 || command.duration > MaximumDuration
                || command.duration > 0 && command.duration < MinimumDuration)
                throw new ArgumentException("duration must be 0 (default) or between 1.2 and 15 seconds.");
            switch (command.action) {
                case "handshake": command.gesture = GestureType.Handshake; break;
                case "wave": command.gesture = GestureType.Wave; break;
                case "double_thumbs_up": command.gesture = GestureType.DoubleThumbsUp; break;
                case "idle": command.gesture = GestureType.Idle; break;
                default: throw new ArgumentException("Unknown action.");
            }
            return command;
        }
    }
}
