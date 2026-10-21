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
