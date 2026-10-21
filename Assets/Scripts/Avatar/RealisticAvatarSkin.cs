// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 ALS HDT Prototype Contributors.
using UnityEngine;

namespace ALS.HDT
{
    // Metadata only. There is intentionally no Update/LateUpdate bone follower.
    public class RealisticAvatarSkin : MonoBehaviour
    {
        public string assetName = "Microsoft Rocketbox / Male_Adult_04";
        public string source = "https://github.com/microsoft/Microsoft-Rocketbox";
        public string license = "MIT — Copyright (c) 2020 Microsoft";
        public SkinnedMeshRenderer skin;
    }
}
