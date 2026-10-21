// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.Rendering;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ALS.HDT.Editor
{
    public static class RocketboxSkinBuilder
    {
        public const string ModelPath = "Assets/ThirdParty/MicrosoftRocketbox/Export/Male_Adult_04.fbx";
        private struct Binding { public int bone; public Matrix4x4 deformation; }

        public static bool TryAttach(AvatarController avatar)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) return false;
            var instance = UnityEngine.Object.Instantiate(model);
            instance.name = "TEMP — source rest pose only";
            try {
                var all = instance.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
                Transform T(string name) => all["Bip01 " + name];
                Vector3 sourceShoulders = (T("R UpperArm").position + T("L UpperArm").position) * .5f;
                Vector3 right = (T("R UpperArm").position - T("L UpperArm").position).normalized;
                Vector3 front = Vector3.Cross(right, Vector3.up).normalized;
                Quaternion orient = Quaternion.Inverse(Quaternion.LookRotation(front, Vector3.up));
                float scale = avatar.profile.shoulderHalfWidth * 2 / Vector3.Distance(T("R UpperArm").position, T("L UpperArm").position);
                var global = Matrix4x4.TRS(new Vector3(0, avatar.profile.shoulderHeight, 0), orient, Vector3.one * scale)
                    * Matrix4x4.Translate(-sourceShoulders);
                MoveFixedAnchor(avatar.Find("Neck"), global.MultiplyPoint3x4(T("Neck").position));
                MoveFixedAnchor(avatar.Find("Head"), global.MultiplyPoint3x4(T("Head").position));

                var source = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.sharedMesh.vertexCount).First();
                var bindings = new Binding[source.bones.Length];
                for (int i = 0; i < bindings.Length; i++) bindings[i] = Bind(source.bones[i], avatar, all, global, front, scale);
                var baked = new Mesh(); source.BakeMesh(baked);
                var original = source.sharedMesh;
                Vector3[] originalVertices = baked.vertices, originalNormals = baked.normals;
                var oldWeights = original.boneWeights;
                var positions = new Vector3[originalVertices.Length];
                var normals = new Vector3[originalVertices.Length];
                var weights = new BoneWeight[originalVertices.Length];
                var keep = new bool[originalVertices.Length];
                for (int v = 0; v < originalVertices.Length; v++) {
                    Vector3 world = source.transform.TransformPoint(originalVertices[v]);
                    Vector3 normal = source.transform.TransformDirection(originalNormals[v]);
                    var w = oldWeights[v];
                    int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                    float[] ws = { w.weight0, w.weight1, w.weight2, w.weight3 };
                    int[] physical = new int[4]; float sum = 0; bool limb = false;
                    Vector3 p = Vector3.zero, n = Vector3.zero;
                    for (int k = 0; k < 4; k++) {
                        var binding = bindings[ids[k]];
                        physical[k] = Mathf.Max(0, binding.bone);
                        if (binding.bone < 0) { ws[k] = 0; continue; }
                        if (ws[k] > .001f && avatar.bones[binding.bone].side != BodySide.None) limb = true;
                        sum += ws[k];
                        p += binding.deformation.MultiplyPoint3x4(world) * ws[k];
                        n += binding.deformation.MultiplyVector(normal) * ws[k];
                    }
                    positions[v] = p / Mathf.Max(.000001f, sum);
                    normals[v] = n.normalized;
                    // Crop the lower TORSO, not hands hanging below waist height.
                    keep[v] = sum > .999f && (limb || positions[v].y >= .91f);
                    weights[v] = new BoneWeight {
                        boneIndex0 = physical[0], weight0 = ws[0] / Mathf.Max(sum, .000001f),
                        boneIndex1 = physical[1], weight1 = ws[1] / Mathf.Max(sum, .000001f),
                        boneIndex2 = physical[2], weight2 = ws[2] / Mathf.Max(sum, .000001f),
                        boneIndex3 = physical[3], weight3 = ws[3] / Mathf.Max(sum, .000001f)
                    };
                }
                // Compact to actual upper-body triangles. No legs or invisible
                // all-zero-weight vertices survive in the generated runtime mesh.
                var submeshes = new List<int>[original.subMeshCount];
                var used = new HashSet<int>();
                for (int s = 0; s < submeshes.Length; s++) {
                    var src = original.GetTriangles(s); var dst = submeshes[s] = new List<int>();
                    for (int j = 0; j < src.Length; j += 3)
                        if (keep[src[j]] && keep[src[j + 1]] && keep[src[j + 2]])
                            for (int k = 0; k < 3; k++) { dst.Add(src[j + k]); used.Add(src[j + k]); }
                }
                int[] vertices = used.OrderBy(v => v).ToArray();
                var indices = vertices.Select((v, i) => (v, i)).ToDictionary(x => x.v, x => x.i);
                var mesh = new Mesh { name = "Rocketbox realistic upper body — physical skin", indexFormat = IndexFormat.UInt32 };
                mesh.vertices = vertices.Select(v => avatar.transform.InverseTransformPoint(positions[v])).ToArray();
                mesh.normals = vertices.Select(v => avatar.transform.InverseTransformDirection(normals[v])).ToArray();
                var uv = original.uv; mesh.uv = vertices.Select(v => uv[v]).ToArray();
                mesh.boneWeights = vertices.Select(v => weights[v]).ToArray();
                mesh.bindposes = avatar.bones.Select(b => Matrix4x4.TRS(b.resetPosition, b.resetRotation, Vector3.one).inverse * avatar.transform.localToWorldMatrix).ToArray();
                mesh.subMeshCount = submeshes.Length;
                for (int s = 0; s < submeshes.Length; s++) mesh.SetTriangles(submeshes[s].Select(v => indices[v]).ToArray(), s);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                const string meshPath = "Assets/Models/RocketboxPhysicalUpperBody.asset";
                AssetDatabase.CreateAsset(mesh, meshPath);
                var go = new GameObject("REALISTIC HUMAN — Rocketbox skin / physical bones");
                go.transform.SetParent(avatar.transform, false);
                var skin = go.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = mesh;
                skin.bones = avatar.bones.Select(b => b.transform).ToArray(); skin.rootBone = avatar.Find("Torso").transform;
                skin.quality = SkinQuality.Bone4; skin.updateWhenOffscreen = true;
                skin.localBounds = new Bounds(new Vector3(0, 1.45f, .10f), new Vector3(3.5f, 3, 3));
                skin.sharedMaterials = source.sharedMaterials.Select(m => CreateMaterial(m.name)).ToArray();
                go.AddComponent<RealisticAvatarSkin>().skin = skin;
                foreach (var fallback in avatar.GetComponentsInChildren<AnatomicalSkin>()) fallback.GetComponent<SkinnedMeshRenderer>().enabled = false;
                avatar.visibleSkin = skin;
                UnityEngine.Object.DestroyImmediate(baked);
                Debug.Log($"REALISTIC SKIN: {mesh.vertexCount} upper-body vertices, {mesh.triangles.Length / 3} triangles, {skin.bones.Length} PHYSICAL bones; no Animator. Source scale={scale:F3}.");
                return true;
            } finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void MoveFixedAnchor(PhysicsBone bone, Vector3 world)
        {
            // Editor setup only. Runtime gestures never call this method.
            bone.body.position = world; bone.resetPosition = world;
            bone.target.position = world;
            if (bone.joint) bone.joint.connectedAnchor = Quaternion.Inverse(bone.joint.connectedBody.rotation) * (world - bone.joint.connectedBody.position);
        }

        private static Binding Bind(Transform source, AvatarController avatar, Dictionary<string, Transform> all,
            Matrix4x4 global, Vector3 front, float scale)
        {
            string name = source.name.Replace("Bip01 ", "");
            // Hip-weighted jacket hems must stay continuous. Keep the source rest
            // deformation for lower-limb influences, collapse them to Torso, then
            // remove all geometry below the waist plane (rather than deleting
            // mixed hip/thigh triangles from the coat and leaving holes).
            if (Regex.IsMatch(name, @"^[LR] (Thigh|Calf|Foot|Toe)"))
                return new Binding { bone = Array.IndexOf(avatar.bones, avatar.Find("Torso")), deformation = global };
            string id = "Torso";
            var match = Regex.Match(name, @"^([LR]) (UpperArm|Forearm|Hand|Finger([0-4])([12])?)$");
            if (match.Success) {
                string side = match.Groups[1].Value, part = match.Groups[2].Value;
                string nextName;
                if (part.StartsWith("Finger")) {
                    int finger = int.Parse(match.Groups[3].Value);
                    int segment = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
                    string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
                    id = side + "_" + fingers[finger] + "_" + segment;
                    nextName = segment == 0 ? source.name + "1" : segment == 1 ? source.name.Substring(0, source.name.Length - 1) + "2" : "";
                } else {
                    id = side + "_" + part;
                    nextName = "Bip01 " + side + " " + (part == "UpperArm" ? "Forearm" : part == "Forearm" ? "Hand" : "Finger2");
                }
                var destination = avatar.Find(id);
                Vector3 longitudinal;
                if (all.TryGetValue(nextName, out var next)) longitudinal = (next.position - source.position).normalized;
                else longitudinal = (source.position - source.parent.position).normalized;
                var hand = all["Bip01 " + side + " Hand"];
                Vector3 alongPalm = (all["Bip01 " + side + " Finger2"].position - hand.position).normalized;
                Vector3 acrossPalm = all["Bip01 " + side + " Finger1"].position - all["Bip01 " + side + " Finger4"].position;
                // Canonical +Z is the PALM. Negative-X finger flexion must curl
                // into the palm, never backwards through the back of the hand.
                Vector3 palmNormal = Vector3.Cross(alongPalm, acrossPalm).normalized * (side == "R" ? 1 : -1);
                Vector3 reference = part == "UpperArm" ? front : palmNormal;
                Vector3 projected = Vector3.ProjectOnPlane(reference, longitudinal).normalized;
                if (projected.sqrMagnitude < .01f) projected = Vector3.ProjectOnPlane(front, longitudinal).normalized;
                Quaternion sourceFrame = Quaternion.LookRotation(projected, -longitudinal);
                Matrix4x4 deformation = Matrix4x4.TRS(destination.resetPosition,
                    destination.resetRotation * Quaternion.Inverse(sourceFrame), Vector3.one * scale) * Matrix4x4.Translate(-source.position);
                return new Binding { bone = Array.IndexOf(avatar.bones, destination), deformation = deformation };
            }
            if (name == "Neck") id = "Neck";
            else {
                for (var t = source; t; t = t.parent) if (t.name == "Bip01 Head") { id = "Head"; break; }
            }
            return new Binding { bone = Array.IndexOf(avatar.bones, avatar.Find(id)), deformation = global };
        }

        private static Material CreateMaterial(string sourceName)
        {
            bool head = sourceName.Contains("head"), opacity = sourceName.Contains("opacity");
            string part = opacity ? "opacity" : head ? "head" : "body";
            string folder = "Assets/Materials/Rocketbox"; Directory.CreateDirectory(folder);
            string path = folder + "/" + part + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            string textures = "Assets/ThirdParty/MicrosoftRocketbox/Textures/m006_" + part;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textures + "_color.tga"));
            m.SetColor("_BaseColor", Color.white); m.SetFloat("_Metallic", 0);
            m.SetFloat("_Smoothness", head ? .30f : .18f);
            if (!opacity) {
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textures + "_normal.tga"));
                m.SetFloat("_BumpScale", head ? .55f : .75f); m.EnableKeyword("_NORMALMAP");
            }
            if (opacity) {
                // Soft alpha for hair cards/eyelashes, without glass-like specular.
                m.SetFloat("_Surface", 1); m.SetFloat("_AlphaClip", 0); m.SetFloat("_Cull", 0);
                m.SetFloat("_Blend", 0); m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SpecularHighlights", 0); m.SetFloat("_EnvironmentReflections", 0); m.SetFloat("_BlendModePreserveSpecular", 0);
                m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            EditorUtility.SetDirty(m); return m;
        }

        public static void ImportAndInspect()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
            foreach (string file in Directory.GetFiles("Assets/ThirdParty/MicrosoftRocketbox/Textures")) {
                if (file.EndsWith(".meta")) continue;
                var ti = AssetImporter.GetAtPath(file) as TextureImporter;
                if (!ti) continue;
                ti.maxTextureSize = 2048;
                if (file.Contains("normal")) { ti.textureType = TextureImporterType.NormalMap; ti.convertToNormalmap = false; }
                ti.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = UnityEngine.Object.Instantiate(model);
            var log = new System.Text.StringBuilder();
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                log.AppendLine($"BONE {t.name} | parent={t.parent?.name} | p={t.position:F5} | rot={t.rotation.eulerAngles:F2} | scale={t.lossyScale}");
            foreach (var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                log.AppendLine($"SKIN {r.name} vertices={r.sharedMesh.vertexCount} triangles={r.sharedMesh.triangles.Length / 3} bounds={r.bounds} scale={r.transform.lossyScale}");
                foreach (var m in r.sharedMaterials) log.AppendLine($"MAT {m.name} shader={m.shader.name} texture={(m.mainTexture ? m.mainTexture.name : "none")}");
                var mesh = new Mesh(); r.BakeMesh(mesh);
                var verts = mesh.vertices.Select(v => r.transform.TransformPoint(v)).ToArray();
                var b = new Bounds(verts[0], Vector3.zero); foreach (var v in verts) b.Encapsulate(v);
                log.AppendLine($"BAKED WORLD {b}"); UnityEngine.Object.DestroyImmediate(mesh);
            }
            Directory.CreateDirectory("Tools/AssetResearch"); File.WriteAllText("Tools/AssetResearch/rocketbox-inspection.txt", log.ToString());
            UnityEngine.Object.DestroyImmediate(instance);
            Debug.Log("Rocketbox inspection written.");
        }
    }
}
