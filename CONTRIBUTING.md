# Contributing

Thanks for improving the prototype. Small, focused changes with reproducible
validation are preferred over broad formatting-only changes.

## Development setup

1. Clone the repository, including its Unity `.meta` files.
2. Use Unity **6000.5.7f1** with macOS build support; package versions are locked.
3. Open `Assets/Scenes/UpperBodyLab.unity`.
4. Run the tests below before submitting a change.

```bash
python3 -m unittest discover -s Tests/Python -v
python3 Tools/check_documentation.py
python3 Tools/check_repository.py
Tools/unity_task.sh test
Tools/unity_task.sh all
Tools/runtime_test.sh
# Start a normal app instance, then:
python3 Tools/test_commands.py
```

Hosted CI runs Python and repository checks only. Unity/PhysX and Metal player
validation require the local licensed Editor; do not describe a Python CI pass
as a physics or rendering pass.

## Module boundaries

- Runtime code lives in `ALS.HDT.Runtime`; never import `UnityEditor` there.
- Editor setup and binding tools live in `ALS.HDT.Editor`.
- NUnit contracts live in `ALS.HDT.Tests.Editor`.
- Socket transport must not call Unity APIs. Scene mutations run on `Update`.
- Keep channel order in `ServoRigSchema`; do not add a hidden eighth arm input.
- Change non-rendered targets, not visible limb transforms. Only explicit reset
  may teleport rigidbodies.

## Pull requests

Explain the problem, the change, validation commands and remaining limitations.
Include screenshots for visible changes and fresh reports for physics changes.
Do not commit `Library`, `Logs`, `Builds`, `Distribution`, credentials, real
patient data or personal recordings. Do not change generated scenes without
regenerating them through the builder and checking the resulting diff.

Keep changes compatible with the existing JSON contract unless a protocol
change is documented. Use LF line endings and the supplied `.editorconfig`.
Preserve third-party notices and source-file MPL headers.

## Licensing contributions

By submitting original contributions, you offer them under MPL-2.0. Only submit
material you are entitled to license. Third-party assets must include their
source and applicable license; their terms are not replaced by the project MPL.
No CLA or copyright assignment is required by this repository.
