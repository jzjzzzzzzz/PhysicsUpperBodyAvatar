// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ALS.HDT.Editor
{
    public static class PrototypeValidation
    {
        [Serializable] private class Result { public string name; public bool passed; public string detail; }
        [Serializable] private class Report { public string unityVersion; public string mode; public bool allPassed; public List<Result> tests = new List<Result>(); }
        private static AvatarController avatar;
        private static InteractionLab lab;
        private static Report report;
        private static float dt, maximumAnchor, maximumSpeed, elbowLimitViolation;
        private static string maximumAnchorBone;

        public static void RebuildAndRun() { PrototypeBuilder.CreateDemo(); Run(); }
        public static void ValidateAndBuildMac() { RebuildAndRun(); PrototypeBuilder.BuildMac(); }

        public static void Run()
        {
            Directory.CreateDirectory("TestResults");
            maximumAnchor = maximumSpeed = elbowLimitViolation = 0;
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            avatar = UnityEngine.Object.FindAnyObjectByType<AvatarController>();
            lab = UnityEngine.Object.FindAnyObjectByType<InteractionLab>();
            report = new Report { unityVersion = Application.unityVersion, mode = "Unity Editor / PhysX manual simulation; no rendering or play-mode collision callbacks" };
            var previous = UnityEngine.Physics.simulationMode;
            try {
                UnityEngine.Physics.simulationMode = SimulationMode.Script;
                avatar.ExternalStepping = true; avatar.Initialize(); dt = avatar.profile.fixedTimeStep;
                Check("Structure", avatar.bones.Length == 41 && avatar.motors.Length == 39
                    && avatar.bones.Where(b => b.side != BodySide.None).All(b => !b.body.isKinematic),
                    $"{avatar.bones.Length} bodies, {avatar.motors.Length} bounded joint motors; arms and fingers dynamic.");
                Check("Skinned renderer", avatar.visibleSkin && avatar.visibleSkin.enabled && avatar.visibleSkin.bones.All(t => t.GetComponent<Rigidbody>()), "The active visible skin directly references physical Rigidbody transforms.");
                Check("No animated visible skeleton", avatar.GetComponentsInChildren<Animator>().Length == 0, "No Animator or runtime source-model bone follower is present.");
                Check("Realistic textured human", avatar.GetComponentInChildren<RealisticAvatarSkin>() && avatar.visibleSkin.sharedMaterials.All(m => m.GetTexture("_BaseMap")), "Microsoft Rocketbox upper-body skin, with body/head/hair texture materials.");
                var skinWeights = avatar.visibleSkin.sharedMesh.boneWeights;
                var usedSkinBones = new System.Collections.Generic.HashSet<int>();
                foreach (var w in skinWeights) {
                    if (w.weight0 > .001f) usedSkinBones.Add(w.boneIndex0); if (w.weight1 > .001f) usedSkinBones.Add(w.boneIndex1);
                    if (w.weight2 > .001f) usedSkinBones.Add(w.boneIndex2); if (w.weight3 > .001f) usedSkinBones.Add(w.boneIndex3);
                }
                var visibleFingers = avatar.bones.Where(b => b.role == BodyRole.Hand || b.role == BodyRole.Finger)
                    .Count(b => usedSkinBones.Contains(Array.IndexOf(avatar.bones, b)));
                Check("Visible hands and every finger segment", visibleFingers == 32, $"{visibleFingers}/32 hand and finger bodies influence retained visible geometry.");
                Check("Hardware channel structure", avatar.servoBridge && avatar.servoBridge.leftProfile.channels.Length == 13
                    && avatar.servoBridge.rightProfile.channels.Length == 13
                    && avatar.Find("R_Hand").joint.angularYMotion == ConfigurableJointMotion.Locked,
                    "Per side: 7 arm + 6 hand inputs; wrist has only two driven axes; controller ports are provisional.");
                bool roundTrip = true;
                foreach (var p in new[] { avatar.servoBridge.leftProfile, avatar.servoBridge.rightProfile }) foreach (var c in p.channels) {
                    float input = Mathf.Lerp(c.inputMin, c.inputMax, .37f);
                    roundTrip &= Mathf.Abs(c.Encode(c.Decode(input)) - input) < .001f;
                    var copy = new ServoChannel { inputMin = c.inputMin, inputMax = c.inputMax, targetMin = c.targetMin, targetMax = c.targetMax, reversed = true };
                    roundTrip &= Mathf.Abs(copy.Encode(copy.Decode(input)) - input) < .001f;
                }
                Check("Servo endpoint/direction conversion", roundTrip, "All 26 channels round-trip in both direction conventions; this is not hardware calibration.");
                int initialExternalOverlaps = 0;
                var external = UnityEngine.Object.FindObjectsByType<Collider>().Where(c => !c.GetComponent<PhysicsBone>() && !c.isTrigger).ToArray();
                foreach (var b in avatar.bones) foreach (var ac in b.GetComponents<Collider>()) foreach (var bc in external)
                    if (UnityEngine.Physics.ComputePenetration(ac, ac.transform.position, ac.transform.rotation, bc, bc.transform.position, bc.transform.rotation, out _, out float depth) && depth > .0001f) initialExternalOverlaps++;
                Check("Initial external clearance", initialExternalOverlaps == 0, $"{initialExternalOverlaps} avatar/external collider penetrations before simulation.");
                lab.movableObject.gameObject.SetActive(false); lab.SetGate(false);
                avatar.ResetPhysics(); Step(2);
                Vector3 idleHand = avatar.Find("R_Hand").body.position;
                Check("Idle tracking", MainError() < 25, "Mean major-joint rotation error " + MainError().ToString("F2") + " degrees.");
                var independent = avatar.gestures.Pose.right.Copy(); independent.fingerCurls = new[] { 1f, 0f, 0f, 0f };
                var inputs = avatar.servoBridge.rightProfile.Encode(independent);
                avatar.servoBridge.SetPose(BodySide.Right, inputs); Step(1.3f);
                Check("Independent single-servo finger synergy", avatar.Find("R_Index_0").Angles.x < -30
                    && avatar.Find("R_Middle_0").Angles.x > -15 && avatar.Find("R_Index_1").Angles.x < -30,
                    $"One H1 curls multiple physical index joints: proximal {avatar.Find("R_Index_0").Angles.x:F1}°, middle joint {avatar.Find("R_Index_1").Angles.x:F1}°; separate middle finger {avatar.Find("R_Middle_0").Angles.x:F1}°.");
                bool rejected = false; var bad = (float[])inputs.Clone(); bad[12] = float.NaN;
                try { avatar.servoBridge.SetPose(BodySide.Right, bad); } catch (ArgumentException) { rejected = true; }
                Check("Atomic servo packet rejection", rejected && avatar.servoBridge.Targets(BodySide.Right).SequenceEqual(inputs), "Invalid packet rejected without changing any accepted channel target.");
                var previousTarget = avatar.Find("R_Index_0").target.localRotation;
                avatar.ReturnToIdle(); OneStep();
                Check("Servo to gesture smooth transition", Quaternion.Angle(previousTarget, avatar.Find("R_Index_0").target.localRotation) < 2,
                    "Return blends from the current servo pose, not the stale gesture target.");
                Step(1); avatar.ResetPhysics(); Step(2);
                foreach (var type in new[] { GestureType.Handshake, GestureType.Wave, GestureType.DoubleThumbsUp }) {
                    float maxY = -100, maxZ = -100, errorAtHold = 0;
                    for (int repeat = 0; repeat < 3; repeat++) {
                        avatar.PlayGesture(type, 3.8f);
                        for (int step = 0; step < Mathf.CeilToInt(4.7f / dt); step++) {
                            OneStep();
                            maxY = Mathf.Max(maxY, avatar.Find("R_Hand").body.position.y);
                            maxZ = Mathf.Max(maxZ, avatar.Find("R_Hand").body.position.z);
                            if (step == Mathf.RoundToInt(1.6f / dt)) errorAtHold = MainError();
                        }
                    }
                    Check(type + " finite and returns idle", avatar.gestures.State == GestureState.Idle && IsFinite(),
                        $"Peak wrist y={maxY:F3}m z={maxZ:F3}m; hold mean error={errorAtHold:F2} degrees.");
                    if (type == GestureType.Handshake) Check("Handshake extension", maxZ > idleHand.z + .18f, $"Forward travel {maxZ - idleHand.z:F3}m.");
                    if (type == GestureType.Wave) Check("Wave arm elevation", maxY > idleHand.y + .35f, $"Vertical wrist travel {maxY - idleHand.y:F3}m.");
                }
                avatar.PlayGesture(GestureType.DoubleThumbsUp, 5); Step(2);
                float curled = avatar.Find("R_Index_0").Angles.x;
                var thumb = avatar.Find("R_Thumb_0");
                float upward = Vector3.Dot(thumb.body.rotation * Vector3.down, Vector3.up);
                Check("Finger fist actuation", curled < -30, $"Right index proximal flexion {curled:F1} degrees.");
                Check("Thumb extension orientation", upward > .35f, $"Right thumb direction dot world up {upward:F3}.");
                avatar.ReturnToIdle(); Step(1);
                avatar.PlayGesture(GestureType.Handshake, 6); Step(2);
                float elevated = avatar.Find("R_Hand").body.position.y;
                avatar.motorStrength = 0; Step(.5f);
                float sag = elevated - avatar.Find("R_Hand").body.position.y;
                avatar.motorStrength = 1; Step(.8f);
                Check("Gravity with disabled motors", sag > .03f, $"Hand dropped {sag:F3}m when drive torque was removed.");
                avatar.ResetPhysics(); Step(1);
                avatar.PlayGesture(GestureType.Handshake, 8); Step(2);
                float freeZ = avatar.Find("R_Hand").body.position.z;
                avatar.ResetPhysics(); lab.SetGate(true); Step(1);
                avatar.PlayGesture(GestureType.Handshake, 8); Step(2);
                float blockedZ = avatar.Find("R_Hand").body.position.z;
                float blockedError = MainError();
                lab.SetGate(false); Step(1);
                float recoveredZ = avatar.Find("R_Hand").body.position.z;
                Check("Solid obstacle changes motion", freeZ - blockedZ > .015f, $"Free wrist z={freeZ:F3}; blocked={blockedZ:F3}; hold error={blockedError:F2} degrees.");
                Check("Recovery after obstacle removal", recoveredZ > blockedZ + .012f, $"Recovered wrist forward by {recoveredZ - blockedZ:F3}m.");
                avatar.ReturnToIdle(); Step(1.3f);
                Vector3 before = avatar.Find("R_Hand").body.position;
                avatar.ApplyTestImpulse(); Step(.10f);
                float response = Vector3.Distance(before, avatar.Find("R_Hand").body.position); Step(1);
                Check("External impulse response", response > .002f && IsFinite(), $"Wrist displacement after impulse {response:F4}m.");
                lab.movableObject.gameObject.SetActive(true); lab.ResetObject();
                float initialY = lab.movableObject.position.y; Step(.5f);
                Check("Movable object gravity and support", lab.movableObject.position.y < initialY + .002f && Mathf.Abs(lab.movableObject.position.y - lab.ExpectedObjectRestY) < .01f,
                    $"Supported object centre y={lab.movableObject.position.y:F4}m.");
                Check("Constraint stability", maximumAnchor < .04f && maximumSpeed < 25 && IsFinite(), $"Max anchor separation {maximumAnchor:F4}m ({maximumAnchorBone}); max linear speed {maximumSpeed:F2}m/s.");
                Check("Elbow anatomical twist limits", elbowLimitViolation < 3, $"Maximum excursion beyond configured flexion/extension range {elbowLimitViolation:F2} degrees (numerical tolerance 3 degrees).");
                report.allPassed = report.tests.All(t => t.passed);
            } catch (Exception e) { Check("Unexpected test exception", false, e.ToString()); }
            finally {
                UnityEngine.Physics.simulationMode = previous;
                File.WriteAllText("TestResults/physics-validation.json", JsonUtility.ToJson(report, true));
                Debug.Log("HDT VALIDATION: " + JsonUtility.ToJson(report));
                EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            }
            if (!report.allPassed) throw new Exception("Physics acceptance tests failed; see TestResults/physics-validation.json.");
        }

        private static void OneStep()
        {
            avatar.StepPhysics(dt); UnityEngine.Physics.Simulate(dt);
            foreach (var b in avatar.bones) {
                if (b.AnchorError > maximumAnchor) { maximumAnchor = b.AnchorError; maximumAnchorBone = b.id; }
                maximumSpeed = Mathf.Max(maximumSpeed, b.body.linearVelocity.magnitude);
                if (b.role == BodyRole.Elbow) {
                    var q = Quaternion.Inverse(b.bindRelative) * b.CurrentRelative;
                    float twist = Mathf.DeltaAngle(0, 2 * Mathf.Atan2(q.x, q.w) * Mathf.Rad2Deg);
                    elbowLimitViolation = Mathf.Max(elbowLimitViolation, avatar.profile.elbow.lowX - twist, twist - avatar.profile.elbow.highX);
                }
            }
        }
        private static void Step(float seconds) { for (int i = 0; i < Mathf.CeilToInt(seconds / dt); i++) OneStep(); }
        private static float MainError() => avatar.motors.Where(m => m.bone.role != BodyRole.Finger).Average(m => m.bone.TrackingError);
        private static bool IsFinite() => avatar.bones.All(b => float.IsFinite(b.body.position.x) && float.IsFinite(b.body.position.y) && float.IsFinite(b.body.position.z));
        private static void Check(string name, bool passed, string detail)
        {
            report.tests.Add(new Result { name = name, passed = passed, detail = detail });
            Debug.Log($"HDT TEST {(passed ? "PASS" : "FAIL")}: {name}: {detail}");
        }
    }
}
