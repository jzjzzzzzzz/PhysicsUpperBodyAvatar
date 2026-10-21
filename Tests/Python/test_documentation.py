# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
import unittest
from Tools.check_documentation import validate


class DocumentationTests(unittest.TestCase):
    def test_local_links_examples_and_channel_order(self):
        result = validate()
        self.assertEqual(result["ordered_channels"], 13)
        self.assertGreater(result["local_links"], 0)
        self.assertGreater(result["python_examples"], 0)


if __name__ == "__main__":
    unittest.main()
