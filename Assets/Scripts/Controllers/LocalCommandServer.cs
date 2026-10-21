// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;
using UnityEngine;

namespace ALS.HDT
{
    /// <summary>Unity lifecycle adapter: transports bytes off-thread, dispatches on Update.</summary>
    public sealed class LocalCommandServer : MonoBehaviour
    {
        public AvatarController avatar;
        public int port = 8765;
        [Min(1)] public int maximumClients = 4, queueCapacity = 32, commandsPerFrame = 8;
        [Min(1)] public int ioTimeoutMs = 4000, commandTimeoutMs = 5000;
        private LocalCommandTransport transport;
        private AvatarCommandDispatcher dispatcher;

        private void OnEnable()
        {
            if (!Application.isPlaying || Array.IndexOf(Environment.GetCommandLineArgs(), "-hdtSelfTest") >= 0) return;
            try {
                dispatcher = new AvatarCommandDispatcher(avatar);
                transport = new LocalCommandTransport(port, maximumClients, queueCapacity, ioTimeoutMs, commandTimeoutMs);
                Debug.Log($"HDT command server: 127.0.0.1:{transport.Port}, newline-delimited JSON over TCP.");
            } catch (Exception error) {
                Debug.LogWarning("Local command listener unavailable: " + error.Message);
                transport?.Dispose(); transport = null;
            }
        }

        private void Update()
        {
            if (transport == null) return;
            for (int i = 0; i < commandsPerFrame && transport.TryDequeue(out var request); i++) {
                if (!request.TryBegin()) continue;
                try { request.Complete(JsonUtility.ToJson(dispatcher.Execute(request.Payload))); }
                catch (Exception error) {
                    Debug.LogException(error);
                    request.Complete(JsonUtility.ToJson(new AvatarCommandReply { error = "Internal command failure." }));
                }
            }
        }

        private void OnDisable() { transport?.Dispose(); transport = null; }
        private void OnDestroy() => OnDisable();
    }
}
