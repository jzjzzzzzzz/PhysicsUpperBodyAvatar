using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ALS.HDT
{
    public class LocalCommandServer : MonoBehaviour
    {
        public AvatarController avatar;
        public int port = 8765;
        private TcpListener listener;
        private Thread worker;
        private volatile bool running;
        private readonly ConcurrentQueue<Request> queue = new ConcurrentQueue<Request>();
        private class Request { public string payload, response; public ManualResetEventSlim done = new ManualResetEventSlim(); }
        [Serializable] private class Command { public string action, side; public float duration; public float[] values; }
        [Serializable] private class Reply { public bool accepted; public string action, state, error, side, mode; public float[] targetValues; public bool hardwareCalibrated; }

        void Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-hdtSelfTest") >= 0) return;
            try {
                listener = new TcpListener(IPAddress.Loopback, port); listener.Start(); running = true;
                worker = new Thread(Accept) { IsBackground = true, Name = "HDT localhost command listener" }; worker.Start();
                Debug.Log($"HDT command server: 127.0.0.1:{port}, newline-delimited JSON over TCP.");
            } catch (Exception e) { Debug.LogWarning("Local command listener unavailable: " + e.Message); }
        }

        private void Accept()
        {
            while (running) {
                try { var client = listener.AcceptTcpClient(); ThreadPool.QueueUserWorkItem(_ => Handle(client)); }
                catch (SocketException) { if (!running) break; }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void Handle(TcpClient client)
        {
            try {
                using (client) {
                    client.ReceiveTimeout = 4000; client.SendTimeout = 4000;
                    using (var stream = client.GetStream()) {
                        var bytes = new System.Collections.Generic.List<byte>();
                        int value;
                        while (bytes.Count < 8192 && (value = stream.ReadByte()) >= 0 && value != 10) bytes.Add((byte)value);
                        if (bytes.Count == 0 || bytes.Count >= 8192) return;
                        var request = new Request { payload = Encoding.UTF8.GetString(bytes.ToArray()) };
                        queue.Enqueue(request);
                        if (!request.done.Wait(5000)) return;
                        var response = Encoding.UTF8.GetBytes(request.response + "\n"); stream.Write(response, 0, response.Length);
                    }
                }
            } catch (IOException) { } catch (ObjectDisposedException) { }
        }

        void Update()
        {
            for (int i = 0; i < 8 && queue.TryDequeue(out var request); i++) {
                var reply = new Reply();
                try {
                    var command = JsonUtility.FromJson<Command>(request.payload);
                    if (command == null || string.IsNullOrWhiteSpace(command.action)) throw new ArgumentException("Missing action.");
                    if (command.action == "servo_pose" || command.action == "servo_state") {
                        if (!avatar.servoBridge) throw new ArgumentException("Servo bridge unavailable.");
                        BodySide side = command.side == "left" ? BodySide.Left : command.side == "right" ? BodySide.Right : BodySide.None;
                        if (side == BodySide.None) throw new ArgumentException("side must be left or right.");
                        if (command.action == "servo_pose") avatar.servoBridge.SetPose(side, command.values);
                        reply.side = command.side; reply.mode = avatar.servoBridge.Mode;
                        reply.targetValues = avatar.servoBridge.Targets(side);
                        reply.hardwareCalibrated = avatar.servoBridge.Profile(side).hardwareCalibrated;
                    } else {
                    if (!float.IsFinite(command.duration) || command.duration < 0 || command.duration > 15 || (command.duration > 0 && command.duration < 1.2f))
                        throw new ArgumentException("duration must be 0 (default) or between 1.2 and 15 seconds.");
                    GestureType type;
                    switch (command.action.ToLowerInvariant()) {
                        case "handshake": type = GestureType.Handshake; break;
                        case "wave": type = GestureType.Wave; break;
                        case "double_thumbs_up": type = GestureType.DoubleThumbsUp; break;
                        case "idle": type = GestureType.Idle; break;
                        default: throw new ArgumentException("Unknown action.");
                    }
                    avatar.PlayGesture(type, command.duration);
                    }
                    reply.accepted = true; reply.action = command.action; reply.state = avatar.gestures.State.ToString();
                } catch (Exception e) { reply.error = e.Message; }
                request.response = JsonUtility.ToJson(reply); request.done.Set();
            }
        }

        void OnDestroy() { running = false; listener?.Stop(); worker?.Join(200); }
    }
}
