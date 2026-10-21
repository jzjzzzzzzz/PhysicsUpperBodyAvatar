using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ALS.HDT.Editor
{
    // Import assets using Unity FBX / glTFast GLB importers, then bind a calibrated
    // rest pose directly to physical bones. No runtime animated bone follower.
    public class PhysicalSkinBinderWindow : EditorWindow
    {
        private AvatarController avatar;
        private SkinnedMeshRenderer source;
        private Animator humanoid;
        private readonly Dictionary<Transform, string> mapping = new Dictionary<Transform, string>();
        private Vector2 scroll;
        private float lowerCutoff = .92f;

        [MenuItem("ALS HDT/Bind imported FBX or GLB upper-body skin")]
        static void Open() => GetWindow<PhysicalSkinBinderWindow>("Physical Skin Binding");

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Editor-only skin binding. Import FBX or GLB into Assets/Models, instantiate it, and align its upper-body REST pose and joint centres to the physical mannequin. The source must be +Z facing, Y up, in metres. This tool bakes that pose; it does not animate physical limbs or automatically calibrate arbitrary body proportions.", MessageType.Info);
            avatar = (AvatarController)EditorGUILayout.ObjectField("Physical avatar", avatar, typeof(AvatarController), true);
            var next = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Source skin renderer", source, typeof(SkinnedMeshRenderer), true);
            if (next != source) { source = next; mapping.Clear(); }
            humanoid = (Animator)EditorGUILayout.ObjectField("Optional Humanoid Animator", humanoid, typeof(Animator), true);
            lowerCutoff = EditorGUILayout.FloatField("World lower crop Y (m)", lowerCutoff);
            if (GUILayout.Button("Populate mapping from Humanoid Avatar")) Populate();
            if (source && avatar) {
                string[] ids = new[] { "— exclude —" }.Concat(avatar.bones.Select(b => b.id)).ToArray();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                foreach (var bone in source.bones.Where(b => b).Distinct()) {
                    string current = mapping.TryGetValue(bone, out var id) ? id : "— exclude —";
                    int index = Mathf.Max(0, Array.IndexOf(ids, current));
                    int selected = EditorGUILayout.Popup(bone.name, index, ids);
                    mapping[bone] = selected == 0 ? "" : ids[selected];
                }
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUI.DisabledScope(!source || !avatar || EditorApplication.isPlaying))
                if (GUILayout.Button("Bake aligned upper body → physical skin")) {
                    try { Bake(); } catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("Skin binding", e.Message, "OK"); }
                }
        }

        private void Populate()
        {
            if (!humanoid || !humanoid.isHuman) { EditorUtility.DisplayDialog("Humanoid mapping", "Select an Animator with a valid Humanoid Avatar. Generic GLB rigs can use the explicit bone mapping list instead.", "OK"); return; }
            mapping.Clear();
            void Map(HumanBodyBones bone, string id) { var t = humanoid.GetBoneTransform(bone); if (t) mapping[t] = id; }
            foreach (var b in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest }) Map(b, "Torso");
            Map(HumanBodyBones.Neck, "Neck"); Map(HumanBodyBones.Head, "Head"); Map(HumanBodyBones.Jaw, "Head");
            Map(HumanBodyBones.LeftEye, "Head"); Map(HumanBodyBones.RightEye, "Head");
            foreach (string side in new[] { "Left", "Right" }) {
                string s = side == "Left" ? "L" : "R";
                foreach (string part in new[] { "Shoulder", "UpperArm", "LowerArm", "Hand" }) {
                    string destination = part == "Shoulder" ? "Torso" : s + "_" + (part == "LowerArm" ? "Forearm" : part);
                    Map((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + part), destination);
                }
                foreach (string finger in new[] { "Index", "Middle", "Ring", "Little", "Thumb" }) {
                    string[] segments = { "Proximal", "Intermediate", "Distal" };
                    for (int i = 0; i < 3; i++) Map((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + finger + segments[i]), s + "_" + finger + "_" + i);
                }
            }
        }

        private void Bake()
        {
            if (source.GetComponent<AnatomicalSkin>()) throw new Exception("Select the imported model's skin, not the generated fallback mannequin.");
            if (!source.sharedMesh.isReadable) throw new Exception("Enable Read/Write on the imported mesh before binding.");
            var weights = source.sharedMesh.boneWeights;
            if (weights.Length != source.sharedMesh.vertexCount) throw new Exception("This binder requires a skinned mesh with standard four-weight vertices. Set importer skin weights to Standard (4 Bones).");
            var bones = avatar.bones;
            int[] remap = source.bones.Select(b => b && mapping.TryGetValue(b, out var id) ? Array.FindIndex(bones, p => p.id == id) : -1).ToArray();
            var baked = new Mesh(); source.BakeMesh(baked);
            Vector3[] vertices = baked.vertices, normals = baked.normals;
            bool[] retain = new bool[vertices.Length];
            var newWeights = new BoneWeight[weights.Length];
            for (int v = 0; v < vertices.Length; v++) {
                Vector3 world = source.transform.TransformPoint(vertices[v]);
                vertices[v] = avatar.transform.InverseTransformPoint(world);
                if (normals.Length == vertices.Length) normals[v] = avatar.transform.InverseTransformDirection(source.transform.TransformDirection(normals[v])).normalized;
                var w = weights[v]; var bw = new BoneWeight();
                int[] bi = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                float[] ws = { w.weight0, w.weight1, w.weight2, w.weight3 };
                float total = 0; int[] dest = new int[4];
                for (int i = 0; i < 4; i++) { dest[i] = bi[i] < remap.Length ? remap[bi[i]] : -1; if (dest[i] >= 0) total += ws[i]; else ws[i] = 0; }
                retain[v] = total > .99f && world.y >= lowerCutoff;
                if (total > 0) {
                    bw.boneIndex0 = Mathf.Max(0, dest[0]); bw.weight0 = ws[0] / total;
                    bw.boneIndex1 = Mathf.Max(0, dest[1]); bw.weight1 = ws[1] / total;
                    bw.boneIndex2 = Mathf.Max(0, dest[2]); bw.weight2 = ws[2] / total;
                    bw.boneIndex3 = Mathf.Max(0, dest[3]); bw.weight3 = ws[3] / total;
                }
                newWeights[v] = bw;
            }
            baked.name = source.name + " — physical upper-body skin";
            baked.vertices = vertices; if (normals.Length == vertices.Length) baked.normals = normals;
            baked.boneWeights = newWeights; baked.tangents = Array.Empty<Vector4>();
            baked.bindposes = bones.Select(b => Matrix4x4.TRS(b.resetPosition, b.resetRotation, Vector3.one).inverse * avatar.transform.localToWorldMatrix).ToArray();
            int triangleCount = 0;
            for (int s = 0; s < baked.subMeshCount; s++) {
                var old = baked.GetTriangles(s); var triangles = new List<int>();
                for (int i = 0; i < old.Length; i += 3) if (retain[old[i]] && retain[old[i + 1]] && retain[old[i + 2]]) {
                    triangles.Add(old[i]); triangles.Add(old[i + 1]); triangles.Add(old[i + 2]);
                }
                triangleCount += triangles.Count / 3; baked.SetTriangles(triangles, s);
            }
            if (triangleCount == 0) { DestroyImmediate(baked); throw new Exception("No upper-body triangles survived. Check bone mapping, world-space alignment and crop height."); }
            baked.RecalculateBounds(); baked.RecalculateTangents();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Models/ImportedPhysicalSkin.asset"); AssetDatabase.CreateAsset(baked, path);
            var go = new GameObject("Imported physical skin"); Undo.RegisterCreatedObjectUndo(go, "Bind physical skin"); go.transform.SetParent(avatar.transform, false);
            var renderer = go.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = baked; renderer.sharedMaterials = source.sharedMaterials;
            renderer.bones = bones.Select(b => b.transform).ToArray(); renderer.rootBone = avatar.Find("Torso").transform;
            renderer.updateWhenOffscreen = true; renderer.localBounds = new Bounds(new Vector3(0, 1.4f, 0), new Vector3(3.5f, 3, 3));
            Undo.RecordObject(source, "Hide source skin"); source.enabled = false;
            foreach (var fallback in avatar.GetComponentsInChildren<AnatomicalSkin>()) { Undo.RecordObject(fallback.GetComponent<SkinnedMeshRenderer>(), "Hide mannequin"); fallback.GetComponent<SkinnedMeshRenderer>().enabled = false; }
            EditorUtility.SetDirty(avatar.gameObject); AssetDatabase.SaveAssets(); Selection.activeGameObject = go;
            Debug.Log($"Bound {triangleCount} upper-body triangles to physical bodies: {path}. Validate rest alignment, weights, materials and elbow deformation in Play mode.");
        }
    }
}
