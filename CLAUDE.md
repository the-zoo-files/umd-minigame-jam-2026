# Claude Project Guide

Use [`AGENTS.md`](AGENTS.md) as the concise source of repository-wide instructions. Detailed documentation is intentionally kept under [`docs/`](docs/README.md).

## Required Reading

- [Architecture](docs/architecture.md)
- [Code style](docs/code-style.md)
- [Gameplay systems](docs/gameplay-systems.md)
- [UI Toolkit](docs/ui-toolkit.md)
- [Development workflow](docs/development-workflow.md)

## Working Contract

- This is a Unity `6000.6.3f1` project using URP, the new Input System, and UI Toolkit.
- Modify C# directly, but make scene, prefab, and serialized asset changes through Unity Editor APIs when the Editor is connected.
- Do not modify generated directories such as `Library/`, `Temp/`, or `Logs/`.
- Preserve serialized references and prefab child-name contracts.
- Favor private serialized configuration, cached components, early returns, and focused `MonoBehaviour` responsibilities.
- Test gameplay changes in Play Mode, inspect the Console, and exit Play Mode when finished.
- Update `/docs` when a change makes the existing documentation inaccurate.

When guidance conflicts, follow the user request first, then `AGENTS.md`, then the linked detailed guide.
