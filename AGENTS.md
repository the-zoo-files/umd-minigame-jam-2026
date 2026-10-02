# Agent Guide

This is a Unity 6 couch-multiplayer prototype. Keep this file lean; detailed guidance lives in [`docs/`](docs/README.md).

## Read First

- [Documentation index](docs/README.md)
- [Architecture and project structure](docs/architecture.md)
- [C# and asset style guide](docs/code-style.md)
- [Gameplay systems](docs/gameplay-systems.md)
- [UI Toolkit guide](docs/ui-toolkit.md)
- [Development and validation workflow](docs/development-workflow.md)

## Project Facts

- Unity: `6000.6.3f1`
- Rendering: Universal Render Pipeline
- Input: Unity Input System (`Assets/InputSystem_Actions.inputactions`)
- Runtime UI: UI Toolkit (`UXML`, `USS`, and `UIDocument`)
- Runtime namespaces: `UmdJam.Gameplay`, `UmdJam.Multiplayer`, and `UmdJam.UI`
- Main scene: `Assets/Scenes/Game.unity`

## Non-Negotiable Rules

- Never edit `Library/`, `Temp/`, `Logs/`, or generated IDE files.
- Do not hand-edit Unity YAML (`.unity`, `.prefab`, `.asset`) while the Editor is available. Use Unity APIs and save through the Editor.
- Preserve prefab and scene references when renaming serialized fields. Use `FormerlySerializedAs` when a rename is necessary.
- Use the new Input System; do not add legacy `Input` polling.
- Use UI Toolkit for runtime HUD and menu work unless a task explicitly requires another UI system.
- Put tunable gameplay values in private `[SerializeField]` fields.
- Validate scripts, run the relevant Play Mode smoke test, check the Console, and leave Play Mode before handing off.
- Keep generated assets and their `.meta` files together.

## Change Discipline

- Make the smallest coherent change and preserve unrelated user work.
- Update the relevant guide when changing architecture, controls, prefab contracts, or established conventions.
- Treat prefab child names referenced from code (`HoldPoint`, `ThrowPoint`, `DirectionGizmo`) as contracts.
- Keep `AGENTS.md` and `CLAUDE.md` below 200 lines; place expanded documentation in `docs/`.
