// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ALS.HDT.Tests
{
    public sealed class CommandProtocolTests
    {
        [TestCase("handshake", GestureType.Handshake)]
        [TestCase("WAVE", GestureType.Wave)]
        [TestCase("double_thumbs_up", GestureType.DoubleThumbsUp)]
        [TestCase("idle", GestureType.Idle)]
        public void ParsesGesture(string action, GestureType expected)
        {
            var command = AvatarCommandProtocol.Parse("{\"action\":\"" + action + "\"}");
            Assert.That(command.kind, Is.EqualTo(AvatarCommandKind.Gesture));
            Assert.That(command.gesture, Is.EqualTo(expected));
        }

        [TestCase(0f)] [TestCase(1.2f)] [TestCase(15f)]
        public void AcceptsDurationBoundary(float duration)
        {
            var payload = JsonUtility.ToJson(new AvatarCommand { action = "wave", duration = duration });
            Assert.That(AvatarCommandProtocol.Parse(payload).duration, Is.EqualTo(duration));
        }

        [TestCase(-1f)] [TestCase(.1f)] [TestCase(16f)]
        public void RejectsDurationOutsideContract(float duration)
        {
            var payload = JsonUtility.ToJson(new AvatarCommand { action = "wave", duration = duration });
            Assert.Throws<ArgumentException>(() => AvatarCommandProtocol.Parse(payload));
        }

        [TestCase("")] [TestCase("{}")] [TestCase("not-json")]
        [TestCase("{\"action\":\"unknown\"}")] [TestCase("{\"action\":\"servo_state\",\"side\":\"other\"}")]
        public void RejectsInvalidCommand(string payload)
            => Assert.Throws<ArgumentException>(() => AvatarCommandProtocol.Parse(payload));

        [TestCase("left", BodySide.Left)] [TestCase("right", BodySide.Right)]
        public void ParsesServoSide(string side, BodySide expected)
        {
            var payload = JsonUtility.ToJson(new AvatarCommand { action = "servo_pose", side = side, values = new float[13] });
            Assert.That(AvatarCommandProtocol.Parse(payload).bodySide, Is.EqualTo(expected));
        }

        [TestCase(0)] [TestCase(12)] [TestCase(14)]
        public void RejectsIncompleteServoPacket(int length)
        {
            var payload = JsonUtility.ToJson(new AvatarCommand { action = "servo_pose", side = "right", values = new float[length] });
            Assert.Throws<ArgumentException>(() => AvatarCommandProtocol.Parse(payload));
        }

        [Test]
        public void CancelledAndExpiredCommandsCannotExecuteLater()
        {
            var expired = new PendingAvatarCommand("{}", TimeSpan.Zero);
            Assert.That(expired.TryBegin(), Is.False);
            var cancelled = new PendingAvatarCommand("{}", TimeSpan.FromSeconds(1));
            cancelled.Cancel();
            Assert.That(cancelled.TryBegin(), Is.False);
            Assert.That(cancelled.Wait(0), Does.Contain("expired"));
        }

        [Test]
        public void CommandCompletesOnlyOnce()
        {
            var pending = new PendingAvatarCommand("{}", TimeSpan.FromSeconds(1));
            Assert.That(pending.TryBegin(), Is.True);
            Assert.That(pending.TryBegin(), Is.False);
            pending.Complete("first"); pending.Complete("second");
            Assert.That(pending.Wait(0), Is.EqualTo("first"));
        }

        [Test]
        public void StoppedTransportReleasesItsPort()
        {
            int port;
            using (var server = new LocalCommandTransport(0)) port = server.Port;
            using (var restarted = new LocalCommandTransport(port)) Assert.That(restarted.Port, Is.EqualTo(port));
        }
    }

    public sealed class ServoRigTests
    {
        private ServoRigProfile profile;
        [SetUp] public void SetUp() => profile = ServoRigProfile.CreateDefault(BodySide.Right);
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(profile);

        private static IEnumerable<TestCaseData> Channels()
            => Enumerable.Range(0, ServoRigSchema.ChannelCount).SelectMany(i => new[] {
                new TestCaseData(i, false), new TestCaseData(i, true) });

        [TestCaseSource(nameof(Channels))]
        public void EveryChannelRoundTripsWithEitherDirection(int index, bool reverse)
        {
            var channel = profile.channels[index]; channel.reversed = reverse;
            float value = Mathf.Lerp(channel.inputMin, channel.inputMax, .37f);
            Assert.That(channel.Encode(channel.Decode(value)), Is.EqualTo(value).Within(.001f));
        }

        [Test]
        public void ChannelOrderAndFunctionsAreStable()
        {
            profile.Validate();
            Assert.That(profile.channels.Length, Is.EqualTo(13));
            for (int i = 0; i < 13; i++) {
                Assert.That(profile.channels[i].id, Is.EqualTo(ServoRigSchema.Id(i)));
                Assert.That((int)profile.channels[i].function, Is.EqualTo(i));
            }
        }

        [Test]
        public void InvalidSideAndCalibrationAreRejected()
        {
            Assert.Throws<ArgumentException>(() => ServoRigProfile.CreateDefault(BodySide.None));
            profile.channels[0].targetMax = float.NaN;
            Assert.Throws<ArgumentException>(() => profile.Validate());
        }

        [Test]
        public void InvalidChannelIdAndFingerSynergyAreRejected()
        {
            profile.channels[0].id = "H6";
            Assert.Throws<ArgumentException>(() => profile.Validate());
            profile.channels[0].id = "A1"; profile.thumbFlexion.x = 90;
            Assert.Throws<ArgumentException>(() => profile.Validate());
        }

        [Test]
        public void DecodeRejectsInvalidInputs()
        {
            Assert.Throws<ArgumentException>(() => profile.Decode(new float[12]));
            var invalid = new float[13]; invalid[0] = float.NaN;
            Assert.Throws<ArgumentException>(() => profile.Decode(invalid));
            invalid[0] = 181;
            Assert.Throws<ArgumentException>(() => profile.Decode(invalid));
        }

        [Test]
        public void NullPoseIsRejected()
            => Assert.Throws<ArgumentNullException>(() => profile.Encode(null));

        [Test]
        public void InputArraysAreIndependentAndWristHasTwoTargets()
        {
            var input = profile.Encode(new ArmPose { fingerCurls = new[] { 1f, 0f, .5f, 0f }, wrist = new Vector3(10, 12, -5) });
            var pose = profile.Decode(input);
            Assert.That(pose.FingerCurl(0), Is.EqualTo(1));
            Assert.That(pose.FingerCurl(1), Is.Zero);
            Assert.That(pose.FingerCurl(2), Is.EqualTo(.5f).Within(.001f));
            Assert.That(pose.wrist.y, Is.Zero);
            var copy = pose.Copy(); copy.fingerCurls[0] = 0;
            Assert.That(pose.FingerCurl(0), Is.EqualTo(1));
        }
    }
}
