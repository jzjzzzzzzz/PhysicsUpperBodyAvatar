// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;

namespace ALS.HDT
{
    /// <summary>Scene mutations belong on Unity's main thread, never on a socket worker.</summary>
    public sealed class AvatarCommandDispatcher
    {
        private readonly AvatarController avatar;

        public AvatarCommandDispatcher(AvatarController avatar)
        {
            this.avatar = avatar ? avatar : throw new ArgumentNullException(nameof(avatar));
        }

        public AvatarCommandReply Execute(string payload)
        {
            var reply = new AvatarCommandReply();
            try {
                var command = AvatarCommandProtocol.Parse(payload);
                if (command.kind == AvatarCommandKind.Gesture) {
                    avatar.PlayGesture(command.gesture, command.duration);
                } else {
                    var bridge = avatar.servoBridge;
                    if (!bridge) throw new ArgumentException("Servo bridge unavailable.");
                    if (command.kind == AvatarCommandKind.ServoPose) bridge.SetPose(command.bodySide, command.values);
                    reply.side = command.side;
                    reply.mode = bridge.Mode;
                    reply.targetValues = bridge.Targets(command.bodySide);
                    reply.hardwareCalibrated = bridge.Profile(command.bodySide).hardwareCalibrated;
                }
                reply.accepted = true;
                reply.action = command.action;
                reply.state = avatar.gestures.State.ToString();
            } catch (ArgumentException error) {
                reply.error = error.Message;
            }
            return reply;
        }
    }
}
