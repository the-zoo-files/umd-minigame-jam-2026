# Code and Asset Style Guide

## C# Formatting

- Use four spaces; do not use tabs.
- Use Allman braces: opening braces go on their own line.
- Use one primary runtime type per file and match the filename to the type.
- Keep files focused. Prefer another component over turning a controller into a general manager.
- Use explicit access modifiers.
- Use expression-bodied members only for short, obvious properties.
- Use target-typed `new(...)` when the type is clear from the declaration.
- Keep lines readable. Split long method calls and boolean conditions across lines.

Follow this member order unless Unity lifecycle clarity requires a small exception:

1. constants and static readonly data;
2. static events and public static properties;
3. serialized fields;
4. private runtime state;
5. public properties and methods;
6. Unity callbacks in lifecycle order;
7. private event handlers and helpers.

## Naming

- Namespaces and types: `PascalCase`
- Public members and events: `PascalCase`
- Private fields, locals, and parameters: `camelCase`
- Private serialized fields: `camelCase`, without prefixes
- Static readonly collections: `PascalCase`
- UXML element names queried from C#: `camelCase`, such as `player1Name`
- USS classes: lowercase kebab-case, such as `player-name` and `is-connected`

Use domain names rather than implementation names. Prefer `holdPoint`, `launchPoint`, and `throwApexHeight` over generic names such as `target`, `object`, or `value` when the longer name carries meaning.

## Unity Component Conventions

### Serialized configuration

- Use private `[SerializeField]` fields for designer-tunable values and prefab references.
- Provide sensible defaults in code, then intentionally set prefab or scene overrides.
- Group related tuning fields together.
- Treat unit meaning as part of the name: `launchInterval`, `throwDistance`, and `maximumFallSpeed` are preferable to abbreviated names.
- When renaming a serialized field, add `UnityEngine.Serialization.FormerlySerializedAs` unless every serialized instance is migrated in the same change.

### Component requirements and caching

- Add `[RequireComponent]` for components without which the script cannot function.
- Cache required components and input actions in `Awake`.
- Use `TryGetComponent` when absence is expected; use `GetComponent` plus a clear error when absence is a broken prefab contract.
- Avoid repeated component searches in `Update` and `FixedUpdate`.

### Lifecycle

- `Awake`: establish required references and identity that other systems may need immediately.
- `OnEnable`/`OnDisable`: subscribe and unsubscribe events.
- `Update`: input, timers, and non-physics presentation decisions.
- `FixedUpdate`: continuous Rigidbody adjustments.
- `LateUpdate`: visual/socket corrections that must follow player movement.
- `OnDestroy`: remove the instance from static collections and publish teardown events when appropriate.

Input System joins can invoke callbacks during instantiation. Do not assume `Start` has run before another component receives a joined player.

## Control Flow and Error Handling

- Prefer early returns over deep nesting.
- Validate required references at the boundary where they are used.
- Log actionable configuration errors with the relevant Unity context object:

```csharp
Debug.LogError("The flask machine is missing its prefab or launch point.", this);
```

- Disable a component after an unrecoverable configuration error if continuing would spam the Console.
- Do not use exceptions for expected gameplay state.
- Remember Unity's special destroyed-object null behavior when maintaining object lists.

## Physics Style

- Use Unity's 3D physics APIs and `Rigidbody.linearVelocity` for this Unity version.
- Apply continuous corrections in `FixedUpdate`; one-time state transitions may set velocity immediately.
- Calculate ballistic launches from origin, target, gravity, and apex height. Avoid tuning unrelated horizontal and vertical speeds when a predictable landing point is required.
- Keep held physics objects kinematic, non-colliding, and non-interpolated. Restore their free-flight settings atomically when released.
- Keep stylized effects bounded: cap fall speed, bounce count, and active object count.
- Use trigger colliders for reliable pickup proximity and a `CharacterController` for player movement.

## Events and Collections

- Name events in past tense (`PlayerJoined`, `PlayerLeft`).
- Publish read-only collection interfaces when consumers should not mutate the backing list.
- Check for duplicates before adding lifecycle-managed objects to static lists.
- Always unsubscribe instance listeners.
- Use a list when stable ordering or small shuffled bags matter; avoid introducing a more complex collection without a measurable need.

## Performance Expectations

This is a small local prototype, but hot paths should still avoid unnecessary work:

- Do not allocate every frame.
- Avoid LINQ in `Update`, `FixedUpdate`, and collision callbacks.
- Cache queried UI elements and Input Actions.
- Cap spawned physics objects.
- Instantiate materials intentionally; accessing `renderer.material` creates an instance and is appropriate only when each player needs a unique color.

## UI Toolkit Style

- Keep hierarchy and default text in UXML.
- Keep presentation, anchors, colors, and state appearance in USS.
- Keep runtime state transitions in C# by toggling classes.
- Prefer semantic state classes such as `is-connected`; use negation in USS only when it improves readability.
- Put reusable visual values in `:root` custom properties.
- Anchor corner HUD elements with absolute positioning and explicit opposing edges.
- Keep labels visible in UXML for UI Builder editing, then let runtime code establish the initial state.

## Asset Editing

- Do not hand-edit `.unity`, `.prefab`, or `.asset` YAML while Unity is available.
- Use `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset` for prefab changes made by tools.
- Mark modified scene objects dirty and save the scene through `EditorSceneManager`.
- Do not modify package-cache or generated project files.
- Preserve GUID-bearing `.meta` files when moving or renaming assets.

## Comments and Documentation

- Prefer clear names and small methods over comments that restate code.
- Comment non-obvious intent, lifecycle hazards, formulas, or Unity-specific constraints.
- Update `/docs` when changing a contract, not for every local refactor.
- Keep root agent guides concise and link to detailed documents.
