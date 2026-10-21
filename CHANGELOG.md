# Changelog

## 0.2.0 — 2026-10-21

### Changed

- License new original sources under MPL-2.0; preserve historical MIT and all
  Microsoft/Unity third-party notices.
- Separate runtime, Editor and Editor-test assemblies without moving existing
  script GUIDs or replacing the physics-driven rig with animation.
- Split JSON parsing, main-thread dispatch and bounded TCP transport.
- Centralize the thirteen-channel schema; remove temporary ScriptableObject
  allocation during profile validation.
- Share the Python NDJSON client and validate responses and frame boundaries.
- Reset the complete dynamic chain coherently to avoid intermittent reset velocity spikes; retain the existing physics acceptance thresholds.
- Add NUnit contracts, Python tests, hosted quality checks and contributor,
  security and release documentation.
- Rewrite the public README with an English entry point and Chinese guide.

### Preserved

- PhysX body/joint architecture, existing scene generator and all three gestures.
- Loopback JSON command names and 13-value order.
- The uncalibrated nature of the hardware mapping; no real servo output.

## Initial prototype — 2026-10-20

- Physics-based upper-body prototype, Rocketbox skin and 7-arm/6-hand input map.
- Original code and mannequin published in the initial Git commit under MIT.
