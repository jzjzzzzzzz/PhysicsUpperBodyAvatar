# Physics Upper-Body Avatar

**A local, physics-driven upper-body human prototype built with Unity 6 and PhysX.**

English · [简体中文](README.zh-CN.md) · [Getting started](Documentation/GETTING_STARTED.md) · [Contributing](CONTRIBUTING.md) · [License](LICENSE)

The avatar performs **handshake**, **wave** and **double thumbs-up** gestures by
tracking target joint trajectories with bounded physical motors. The visible
skin follows simulated rigidbodies—not an Animator or a transform-driven pose.
A provisional **7-arm + 6-hand input map** connects one robotic upper limb to the
virtual human without treating every finger joint as a separate servo channel.

![Physics-driven wave in the standalone application](TestResults/Standalone/Wave.png)

## Status and scope

Version **0.2.0** is a research prototype for the ALS Human Digital Twin project.
It includes an actual wired scene, textured humanoid skin, gestures, contact
tests, a desktop UI and a localhost command interface.

The hardware map is **not calibrated to the photographed device**. No real-servo
output, EEG, AI model, TTS, motion capture, patient fitting or cloud service is
implemented. Head/arm/finger physics does not imply a validated biomechanical or
clinical model.

## Features

- **Active ragdoll:** 41 rigidbodies and 39 joint motors; torso/neck anchored,
  both arms, hands, fingers and head dynamically simulated.
- **Finite actuation:** ConfigurableJoint angular drives and a bounded elbow PD
  torque pair; gravity, inertia, collision resistance and recovery remain active.
- **Textured human:** Microsoft Rocketbox upper-body skin bound directly to the
  physical skeleton, including articulated fingers.
- **13 logical inputs per side:** shoulder 3, elbow 1, forearm 1, wrist 2;
  four finger curls and two thumb inputs. One curl drives multiple physical joints.
- **Local interaction:** gesture buttons, input sliders, pause/reset, orbit camera,
  movable test object, resistance gate and diagnostics.
- **Modular control:** separate wire protocol, main-thread dispatcher and bounded
  loopback transport; no Unity calls on socket workers.

## Requirements

| Item | Supported / validated configuration |
|---|---|
| Editor | Unity **6000.5.7f1** |
| Rendering | URP **17.5.0** |
| Model import | FBX; glTFast **6.20.0** included for GLB/glTF |
| Tested player | macOS Apple Silicon, **ARM64 + Mono**, Metal |
| Optional tools | Python **3.11+** for clients and repository checks; no pip dependencies |

The built application runs offline without Unity or Python. A fresh source import
needs package-registry access once. Other OS/architecture builds are not yet
validated; do not treat the macOS ARM64 package as a Windows or Intel build.

## Quick start

```bash
git clone https://github.com/jzjzzzzzzz/PhysicsUpperBodyAvatar.git
cd PhysicsUpperBodyAvatar
```

1. Add this folder in Unity Hub and open it with the pinned Editor.
2. Open `Assets/Scenes/UpperBodyLab.unity` and press **Play**.
3. Try **Handshake**, **Wave**, **Double Thumbs-Up**, then **Return to Idle**.
4. Expand **Servo mapping / 13 channels** to test individual arm/finger inputs.
5. Enable the resistance gate and repeat Handshake to observe contact resistance.

Build locally with `Tools/unity_task.sh all`, or use the **ALS HDT** Editor menu.
The app is written to `Builds/macOS/PhysicsUpperBodyAvatar.app`. Build outputs and
ZIPs are generated locally, not committed to this repository.

Detailed UI instructions: [快速使用指南](Documentation/GETTING_STARTED.md).

## Architecture

```text
Gesture trajectories ── 13-channel projection ─┐
                                             ├─ target skeleton ─ motors ─ PhysX ─ visible skin
Sliders / TCP ─ validation ─ input slew limit ─┘                       ↑
                                                            gravity / inertia / contact
```

| Module | Responsibility |
|---|---|
| `Assets/Scripts/Physics/` | Physical bones, joint-space conversion and bounded motors |
| `Assets/Scripts/Gestures/` | Pose trajectories, blending and gesture state machine |
| `Assets/Scripts/Hardware/` | Canonical channel schema, calibration and finger synergies |
| `Assets/Scripts/Networking/` | JSON contracts, scene dispatch and bounded loopback transport |
| `Assets/Scripts/Controllers/` | Avatar lifecycle, interactions and Unity adapters |
| `Assets/Scripts/Avatar/`, `UI/`, `Diagnostics/` | Physical skin, desktop controls and measurements |
| `Assets/Editor/` | Reproducible scene generation, skin binding, validation and builds |
| `Assets/Tests/Editor/`, `Tests/Python/` | NUnit and standard-library contract tests |

Runtime, Editor and Editor tests have separate assembly definitions. Existing
script GUIDs are preserved. See [architecture](Documentation/ARCHITECTURE.md),
[joint parameters](Documentation/PHYSICS.md) and [hardware mapping](Documentation/HARDWARE_MAPPING.md).

## Command API

Normal launches listen on **127.0.0.1:8765** using TCP and newline-delimited JSON,
not HTTP. One request/response per connection:

```json
{"action":"wave","duration":2.5}
{"action":"servo_state","side":"right"}
```

```bash
python3 Tools/send_command.py wave --duration 2.5
python3 Tools/send_servo.py --side right
python3 Tools/send_command.py idle
```

`servo_pose` takes exactly 13 angles in `A1..A7,H1..H6` order. Responses acknowledge
target acceptance, not completed movement or encoder feedback. Any local process
can connect; the interface is not intended for direct network exposure.

See the complete [API contract](Documentation/COMMAND_API.md) and [security policy](SECURITY.md).

## Validation

```bash
python3 -m unittest discover -s Tests/Python -v
python3 Tools/check_documentation.py
python3 Tools/check_repository.py
Tools/unity_task.sh test       # NUnit EditMode contracts
Tools/unity_task.sh all        # rebuild scene, physical regression, macOS build
Tools/runtime_test.sh          # real Metal window, gestures, contacts and UI events
# Launch a normal app instance separately, then:
python3 Tools/test_commands.py # live TCP acceptance/rejection checks
```

Reports and actual screenshots are committed under `TestResults/`. GitHub Actions
runs **Python and repository checks only**; Unity physics and player checks require
the licensed local Editor. Synthetic UI events are not native mouse end-to-end tests.
See [test scope/results](Documentation/TESTING.md) and [release procedure](Documentation/RELEASING.md).

## Limitations

The torso/neck are fixed. Hand self-collision is simplified, and finger synergies
are target coupling—not a closed-loop simulation of the photographed linkage.
Skinning is linear blend skinning, not soft tissue or cloth. The low-poly character
has static facial expression and transparency-card hair. Joint tuning and hardware
limits are prototype values, not measured patient or servo parameters.

## License and provenance

Original sources from **0.2.0** use **MPL-2.0**: commercial use is allowed; distributed
covered files and their modifications remain available under MPL. Independent
files need not adopt MPL merely because they are used alongside this code.

The initial MIT-licensed commit remains MIT; its grant is not revoked. Microsoft
Rocketbox assets and the derived skin retain Microsoft MIT notices. Unity and
packages retain their own terms. See [LICENSE](LICENSE), [NOTICE](NOTICE.md),
[model provenance](Documentation/REALISTIC_AVATAR.md) and [references](Documentation/REFERENCES.md).

Contributions are welcome through focused issues and pull requests. Start with
[CONTRIBUTING.md](CONTRIBUTING.md) and the [community guidelines](CODE_OF_CONDUCT.md).
