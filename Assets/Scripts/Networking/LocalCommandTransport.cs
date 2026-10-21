// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ALS.HDT
{
    internal sealed class PendingAvatarCommand
    {
        private readonly TaskCompletionSource<string> completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Stopwatch age = Stopwatch.StartNew();
        private readonly TimeSpan lifetime;
        private int state; // 0 queued; 1 executing; 2 completed/cancelled
        public string Payload { get; }

        public PendingAvatarCommand(string payload, TimeSpan lifetime) { Payload = payload; this.lifetime = lifetime; }
        public bool TryBegin()
        {
            if (age.Elapsed >= lifetime) { Cancel(); return false; }
            return Interlocked.CompareExchange(ref state, 1, 0) == 0;
        }
        public void Complete(string reply)
        {
            if (Interlocked.CompareExchange(ref state, 2, 1) == 1) completion.TrySetResult(reply);
        }
        public void Cancel()
        {
            if (Interlocked.CompareExchange(ref state, 2, 0) == 0) completion.TrySetResult(LocalCommandTransport.TimeoutReply);
        }
        public string Wait(int milliseconds)
        {
            if (completion.Task.Wait(milliseconds)) return completion.Task.Result;
            Cancel();
            return LocalCommandTransport.TimeoutReply;
        }
    }

    /// <summary>Bounded loopback-only NDJSON transport. Does not call any Unity API.</summary>
    internal sealed class LocalCommandTransport : IDisposable
    {
        internal const string TimeoutReply = "{\"accepted\":false,\"error\":\"Command expired before a response was available.\"}";
        private const string BusyReply = "{\"accepted\":false,\"error\":\"Server is busy.\"}";
        private const string FrameReply = "{\"accepted\":false,\"error\":\"Invalid request frame.\"}";
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly TcpListener listener;
        private readonly Thread acceptThread;
        private readonly BlockingCollection<PendingAvatarCommand> requests;
        private readonly ConcurrentDictionary<TcpClient, byte> clients = new ConcurrentDictionary<TcpClient, byte>();
        private readonly int maximumClients, ioTimeoutMs, commandTimeoutMs;
        private int activeClients;
        private volatile bool running;
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public LocalCommandTransport(int port, int maximumClients = 4, int queueCapacity = 32, int ioTimeoutMs = 4000, int commandTimeoutMs = 5000)
        {
            if (port < 0 || port > 65535 || maximumClients < 1 || queueCapacity < 1 || ioTimeoutMs < 1 || commandTimeoutMs < 1)
                throw new ArgumentOutOfRangeException(nameof(port), "Invalid command-server settings.");
            this.maximumClients = maximumClients;
            this.ioTimeoutMs = ioTimeoutMs;
            this.commandTimeoutMs = commandTimeoutMs;
            requests = new BlockingCollection<PendingAvatarCommand>(queueCapacity);
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            running = true;
            acceptThread = new Thread(Accept) { IsBackground = true, Name = "HDT loopback commands" };
            acceptThread.Start();
        }

        public bool TryDequeue(out PendingAvatarCommand command) => requests.TryTake(out command);

        private void Accept()
        {
            while (running) {
                try {
                    var client = listener.AcceptTcpClient();
                    client.SendTimeout = client.ReceiveTimeout = ioTimeoutMs;
                    if (Interlocked.Increment(ref activeClients) > maximumClients) {
                        Interlocked.Decrement(ref activeClients);
                        using (client) { try { Write(client.GetStream(), BusyReply); } catch (IOException) { } }
                        continue;
                    }
                    clients.TryAdd(client, 0);
                    if (!running) { Close(client); break; }
                    ThreadPool.QueueUserWorkItem(_ => Handle(client));
                } catch (SocketException) { if (!running) break; }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void Handle(TcpClient client)
        {
            PendingAvatarCommand pending = null;
            try {
                using (var stream = client.GetStream()) {
                    var bytes = new List<byte>();
                    int value = -1;
                    var frameAge = Stopwatch.StartNew();
                    while (bytes.Count < AvatarCommandProtocol.MaximumFrameBytes && frameAge.ElapsedMilliseconds < ioTimeoutMs) {
                        stream.ReadTimeout = Math.Max(1, ioTimeoutMs - (int)frameAge.ElapsedMilliseconds);
                        value = stream.ReadByte();
                        if (value < 0 || value == 10) break;
                        bytes.Add((byte)value);
                    }
                    if (value != 10 || bytes.Count == 0 || bytes.Count >= AvatarCommandProtocol.MaximumFrameBytes) {
                        Write(stream, FrameReply); return;
                    }
                    string payload;
                    try { payload = Utf8.GetString(bytes.ToArray()); }
                    catch (DecoderFallbackException) { Write(stream, FrameReply); return; }
                    pending = new PendingAvatarCommand(payload, TimeSpan.FromMilliseconds(commandTimeoutMs));
                    if (!running || !requests.TryAdd(pending)) { Write(stream, BusyReply); return; }
                    Write(stream, pending.Wait(commandTimeoutMs));
                }
            } catch (IOException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) when (!running) { }
            finally { pending?.Cancel(); Close(client); }
        }

        private static void Write(Stream stream, string reply)
        {
            byte[] bytes = Utf8.GetBytes(reply + "\n");
            stream.Write(bytes, 0, bytes.Length);
        }

        private void Close(TcpClient client)
        {
            client.Close();
            if (clients.TryRemove(client, out _)) Interlocked.Decrement(ref activeClients);
        }

        public void Dispose()
        {
            if (!running) return;
            running = false;
            listener.Stop();
            foreach (var client in clients.Keys) Close(client);
            while (requests.TryTake(out var pending)) pending.Cancel();
            acceptThread.Join(500);
            // Workers may still unwind after Close: do not dispose their shared queue.
        }
    }
}
