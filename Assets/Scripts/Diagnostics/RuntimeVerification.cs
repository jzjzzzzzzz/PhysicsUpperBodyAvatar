// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ALS.HDT
{
    // Opt-in only. Normal local launches never run tests or quit automatically.
    public class RuntimeVerification : MonoBehaviour
    {
        [Serializable] private class Result { public string name; public bool passed; public string detail; }
        [Serializable] private class Report {
            public string unityVersion, platform, graphicsDevice, mode = "Standalone / automatic FixedUpdate / live collision callbacks / rendered screenshots";
            public bool allPassed;
            public List<Result> tests = new List<Result>();
        }
        public AvatarController avatar;
        private Report report;
        private string output;
        private float maxAnchor, maxSpeed;
        private bool testing;

        void FixedUpdate()
        {
            if (!testing) return;
            foreach (var b in avatar.bones) {
                maxAnchor = Mathf.Max(maxAnchor, b.AnchorError);
                maxSpeed = Mathf.Max(maxSpeed, b.body.linearVelocity.magnitude);
            }
        }

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("-hdtSelfTest")) yield break;
            FindAnyObjectByType<AvatarHUD>().SuppressNativeInputForVerification = true;
            FindAnyObjectByType<OrbitCamera>().enabled = false;
            int outputFlag = Array.IndexOf(args, "-hdtOutput");
            output = outputFlag >= 0 && outputFlag + 1 < args.Length ? Path.GetFullPath(args[outputFlag + 1])
                : Path.Combine(Application.persistentDataPath, "TestResults");
            Directory.CreateDirectory(output);
            report = new Report { unityVersion = Application.unityVersion, platform = Application.platform.ToString(), graphicsDevice = SystemInfo.graphicsDeviceName };
            var lab = FindAnyObjectByType<InteractionLab>();
            lab.movableObject.gameObject.SetActive(false);
            testing = true;
            yield return new WaitForSeconds(2);
            float idleY = avatar.Find("R_Hand").body.position.y;
            float idleZ = avatar.Find("R_Hand").body.position.z;
            Check("Idle physical tracking", MeanError() < 15 && Finite(), $"Major joint mean error {MeanError():F2} degrees.");
            Check("Realistic human skin", avatar.visibleSkin && avatar.GetComponentInChildren<RealisticAvatarSkin>() && avatar.GetComponentsInChildren<Animator>().Length == 0,
                "Textured Rocketbox character; visible mesh has physical bones and no Animator.");
            // Exercise the real UI callbacks through UI Toolkit navigation events.
            // This is deliberately labelled synthetic, not a native mouse test.
            foreach (var action in new[] { ("Handshake", GestureType.Handshake), ("Wave", GestureType.Wave), ("Double Thumbs-Up", GestureType.DoubleThumbsUp) }) {
                SubmitButton(action.Item1);
                yield return new WaitForSeconds(.1f);
                Check("UI navigation: " + action.Item1, avatar.gestures.CurrentGesture == action.Item2, "Synthetic NavigationSubmitEvent dispatched to the actual Button.");
                SubmitButton("Return to Idle");
                yield return new WaitForSeconds(.8f);
            }
            SubmitButton("Pause"); int pausedSteps = avatar.PhysicsSteps;
            yield return new WaitForSecondsRealtime(.25f);
            Check("UI navigation: Pause", avatar.IsPaused && avatar.PhysicsSteps == pausedSteps, "Physics steps stop; unscaled UI remains responsive.");
            SubmitButton("Resume");
            yield return new WaitForSeconds(.1f);
            Check("UI navigation: Resume", !avatar.IsPaused && avatar.PhysicsSteps > pausedSteps, "FixedUpdate resumes.");
            SubmitButton("Toggle resistance gate");
            Check("UI navigation: contact gate", lab.GateEnabled, "Actual interaction button toggles the kinematic obstacle.");
            SubmitButton("Toggle resistance gate");
            SubmitButton("Physics Debug");
            yield return new WaitForSeconds(.2f);
            var debug = FindAnyObjectByType<AvatarHUD>().GetComponent<UIDocument>().rootVisualElement.Q("debug-panel");
            Check("UI navigation: diagnostics", debug.resolvedStyle.display != DisplayStyle.None, "Physics diagnostics panel opens.");
            yield return Capture("PhysicsDebug");
            SubmitButton("Physics Debug"); SubmitButton("Reset Physics");
            yield return new WaitForSeconds(1);
            Check("UI navigation: reset and idle", avatar.gestures.State == GestureState.Idle, "Reset button returns state machine to idle.");
            yield return Capture("Idle");
            var bridge = avatar.servoBridge;
            var independent = avatar.gestures.Pose.right.Copy(); independent.fingerCurls = new[] { 1f, 0f, 0f, 0f };
            bridge.SetPose(BodySide.Right, bridge.rightProfile.Encode(independent));
            yield return new WaitForSeconds(1.3f);
            Check("13-channel independent finger physics", avatar.Find("R_Index_0").Angles.x < -30 && avatar.Find("R_Index_1").Angles.x < -30
                && avatar.Find("R_Middle_0").Angles.x > -15 && avatar.Find("L_Index_0").Angles.x > -15,
                $"Right index {avatar.Find("R_Index_0").Angles.x:F1}°; right middle {avatar.Find("R_Middle_0").Angles.x:F1}°; left index {avatar.Find("L_Index_0").Angles.x:F1}°. One input per finger, multiple simulated phalanges.");
            var hud = FindAnyObjectByType<AvatarHUD>();
            var root = hud.GetComponent<UIDocument>().rootVisualElement;
            root.Q<Foldout>("servo-panel").value = true;
            var slider = root.Q<Slider>("servo-H2");
            hud.DispatchingVerificationInput = true;
            try { slider.value = bridge.rightProfile.channels[8].inputMax; }
            finally { hud.DispatchingVerificationInput = false; }
            yield return new WaitForSeconds(1.3f);
            Check("UI synthetic servo slider", avatar.Find("R_Middle_0").Angles.x < -30,
                $"H2 Slider ChangeEvent drives the real middle-finger joints to {avatar.Find("R_Middle_0").Angles.x:F1}°. No native mouse claim.");
            yield return Capture("ServoMapping");
            // A real rendered close-up, not an edited reference photo.
            root.Q<ScrollView>("sidebar").ScrollTo(root.Q<Slider>("servo-H6"));
            var orbit = FindAnyObjectByType<OrbitCamera>();
            var oldFocus = orbit.focus; float oldDistance = orbit.distance, oldYaw = orbit.yaw, oldPitch = orbit.pitch;
            orbit.focus = avatar.Find("R_Hand").body.position + Vector3.down * .07f;
            orbit.distance = .52f; orbit.yaw = -75; orbit.pitch = 0; orbit.Place();
            yield return Capture("HandServoMapping");
            orbit.focus = oldFocus; orbit.distance = oldDistance; orbit.yaw = oldYaw; orbit.pitch = oldPitch; orbit.Place();
            root.Q<ScrollView>("sidebar").scrollOffset = Vector2.zero;
            SubmitButton("Release servo control");
            yield return new WaitForSeconds(1);
            Check("Servo release to idle", !bridge.RightOverride && !bridge.LeftOverride && avatar.gestures.State == GestureState.Idle, "UI releases manual targets and smoothly returns to idle.");
            root.Q<Foldout>("servo-panel").value = false;
            foreach (var gesture in new[] { GestureType.Handshake, GestureType.Wave, GestureType.DoubleThumbsUp }) {
                avatar.PlayGesture(gesture, 4.2f);
                yield return new WaitForSeconds(1.9f);
                var hand = avatar.Find("R_Hand").body;
                bool pose = gesture == GestureType.Handshake ? hand.position.z > idleZ + .18f
                    : gesture == GestureType.Wave ? hand.position.y > idleY + .35f
                    : avatar.Find("R_Index_0").Angles.x < -30 && avatar.Find("L_Index_0").Angles.x < -30
                      && Vector3.Dot(avatar.Find("R_Thumb_0").body.rotation * Vector3.down, Vector3.up) > .35f
                      && Vector3.Dot(avatar.Find("L_Thumb_0").body.rotation * Vector3.down, Vector3.up) > .35f;
                Check(gesture + " physical pose", pose && Finite(), $"Wrist ({hand.position.x:F3},{hand.position.y:F3},{hand.position.z:F3}) m; major mean error {MeanError():F2} degrees.");
                float skinError = PhysicalSkinError();
                Check(gesture + " physical skin binding", skinError < .002f, $"CPU-skinned vertices vs physical bone matrices: max difference {skinError:F6} m.");
                yield return Capture(gesture.ToString());
                yield return new WaitForSeconds(2.9f);
                Check(gesture + " returns idle", avatar.gestures.State == GestureState.Idle, avatar.gestures.State.ToString());
            }
            avatar.PlayGesture(GestureType.Handshake, 8);
            yield return new WaitForSeconds(2);
            float freeZ = avatar.Find("R_Hand").body.position.z;
            avatar.ResetPhysics(); lab.SetGate(true);
            yield return new WaitForSeconds(1);
            avatar.PlayGesture(GestureType.Handshake, 8);
            int initialImpacts = Impacts();
            yield return new WaitForSeconds(2);
            float blockedZ = avatar.Find("R_Hand").body.position.z;
            Check("Obstacle blocks driven hand", freeZ - blockedZ > .015f, $"Free z={freeZ:F3}, blocked z={blockedZ:F3} m.");
            Check("Live external contact callbacks", Impacts() > initialImpacts, $"External contact entries: {Impacts() - initialImpacts}.");
            yield return Capture("ObstacleContact");
            lab.SetGate(false);
            yield return new WaitForSeconds(1);
            Check("Recovery after obstacle removal", avatar.Find("R_Hand").body.position.z > blockedZ + .012f, $"Recovered z={avatar.Find("R_Hand").body.position.z:F3} m.");
            avatar.ReturnToIdle();
            yield return new WaitForSeconds(1);
            var before = avatar.Find("R_Hand").body.position;
            avatar.ApplyTestImpulse();
            yield return new WaitForSeconds(.1f);
            Check("Impulse affects physical skeleton", Vector3.Distance(before, avatar.Find("R_Hand").body.position) > .002f, "Applied finite 0.4 N s lateral hand impulse.");
            yield return new WaitForSeconds(1);
            lab.movableObject.gameObject.SetActive(true); lab.ResetObject();
            yield return new WaitForSeconds(.6f);
            Check("Movable object supported by contact", Mathf.Abs(lab.movableObject.position.y - lab.ExpectedObjectRestY) < .01f, $"Block centre y={lab.movableObject.position.y:F3} m.");
            avatar.ResetPhysics();
            yield return new WaitForSeconds(1);
            Vector3 objectBefore = lab.movableObject.position;
            int contactsBefore = Impacts();
            avatar.PlayGesture(GestureType.Handshake, 5);
            yield return new WaitForSeconds(2.2f);
            float moved = Vector3.Distance(objectBefore, lab.movableObject.position);
            Check("Driven hand moves dynamic object", moved > .005f && Impacts() > contactsBefore, $"350 g block displacement {moved:F4} m; new external contacts {Impacts() - contactsBefore}.");
            yield return Capture("MovableObjectContact");
            Check("Finite bounded simulation", Finite() && maxAnchor < .04f && maxSpeed < 25, $"Maximum anchor error {maxAnchor:F4} m; speed {maxSpeed:F2} m/s; {avatar.PhysicsSteps} physics steps.");
            report.allPassed = report.tests.All(t => t.passed);
            File.WriteAllText(Path.Combine(output, "runtime-validation.json"), JsonUtility.ToJson(report, true));
            Debug.Log("HDT RUNTIME VALIDATION: " + JsonUtility.ToJson(report));
            testing = false;
            Application.Quit(report.allPassed ? 0 : 2);
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return null;
        }
        private int Impacts() => avatar.GetComponentsInChildren<ContactReporter>().Sum(c => c.TotalImpacts);
        private float PhysicalSkinError()
        {
            var skin = avatar.visibleSkin; var mesh = skin.sharedMesh;
            var baked = new Mesh(); skin.BakeMesh(baked);
            var actual = baked.vertices; var vertices = mesh.vertices; var weights = mesh.boneWeights;
            var matrices = skin.bones.Select((b, i) => b.localToWorldMatrix * mesh.bindposes[i]).ToArray();
            float maxError = 0;
            for (int v = 0; v < vertices.Length; v++) {
                var w = weights[v]; Vector3 p = matrices[w.boneIndex0].MultiplyPoint3x4(vertices[v]) * w.weight0
                    + matrices[w.boneIndex1].MultiplyPoint3x4(vertices[v]) * w.weight1
                    + matrices[w.boneIndex2].MultiplyPoint3x4(vertices[v]) * w.weight2
                    + matrices[w.boneIndex3].MultiplyPoint3x4(vertices[v]) * w.weight3;
                float error = Vector3.Distance(p, skin.transform.TransformPoint(actual[v]));
                if (!float.IsFinite(error)) { maxError = float.PositiveInfinity; break; }
                maxError = Mathf.Max(maxError, error);
            }
            Destroy(baked); return maxError;
        }
        private void SubmitButton(string label)
        {
            var hud = FindAnyObjectByType<AvatarHUD>();
            var root = hud.GetComponent<UIDocument>().rootVisualElement;
            var button = root.Query<Button>().ToList().First(b => b.text == label);
            button.Focus();
            hud.DispatchingVerificationInput = true;
            try { using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); } }
            finally { hud.DispatchingVerificationInput = false; button.Blur(); }
        }
        private float MeanError() => avatar.motors.Where(m => m.bone.role != BodyRole.Finger).Average(m => m.bone.TrackingError);
        private bool Finite() => avatar.bones.All(b => float.IsFinite(b.body.position.x) && float.IsFinite(b.body.position.y) && float.IsFinite(b.body.position.z));
        private void Check(string name, bool passed, string detail)
        {
            report.tests.Add(new Result { name = name, passed = passed, detail = detail });
            Debug.Log($"HDT RUNTIME {(passed ? "PASS" : "FAIL")}: {name}: {detail}");
        }
    }
}
