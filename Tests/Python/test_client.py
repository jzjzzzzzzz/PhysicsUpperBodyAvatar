# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
"""Contract tests use an ephemeral loopback server, never the live Unity port."""
from contextlib import contextmanager
import json
import socketserver
import threading
import unittest
from unittest.mock import patch

from Tools.hdt_client import request
from Tools.send_command import send


@contextmanager
def server_with_reply(reply):
    captured = []

    class Handler(socketserver.StreamRequestHandler):
        def handle(self):
            captured.append(self.rfile.readline())
            self.wfile.write(reply)

    with socketserver.TCPServer(("127.0.0.1", 0), Handler) as server:
        worker = threading.Thread(target=server.serve_forever, daemon=True)
        worker.start()
        try:
            yield server.server_address[1], captured
        finally:
            server.shutdown()
            worker.join(timeout=2)


class CommandClientTests(unittest.TestCase):
    def test_round_trip_is_newline_delimited_utf8(self):
        with server_with_reply(b'{"accepted":true}\n') as (port, captured):
            self.assertTrue(request({"action": "idle", "note": "测试"}, port)["accepted"])
            self.assertTrue(captured[0].endswith(b"\n"))
            self.assertEqual(json.loads(captured[0])["note"], "测试")

    def test_server_rejection_is_returned_not_hidden(self):
        with server_with_reply(b'{"accepted":false,"error":"bad"}\n') as (port, _):
            self.assertFalse(request({"action": "bad"}, port)["accepted"])

    def test_nan_is_rejected_before_connecting(self):
        with patch("Tools.hdt_client.socket.create_connection") as connect:
            with self.assertRaises(ValueError):
                request({"values": [float("nan")]})
            connect.assert_not_called()

    def test_oversized_request_is_rejected_before_connecting(self):
        with patch("Tools.hdt_client.socket.create_connection") as connect:
            with self.assertRaises(ValueError):
                request({"action": "a" * 8192})
            connect.assert_not_called()

    def test_invalid_response_shapes_are_rejected(self):
        for payload in (b'[]\n', b'{}\n', b'{"accepted":1}\n', b'bad-json\n'):
            with self.subTest(payload=payload):
                with server_with_reply(payload) as (port, _):
                    with self.assertRaises(ValueError):
                        request({"action": "idle"}, port)

    def test_missing_newline_and_oversized_response_are_rejected(self):
        for payload in (b'{"accepted":true}', b' ' * 8193 + b'\n'):
            with self.subTest(size=len(payload)):
                with server_with_reply(payload) as (port, _):
                    with self.assertRaises(ValueError):
                        request({"action": "idle"}, port)

    def test_empty_response_reports_unavailable_application(self):
        with server_with_reply(b'') as (port, _):
            with self.assertRaises(RuntimeError):
                request({"action": "idle"}, port)

    def test_send_wrapper_preserves_public_api(self):
        with patch("Tools.send_command.request", return_value={"accepted": True}) as call:
            send("wave", 2.5, 9000)
            call.assert_called_once_with({"action": "wave", "duration": 2.5}, port=9000)


if __name__ == "__main__":
    unittest.main()
