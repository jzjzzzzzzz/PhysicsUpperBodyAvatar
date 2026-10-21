// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ALS.HDT
{
    [ExecuteAlways, RequireComponent(typeof(SkinnedMeshRenderer))]
    public class AnatomicalSkin : MonoBehaviour
    {
        public AvatarController avatar;
        public Material[] materials;
        public Mesh GeneratedMesh { get; private set; }
        private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<BoneWeight> weights = new List<BoneWeight>();
        private List<int>[] triangles;
        private PhysicsBone[] bones;
        private Dictionary<string, int> indices;

        void OnEnable() { if (avatar && !GetComponent<SkinnedMeshRenderer>().sharedMesh) Rebuild(); }

        public void Rebuild()
        {
            if (!avatar || avatar.bones == null || avatar.bones.Length == 0) return;
            bones = avatar.bones; indices = bones.Select((b, i) => (b, i)).ToDictionary(v => v.b.id, v => v.i);
            vertices.Clear(); normals.Clear(); uv.Clear(); weights.Clear();
            triangles = Enumerable.Range(0, 4).Select(_ => new List<int>()).ToArray();
            TorsoSurface();
            Ellipsoid("Neck", new Vector3(0, .045f, 0), new Vector3(.047f, .065f, .05f), 0);
            Ellipsoid("Head", new Vector3(0, .12f, 0), new Vector3(.087f, .125f, .095f), 0, 28, 22);
            Ellipsoid("Head", new Vector3(0, .115f, .092f), new Vector3(.016f, .026f, .025f), 0);
            foreach (float sign in new[] { -1f, 1f }) {
                Ellipsoid("Head", new Vector3(sign * .092f, .125f, .005f), new Vector3(.018f, .034f, .015f), 0);
                Ellipsoid("Head", new Vector3(sign * .032f, .151f, .080f), new Vector3(.018f, .010f, .008f), 2);
                Ellipsoid("Head", new Vector3(sign * .032f, .151f, .087f), new Vector3(.007f, .007f, .003f), 3);
                Ellipsoid("Head", new Vector3(sign * .033f, .173f, .078f), new Vector3(.023f, .003f, .005f), 3);
            }
            Ellipsoid("Head", new Vector3(0, .071f, .087f), new Vector3(.023f, .003f, .004f), 3);
            foreach (string side in new[] { "L", "R" }) {
                ArmTube(side);
                Ellipsoid(side + "_UpperArm", new Vector3(0, -.012f, 0), new Vector3(.064f, .070f, .063f), 0, 20, 16);
                Ellipsoid(side + "_Hand", new Vector3(0, -.056f, 0), new Vector3(.046f, .072f, .024f), 0);
                var fingers = bones.Where(b => b.side == (side == "R" ? BodySide.Right : BodySide.Left) && b.role == BodyRole.Finger);
                foreach (var root in fingers.Where(b => b.phalanxIndex == 0)) FingerTube(root);
                foreach (var b in fingers) {
                    float radius = b.fingerIndex == 4 ? .0115f : b.fingerIndex == 3 ? .008f : .0092f;
                    if (b.phalanxIndex == 2) Ellipsoid(b.id, new Vector3(0, -b.length * .62f, -radius * .9f), new Vector3(radius * .6f, b.length * .23f, .0017f), 2, 10, 8);
                }
            }
            var mesh = new Mesh { name = "Anatomical upper-body skinned mannequin", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bones.Select(b => Matrix4x4.TRS(b.resetPosition, b.resetRotation, Vector3.one).inverse * transform.localToWorldMatrix).ToArray();
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateBounds();
            var renderer = GetComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.bones = bones.Select(b => b.transform).ToArray();
            renderer.rootBone = avatar.Find("Torso").transform;
            renderer.sharedMaterials = materials; renderer.updateWhenOffscreen = true;
            renderer.localBounds = new Bounds(new Vector3(0, 1.4f, 0), new Vector3(3.5f, 3, 3));
            GeneratedMesh = mesh;
        }

        private void AddVertex(Vector3 world, Vector3 normal, Vector2 texcoord, BoneWeight weight)
        {
            vertices.Add(transform.InverseTransformPoint(world)); normals.Add(transform.InverseTransformDirection(normal).normalized);
            uv.Add(texcoord); weights.Add(weight);
        }

        private void Ellipsoid(string id, Vector3 center, Vector3 scale, int material, int longitude = 16, int latitude = 12)
        {
            int boneIndex = indices[id], start = vertices.Count;
            var b = bones[boneIndex]; var weight = new BoneWeight { boneIndex0 = boneIndex, weight0 = 1 };
            for (int y = 0; y <= latitude; y++) for (int x = 0; x <= longitude; x++) {
                float theta = y * Mathf.PI / latitude, phi = x * 2 * Mathf.PI / longitude;
                Vector3 unit = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                Vector3 normal = new Vector3(unit.x / scale.x, unit.y / scale.y, unit.z / scale.z).normalized;
                AddVertex(b.resetPosition + b.resetRotation * (center + Vector3.Scale(unit, scale)), b.resetRotation * normal, new Vector2((float)x / longitude, (float)y / latitude), weight);
            }
            Grid(start, longitude, latitude, material);
        }

        private void ArmTube(string side)
        {
            int upper = indices[side + "_UpperArm"], lower = indices[side + "_Forearm"];
            var root = bones[upper]; float a = avatar.profile.upperArmLength, b = avatar.profile.forearmLength;
            const int around = 24, rings = 30; int start = vertices.Count;
            for (int r = 0; r <= rings; r++) {
                float d = Mathf.Lerp(-.025f, a + b, (float)r / rings);
                float radius = d <= a ? Mathf.Lerp(.055f, .038f, Mathf.Clamp01(d / a)) : Mathf.Lerp(.038f, .024f, (d - a) / b);
                if (r == 0) radius = .002f;
                float blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a - .035f, a + .035f, d));
                var weight = new BoneWeight { boneIndex0 = upper, weight0 = 1 - blend, boneIndex1 = lower, weight1 = blend };
                for (int i = 0; i <= around; i++) {
                    float phi = i * 2 * Mathf.PI / around;
                    var radial = new Vector3(Mathf.Cos(phi), 0, Mathf.Sin(phi));
                    AddVertex(root.resetPosition + Vector3.down * d + radial * radius, radial, new Vector2((float)i / around, (float)r / rings), weight);
                }
            }
            Grid(start, around, rings, 0);
        }

        private void TorsoSurface()
        {
            // A connected chest/waist silhouette, with clavicular width reaching
            // the actual shoulder centres rather than disconnected oval parts.
            float[] y = { .32f, .30f, .27f, .23f, .16f, .04f, -.10f, -.23f, -.31f };
            float[] width = { .050f, .12f, .205f, .238f, .232f, .207f, .179f, .153f, .145f };
            float[] depth = { .048f, .070f, .107f, .123f, .135f, .136f, .124f, .105f, .098f };
            const int around = 32; int start = vertices.Count;
            var b = bones[indices["Torso"]];
            var weight = new BoneWeight { boneIndex0 = indices["Torso"], weight0 = 1 };
            for (int r = 0; r < y.Length; r++) for (int i = 0; i <= around; i++) {
                float phi = i * 2 * Mathf.PI / around;
                var p = new Vector3(Mathf.Cos(phi) * width[r], y[r], Mathf.Sin(phi) * depth[r]);
                int before = Mathf.Max(0, r - 1), after = Mathf.Min(y.Length - 1, r + 1);
                float dx = (width[after] - width[before]) / (y[after] - y[before]);
                float dz = (depth[after] - depth[before]) / (y[after] - y[before]);
                float ny = -dx / width[r] * Mathf.Cos(phi) * Mathf.Cos(phi) - dz / depth[r] * Mathf.Sin(phi) * Mathf.Sin(phi);
                var n = new Vector3(Mathf.Cos(phi) / width[r], ny, Mathf.Sin(phi) / depth[r]).normalized;
                AddVertex(b.resetPosition + b.resetRotation * p, b.resetRotation * n, new Vector2((float)i / around, (float)r / (y.Length - 1)), weight);
            }
            Grid(start, around, y.Length - 1, 1);
        }

        private void FingerTube(PhysicsBone proximal)
        {
            var chain = bones.Where(b => b.side == proximal.side && b.role == BodyRole.Finger && b.fingerIndex == proximal.fingerIndex)
                .OrderBy(b => b.phalanxIndex).ToArray();
            float a = chain[0].length, b = chain[1].length, total = a + b + chain[2].length;
            float baseRadius = proximal.fingerIndex == 4 ? .0105f : proximal.fingerIndex == 3 ? .008f : .009f;
            const int around = 12, rings = 22; int start = vertices.Count;
            for (int r = 0; r <= rings; r++) {
                float d = Mathf.Lerp(-.004f, total + .002f, (float)r / rings);
                int first = 0, second = 0; float blend = 0;
                if (d > a + b - .006f) { first = 1; second = 2; blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a + b - .006f, a + b + .006f, d)); }
                else if (d > a - .006f) { first = 0; second = 1; blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a - .006f, a + .006f, d)); }
                float radius = Mathf.Lerp(baseRadius, baseRadius * .74f, Mathf.Clamp01(d / total));
                if (r == rings) radius = .001f;
                var weight = new BoneWeight { boneIndex0 = indices[chain[first].id], weight0 = 1 - blend, boneIndex1 = indices[chain[second].id], weight1 = blend };
                for (int i = 0; i <= around; i++) {
                    float phi = i * 2 * Mathf.PI / around;
                    Vector3 radial = new Vector3(Mathf.Cos(phi), 0, Mathf.Sin(phi));
                    AddVertex(proximal.resetPosition + proximal.resetRotation * (Vector3.down * d + radial * radius),
                        proximal.resetRotation * radial, new Vector2((float)i / around, (float)r / rings), weight);
                }
            }
            Grid(start, around, rings, 0);
        }

        private void Grid(int start, int width, int height, int material)
        {
            var t = triangles[material];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
                int a = start + y * (width + 1) + x, b = a + width + 1;
                t.Add(a); t.Add(a + 1); t.Add(b); t.Add(a + 1); t.Add(b + 1); t.Add(b);
            }
        }
    }
}
