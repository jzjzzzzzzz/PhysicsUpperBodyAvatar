# Copyright and license scope

## Original project sources

Copyright (c) 2026 ALS HDT Prototype Contributors.

Beginning with version **0.2.0**, original C#, Python and shell sources in
`Assets/Scripts/`, `Assets/Editor/`, `Assets/Tests/`, `Tools/` and `Tests/` are
licensed under the **Mozilla Public License 2.0**. Original documentation and
repository metadata are under the same license unless individually marked.
See [LICENSE](LICENSE). Source files carry MPL notices and SPDX identifiers.

MPL-2.0 permits commercial use. When distributing covered source or binaries,
make the covered source and modifications available to recipients under the MPL;
separate files that contain no covered code may use other licenses. The license,
not this summary, controls the grant.

## Historical MIT version

Initial commit `d41a9213918e451839bdcddf00846644e1f558d3` used the MIT license for
original code and the generated mannequin. That historical grant is preserved;
this change does not retroactively revoke it. The original notice is retained
in [LICENSES/Legacy-MIT.txt](LICENSES/Legacy-MIT.txt). Git history is intentionally
not rewritten.

## Third-party material

| Material | License / treatment |
|---|---|
| Microsoft Rocketbox FBX, textures and research thumbnails | Copyright 2020 Microsoft; MIT. Full notice at `Assets/ThirdParty/MicrosoftRocketbox/LICENSE.md` |
| Generated mannequin asset | Original MIT grant retained in `LICENSES/Legacy-MIT.txt` |
| Rocketbox-derived physical skin mesh | Retains the source asset's Microsoft MIT notice; not exclusively relicensed under MPL |
| Unity engine, packages, generated templates and default theme assets | Respective upstream Unity/package terms; no license to Unity itself is granted here |

`Assets/ThirdParty/MicrosoftRocketbox/SOURCE.json` records the fixed source commit
and asset hashes. The app ships the Microsoft notice and the project MPL/NOTICE
in StreamingAssets. Redistribution must preserve applicable notices.
