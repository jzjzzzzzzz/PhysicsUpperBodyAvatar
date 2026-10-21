// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using UnityEngine;

namespace ALS.HDT
{
    [RequireComponent(typeof(Camera))]
    public class OrbitCamera : MonoBehaviour
    {
        public Vector3 focus = new Vector3(0, 1.40f, .12f);
        public float distance = 2.9f, yaw, pitch = 4;
        public float minimumDistance = 2.15f, maximumDistance = 5;
        private Camera view;
        private bool dragging;
        private Vector3 lastMouse;
        void Awake() { view = GetComponent<Camera>(); Place(); }
        void LateUpdate()
        {
            Vector3 mouse = Input.mousePosition;
            bool inViewport = view.pixelRect.Contains(mouse);
            if (Input.GetMouseButtonDown(0) && inViewport) { dragging = true; lastMouse = mouse; }
            if (!Input.GetMouseButton(0)) dragging = false;
            if (dragging) {
                Vector3 delta = mouse - lastMouse;
                yaw = Mathf.Clamp(yaw + delta.x * .25f, -75, 75);
                pitch = Mathf.Clamp(pitch - delta.y * .2f, -15, 30);
                lastMouse = mouse;
            }
            if (inViewport) distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * .12f, minimumDistance, maximumDistance);
            Place();
        }
        public void Place()
        {
            transform.position = focus + Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, distance);
            transform.LookAt(focus);
        }
    }
}
