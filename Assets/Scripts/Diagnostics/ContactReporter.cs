// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using UnityEngine;

namespace ALS.HDT
{
    public class ContactReporter : MonoBehaviour
    {
        public int ActiveContacts { get; private set; }
        public int TotalImpacts { get; private set; }
        public float LastImpulse { get; private set; }
        public string LastOther { get; private set; } = "none";
        private float lastContactTime;
        void OnCollisionEnter(Collision collision) {
            if (collision.collider.GetComponent<PhysicsBone>()) return;
            TotalImpacts++; Record(collision);
        }
        void OnCollisionStay(Collision collision) => Record(collision);
        void Record(Collision collision)
        {
            if (collision.collider.GetComponent<PhysicsBone>()) return;
            ActiveContacts = collision.contactCount; LastImpulse = collision.impulse.magnitude;
            LastOther = collision.gameObject.name; lastContactTime = Time.time;
        }
        void Update() { if (Time.time - lastContactTime > .12f) ActiveContacts = 0; }
    }
}
