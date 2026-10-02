# Project Documentation

This directory contains the durable technical documentation for the project. Root-level agent files are only entry points.

## Guides

- [Architecture and project structure](architecture.md) — directories, runtime ownership, dependencies, and event flow.
- [Code and asset style](code-style.md) — C#, Unity, physics, UXML, USS, and serialized-asset conventions.
- [Gameplay systems](gameplay-systems.md) — joining, players, flasks, machine launches, pickup, and throwing.
- [UI Toolkit](ui-toolkit.md) — HUD structure, selectors, state classes, anchoring, and font usage.
- [Development workflow](development-workflow.md) — safe editing, validation, and smoke-test expectations.

## Documentation Policy

Update these guides when a change affects:

- ownership or communication between systems;
- controls or input action names;
- required prefab components or named child transforms;
- serialized tuning semantics;
- UI element names or USS state classes;
- the expected validation workflow.

Document intent and contracts rather than narrating every line of code. Source code remains the authority for exact numeric defaults.
