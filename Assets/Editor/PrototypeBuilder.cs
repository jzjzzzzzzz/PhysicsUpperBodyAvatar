// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace ALS.HDT.Editor
{
    public static class PrototypeBuilder
    {
        public const string ScenePath = "Assets/Scenes/UpperBodyLab.unity";
        private static readonly Dictionary<string, PhysicsBone> bodies = new Dictionary<string, PhysicsBone>();
        private static readonly Dictionary<string, Transform> targets = new Dictionary<string, Transform>();
        private static AvatarPhysicsProfile profile;
        private static AvatarController avatar;
        private static Transform physicalRoot;

        private static T Asset<T>(string path, Func<T> create) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) { asset = create(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }

        [MenuItem("ALS HDT/Create or rebuild demonstration scene")]
        public static void CreateDemo()
        {
            foreach (string path in new[] { "Assets/Scenes", "Assets/Models", "Assets/Materials", "Assets/Prefabs", "Assets/Configuration" }) Directory.CreateDirectory(path);
            profile = Asset("Assets/Configuration/AvatarPhysics.asset", ScriptableObject.CreateInstance<AvatarPhysicsProfile>);
            EditorUtility.SetDirty(profile);
            var library = Asset("Assets/Configuration/Gestures.asset", DefaultGestures);
            ConfigurePipeline();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            bodies.Clear(); targets.Clear();

            var root = new GameObject("Physics Upper Body Avatar");
            avatar = root.AddComponent<AvatarController>(); avatar.profile = profile;
            avatar.gestures = root.AddComponent<GesturePlayer>(); avatar.gestures.library = library;
            avatar.servoBridge = root.AddComponent<ServoAvatarBridge>();
            avatar.servoBridge.leftProfile = Asset("Assets/Configuration/ServoRigLeft.asset", () => ServoRigProfile.CreateDefault(BodySide.Left));
            avatar.servoBridge.rightProfile = Asset("Assets/Configuration/ServoRigRight.asset", () => ServoRigProfile.CreateDefault(BodySide.Right));
            physicalRoot = new GameObject("PhysicsSkeleton — PhysX owns these transforms").transform; physicalRoot.SetParent(root.transform);
            var targetRoot = new GameObject("TargetSkeleton — no renderer / collider / Rigidbody").transform; targetRoot.SetParent(root.transform);
            targets["Root"] = targetRoot;
            Bone("Torso", BodyRole.Torso, BodySide.None, "Root", null, new Vector3(0, 1.23f, 0), Quaternion.identity, 0, 16, JointKind.Neck, true);
            var torso = bodies["Torso"].gameObject.AddComponent<CapsuleCollider>(); torso.radius = .143f; torso.height = .55f; torso.direction = 1;
            Bone("Neck", BodyRole.Neck, BodySide.None, "Torso", "Torso", new Vector3(0, .32f, 0), Quaternion.identity, .08f, 1, JointKind.Neck, true);
            var neck = bodies["Neck"].gameObject.AddComponent<CapsuleCollider>(); neck.radius = .042f; neck.height = .10f; neck.center = new Vector3(0, .04f, 0);
            Bone("Head", BodyRole.Head, BodySide.None, "Neck", "Neck", new Vector3(0, .08f, 0), Quaternion.identity, .25f, profile.headMass, JointKind.Neck);
            var head = bodies["Head"].gameObject.AddComponent<SphereCollider>(); head.radius = .105f; head.center = new Vector3(0, .12f, 0);
            BuildArm(BodySide.Left); BuildArm(BodySide.Right);
            avatar.bones = bodies.Values.ToArray(); avatar.motors = root.GetComponentsInChildren<JointMotor>();
            foreach (var collider in root.GetComponentsInChildren<Collider>()) collider.contactOffset = profile.contactOffset;

            var skinObject = new GameObject("Visible skinned anatomy — bound directly to physical bones"); skinObject.transform.SetParent(root.transform);
            var skin = skinObject.AddComponent<AnatomicalSkin>(); skin.avatar = avatar;
            skin.materials = new[] {
                Material("Skin", new Color(.68f, .49f, .38f), .30f),
                Material("Clinical garment", new Color(.16f, .29f, .33f), .25f),
                Material("Eyes and nails", new Color(.88f, .85f, .78f), .4f),
                Material("Facial details", new Color(.12f, .08f, .064f), .25f)
            };
            skin.Rebuild();
            string meshPath = "Assets/Models/AnatomicalMannequin.asset";
            // CreateAsset replaces the native mesh data too; CopySerialized alone
            // can retain stale native index/vertex buffers when topology changes.
            var savedMesh = skin.GeneratedMesh;
            AssetDatabase.CreateAsset(savedMesh, meshPath);
            skin.GetComponent<SkinnedMeshRenderer>().sharedMesh = savedMesh;
            avatar.visibleSkin = skin.GetComponent<SkinnedMeshRenderer>();
            RocketboxSkinBuilder.TryAttach(avatar);

            var lab = BuildEnvironment();
            // The main viewport deliberately leaves UI margins. Clear those
            // margins every frame too, avoiding stale UI glyphs outside its rect.
            var backdrop = new GameObject("Full-window background clear").AddComponent<Camera>();
            backdrop.depth = -100; backdrop.cullingMask = 0;
            backdrop.clearFlags = CameraClearFlags.SolidColor; backdrop.backgroundColor = new Color(.035f, .055f, .07f);
            backdrop.nearClipPlane = .01f; backdrop.farClipPlane = 1;
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.fieldOfView = 38;
            camera.rect = new Rect(0, .14f, .75f, .86f); camera.nearClipPlane = .05f; camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f, .095f, .12f);
            var orbit = cameraObject.AddComponent<OrbitCamera>(); orbit.distance = 2.25f; orbit.focus = new Vector3(0, 1.36f, .12f); orbit.pitch = 2; orbit.yaw = 12; orbit.Place();
            camera.GetUniversalAdditionalCameraData().renderShadows = true;
            var ui = new GameObject("Desktop Interface");
            var document = ui.AddComponent<UIDocument>();
            document.panelSettings = Asset("Assets/Configuration/DesktopPanel.asset", () => {
                var panel = ScriptableObject.CreateInstance<PanelSettings>(); panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/LabTheme.tss");
                panel.referenceResolution = new Vector2Int(1280, 800); return panel;
            });
            document.panelSettings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/LabTheme.tss");
            EditorUtility.SetDirty(document.panelSettings);
            var hud = ui.AddComponent<AvatarHUD>(); hud.avatar = avatar; hud.lab = lab;
            hud.stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/AvatarHUD.uss");
            var server = root.AddComponent<LocalCommandServer>(); server.avatar = avatar;
            root.AddComponent<RuntimeVerification>().avatar = avatar;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/PhysicsUpperBodyAvatar.prefab");
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.44f, .50f, .55f);
            Time.fixedDeltaTime = profile.fixedTimeStep;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "ALS HDT Research"; PlayerSettings.productName = "Physics Upper Body Avatar";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log($"HDT scene saved: {avatar.bones.Length} rigid bodies, {avatar.motors.Length} motorized joints; visible mesh uses physical bones.");
        }

        private static void ConfigurePipeline()
        {
            var renderer = Asset("Assets/Configuration/DesktopRenderer.asset", ScriptableObject.CreateInstance<UniversalRendererData>);
            var pipeline = Asset("Assets/Configuration/DesktopURP.asset", () => UniversalRenderPipelineAsset.Create(renderer));
            pipeline.msaaSampleCount = 4; pipeline.shadowDistance = 8; pipeline.mainLightShadowmapResolution = 2048;
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorUtility.SetDirty(pipeline);
        }

        private static Material Material(string name, Color color, float smoothness = .25f)
        {
            var mat = Asset("Assets/Materials/" + name + ".mat", () => new Material(Shader.Find("Universal Render Pipeline/Lit")));
            mat.color = color; mat.SetFloat("_Smoothness", smoothness); EditorUtility.SetDirty(mat); return mat;
        }

        private static PhysicsBone Bone(string id, BodyRole role, BodySide side, string targetParent, string connected,
            Vector3 localPosition, Quaternion localRotation, float length, float mass, JointKind kind,
            bool kinematic = false, int finger = -1, int phalanx = 0)
        {
            var target = new GameObject(id).transform; target.SetParent(targets[targetParent], false);
            target.localPosition = localPosition; target.localRotation = localRotation; targets[id] = target;
            var go = new GameObject(id); go.layer = 8; go.transform.SetParent(physicalRoot);
            go.transform.SetPositionAndRotation(target.position, target.rotation);
            var rb = go.AddComponent<Rigidbody>(); rb.mass = mass; rb.isKinematic = kinematic; rb.useGravity = !kinematic;
            var bone = go.AddComponent<PhysicsBone>(); bone.id = id; bone.role = role; bone.side = side;
            bone.length = length; bone.body = rb; bone.target = target; bone.targetRest = localRotation;
            bone.fingerIndex = finger; bone.phalanxIndex = phalanx; bone.jointKind = kind;
            bone.resetPosition = rb.position; bone.resetRotation = rb.rotation;
            if (!kinematic && connected != null) {
                var joint = go.AddComponent<ConfigurableJoint>(); joint.connectedBody = bodies[connected].body;
                joint.autoConfigureConnectedAnchor = false; joint.anchor = Vector3.zero;
                joint.connectedAnchor = joint.connectedBody.transform.InverseTransformPoint(rb.position);
                if (kind == JointKind.Pronation) { joint.axis = Vector3.up; joint.secondaryAxis = Vector3.forward; }
                bone.joint = joint; bone.bindRelative = Quaternion.Inverse(joint.connectedBody.rotation) * rb.rotation;
                var motor = go.AddComponent<JointMotor>(); motor.bone = bone; motor.profile = profile; motor.Initialize();
            }
            if (role == BodyRole.Hand || role == BodyRole.Finger) go.AddComponent<ContactReporter>();
            bodies[id] = bone; return bone;
        }

        private static void BuildArm(BodySide side)
        {
            string s = side == BodySide.Right ? "R" : "L"; float sign = side == BodySide.Right ? 1 : -1;
            string marker = s + "_Shoulder";
            var shoulder = new GameObject(side + "Shoulder").transform; shoulder.SetParent(targets["Torso"], false);
            shoulder.localPosition = new Vector3(sign * profile.shoulderHalfWidth, profile.shoulderHeight - 1.23f, 0); targets[marker] = shoulder;
            var upper = Bone(s + "_UpperArm", BodyRole.UpperArm, side, marker, "Torso", Vector3.zero, Quaternion.identity, profile.upperArmLength, profile.upperArmMass, JointKind.Shoulder);
            Capsule(upper, profile.upperArmRadius, profile.upperArmLength);
            var elbow = Bone(s + "_Elbow", BodyRole.Elbow, side, upper.id, upper.id, Vector3.down * profile.upperArmLength, Quaternion.identity, 0, profile.elbowMass, JointKind.Elbow);
            var sphere = elbow.gameObject.AddComponent<SphereCollider>(); sphere.radius = .022f;
            var forearm = Bone(s + "_Forearm", BodyRole.Forearm, side, elbow.id, elbow.id, Vector3.zero, Quaternion.identity, profile.forearmLength, profile.forearmMass, JointKind.Pronation);
            Capsule(forearm, profile.forearmRadius, profile.forearmLength);
            var hand = Bone(s + "_Hand", BodyRole.Hand, side, forearm.id, forearm.id, Vector3.down * profile.forearmLength, Quaternion.identity, profile.palmLength, profile.handMass, JointKind.Wrist);
            var box = hand.gameObject.AddComponent<BoxCollider>(); box.center = new Vector3(0, -.055f, 0); box.size = new Vector3(.080f, .11f, .038f);
            string[] names = { "Index", "Middle", "Ring", "Little", "Thumb" };
            float[] offsets = { -.027f, -.008f, .012f, .030f, -.05f };
            float[][] lengths = { new[] { .039f, .025f, .021f }, new[] { .044f, .028f, .023f }, new[] { .041f, .027f, .022f }, new[] { .031f, .022f, .019f }, new[] { .036f, .026f, .021f } };
            for (int finger = 0; finger < 5; finger++) {
                string parent = hand.id;
                for (int segment = 0; segment < 3; segment++) {
                    // Palm +Z: right thumb/index are on +X (left mirrored).
                    Vector3 pos = segment == 0 ? new Vector3(finger == 4 ? sign * profile.thumbBaseLateral : -sign * offsets[finger], finger == 4 ? -profile.thumbBaseDown : -.108f, 0) : Vector3.down * lengths[finger][segment - 1];
                    Quaternion rotation = finger == 4 && segment == 0 ? Quaternion.Euler(0, 0, sign * 55) : Quaternion.identity;
                    var b = Bone(s + "_" + names[finger] + "_" + segment, BodyRole.Finger, side, parent, parent, pos, rotation,
                        lengths[finger][segment], profile.fingerMass, finger == 4 && segment == 0 ? JointKind.Thumb : JointKind.Finger, false, finger, segment);
                    Capsule(b, finger == 4 ? .0095f : .0075f, b.length);
                    parent = b.id;
                }
            }
        }

        private static void Capsule(PhysicsBone b, float radius, float length)
        {
            var col = b.gameObject.AddComponent<CapsuleCollider>(); col.direction = 1;
            col.radius = radius; col.height = length + radius * 1.5f; col.center = Vector3.down * length * .5f;
        }

        private static GameObject Block(string name, Vector3 position, Vector3 scale, Material mat, bool collision = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static InteractionLab BuildEnvironment()
        {
            var root = new GameObject("Physical interaction laboratory"); var lab = root.AddComponent<InteractionLab>();
            var stage = Material("Stage", new Color(.12f, .17f, .20f));
            var table = Material("Contact table", new Color(.38f, .58f, .64f, .16f));
            table.SetFloat("_Surface", 1); table.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            table.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); table.SetFloat("_ZWrite", 0);
            table.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); table.SetOverrideTag("RenderType", "Transparent");
            table.renderQueue = (int)RenderQueue.Transparent;
            table.SetShaderPassEnabled("ShadowCaster", false); EditorUtility.SetDirty(table);
            var teal = Material("Movable test block", new Color(.10f, .67f, .58f), .35f);
            var gateMat = Material("Resistance gate", new Color(.75f, .39f, .18f), .2f);
            Block("Floor", new Vector3(0, .53f, 0), new Vector3(5, .06f, 4), stage);
            // The stand must clear the inward-pointing thumbs in the bind pose.
            Block("Torso mounting plinth", new Vector3(0, .73f, 0), new Vector3(.16f, .34f, .16f), stage);
            // Keep the near edge beyond the rising hand's sweep. A table above
            // the resting hands would otherwise block every forward gesture.
            lab.supportSurface = Block("Contact table", new Vector3(0, .85f, .76f), new Vector3(1.35f, .08f, .52f), table).GetComponent<Collider>();
            var block = Block("Movable 350 g test object", new Vector3(.22f, 1.05f, .55f), new Vector3(.15f, .30f, .15f), teal);
            var rb = block.AddComponent<Rigidbody>(); rb.mass = .35f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = .12f; rb.angularDamping = .3f;
            lab.movableObject = rb; lab.objectStartPosition = rb.position;
            var gate = Block("Solid resistance gate", new Vector3(.22f, 1.27f, .42f), new Vector3(.24f, .40f, .09f), gateMat);
            gate.AddComponent<Rigidbody>().isKinematic = true; gate.SetActive(false); lab.resistanceGate = gate;
            foreach (var go in new[] { block, gate }) go.layer = 10;
            Light("Warm key", new Vector3(50, 160, 0), new Color(1, .95f, .90f), 1.35f, true);
            Light("Cool fill", new Vector3(25, 240, 0), new Color(.65f, .79f, 1), .7f, false);
            Light("Rim", new Vector3(25, -25, 0), new Color(.75f, 1, .96f), .7f, false);
            return lab;
        }

        private static void Light(string name, Vector3 rotation, Color color, float intensity, bool shadows)
        {
            var go = new GameObject(name); go.transform.rotation = Quaternion.Euler(rotation);
            var light = go.AddComponent<Light>(); light.type = LightType.Directional; light.color = color; light.intensity = intensity;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        private static GestureLibrary DefaultGestures()
        {
            var result = ScriptableObject.CreateInstance<GestureLibrary>();
            result.idle.left = new ArmPose { shoulder = new Vector3(-7, 0, -4), elbow = -12, pronation = 85, fingerCurl = .08f, thumbCurl = .05f };
            result.idle.right = new ArmPose { shoulder = new Vector3(-7, 0, 4), elbow = -12, pronation = -85, fingerCurl = .08f, thumbCurl = .05f };
            var handshake = new GestureDefinition { type = GestureType.Handshake, duration = 4.2f, amplitude = 5, cycles = 3, pose = result.idle.Copy() };
            handshake.pose.right = new ArmPose { shoulder = new Vector3(-45, 0, 2), elbow = -45, pronation = -78, wrist = new Vector3(0, -10, 0), fingerCurl = .03f };
            var wave = new GestureDefinition { type = GestureType.Wave, duration = 4.5f, amplitude = 16, cycles = 3, pose = result.idle.Copy() };
            wave.pose.right = new ArmPose { shoulder = new Vector3(-5, 88, 72), elbow = -132, pronation = -80, wrist = new Vector3(0, -8, 0), fingerCurl = 0 };
            var thumbs = new GestureDefinition { type = GestureType.DoubleThumbsUp, duration = 4.0f, pose = result.idle.Copy() };
            thumbs.pose.right = new ArmPose { shoulder = new Vector3(-10, 0, 8), elbow = -95, pronation = -85, fingerCurl = .96f, thumbSpread = 34 };
            thumbs.pose.left = new ArmPose { shoulder = new Vector3(-10, 0, -8), elbow = -95, pronation = 85, fingerCurl = .96f, thumbSpread = 34 };
            result.gestures = new[] { handshake, wave, thumbs }; return result;
        }

        [MenuItem("ALS HDT/Build macOS application")]
        public static void BuildMac()
        {
            if (!File.Exists(ScenePath)) CreateDemo();
            Directory.CreateDirectory("Builds/macOS");
            PlayerSettings.bundleVersion = File.ReadAllText("VERSION").Trim();
            // Mono ARM64 is included with this editor. IL2CPP is optional and
            // requires the separate macOS IL2CPP support module in Unity Hub.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX), "Architecture", "arm64");
            Directory.CreateDirectory("Assets/StreamingAssets/Hardware");
            foreach (string side in new[] { "Left", "Right" }) {
                var rig = AssetDatabase.LoadAssetAtPath<ServoRigProfile>("Assets/Configuration/ServoRig" + side + ".asset");
                if (rig) { rig.Validate(); File.WriteAllText("Assets/StreamingAssets/Hardware/ServoRig" + side + ".json", JsonUtility.ToJson(rig, true)); }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string projectNotices = "Assets/StreamingAssets/ThirdPartyNotices";
            Directory.CreateDirectory(projectNotices);
            File.Copy("LICENSE", projectNotices + "/Project-MPL-2.0.txt", true);
            File.Copy("NOTICE.md", projectNotices + "/NOTICE.md", true);
            File.Copy("LICENSES/Legacy-MIT.txt", projectNotices + "/Legacy-MIT.txt", true);
            string license = "Assets/ThirdParty/MicrosoftRocketbox/LICENSE.md";
            if (File.Exists(license)) {
                // Package attribution BEFORE BuildPlayer, so the final bundle's
                // code-signature resource seal includes the MIT notice.
                string notices = "Assets/StreamingAssets/ThirdPartyNotices";
                Directory.CreateDirectory(notices); File.Copy(license, notices + "/MicrosoftRocketbox-MIT.txt", true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/macOS/PhysicsUpperBodyAvatar.app",
                target = BuildTarget.StandaloneOSX, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("macOS build failed: " + report.summary.result);
            Debug.Log("HDT macOS application built successfully.");
        }

        public static void CreateAndBuildMac() { CreateDemo(); BuildMac(); }
    }
}
