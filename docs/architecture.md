# Architecture and Project Structure

## Technology Baseline

- Unity `6000.6.3f1`
- Universal Render Pipeline `17.6.0`
- Input System `1.20.0`
- UI Toolkit runtime UI
- Three-dimensional PhysX gameplay
- One perspective camera showing the whole arena; stationary during play, animated overhead for results

The prototype is local couch multiplayer for up to four players. There is no networking layer and no split-screen camera.

## Project-Owned Runtime Files

```text
Assets/
├── Gameplay/
│   ├── MachineFlaskShooter.cs
│   ├── Flask.cs
│   ├── PickupFlask.cs
│   ├── DefaultFlask.asset
│   ├── Ballistics.cs
│   └── FlaskPlaceholder.prefab
├── Multiplayer/
│   ├── CouchMultiplayerManager.cs
│   ├── CouchPlayerController.cs
│   ├── CharacterSkinCatalog.cs
│   ├── CpuPlayerController.cs
│   ├── CpuNavigation.cs
│   ├── CpuNavigationBake.cs
│   ├── CpuDifficulty.cs
│   ├── CpuSettings.cs
│   ├── PlayerColorPalette.cs
│   ├── GameManager.cs
│   ├── PlayerFlaskCollector.cs
│   └── Player.prefab
├── Scenes/
│   └── Game.unity
├── Resources/
│   └── CpuNavigation/Game.asset
├── UI/
│   ├── CouchConnectionMenu.cs
│   ├── CouchConnectionMenu.uss
│   ├── CouchPlayerHud.cs
│   ├── CouchPlayerHud.uxml
│   ├── CouchPlayerHud.uss
│   └── Fonts/
└── InputSystem_Actions.inputactions
```

Unity-generated `.meta` files are required companions to their assets and must remain alongside them.

## Namespace Boundaries

### `UmdJam.Gameplay`

Owns world interactions and physics objects.

- `Flask` is the designer-facing definition for a flask prefab, point value, and physics tuning.
- `MachineFlaskShooter` schedules and launches flask instances from configured `Flask` definitions.
- `PickupFlask` owns free-flight, collision response, held state, and the transition between physics and attachment modes.
- `Ballistics` calculates and validates launch velocities for both machine and player throws.

### `UmdJam.Multiplayer`

Owns local player joining and per-player behavior.

- `CouchMultiplayerManager` configures `PlayerInputManager` and places joined players.
- It also owns the selected lobby count, explicit join/leave/start inputs, and readiness derived from paired devices. It references the scene's `GameManager` directly.
- `CouchPlayerController` reads player-scoped actions, moves and rotates the avatar, instantiates the selected catalog skin, drives the shared animator, manages flask contact/pickup/throw, owns the player's score, and publishes roster and score events.
- Its `ActivePlayers` roster includes both humans and CPUs and is the authoritative participant list. `PlayerInput` is only the human device adapter; lobby occupancy/readiness comes from participants, not the Input System's human-only list.
- `CpuPlayerController` supplies movement and attack decisions to the same controller used by humans. Difficulty profiles live in its serialized `CpuSettings` fields. `CpuNavigation` registers compatible prebaked navigation before CPU rounds, with runtime baking as a fallback. `CpuNavigationBaker` regenerates and validates the static arena bake before builds using the same capsule configuration and geometry exclusions. Registrations and owned fallback data are released on teardown; shared baked assets are preserved. Bots still move with `CharacterController`, not a second movement system.
- `GameManager` owns the round timer, player-zone definitions, end-of-round flask penalties, and final pause state.
- `RoundResults`, `PlayerRoundResult`, and `FlaskPenaltyEntry` hold immutable timeout snapshots: participant identity/color, starting/final scores, and spatially ordered flask point values/positions. UI never applies penalties.
- `PlayerFlaskCollector` validates thrown flasks against its player number and awards their configured points.

### `UmdJam.UI`

Owns runtime UI Toolkit presentation.

- `CouchPlayerHud` maps player events to named UXML labels and USS state classes.
- `CouchConnectionMenu` presents lobby state in the same `UIDocument`, references `CouchMultiplayerManager`, and never keeps a separate roster or readiness state. Its appearance lives in `CouchConnectionMenu.uss`.
- `RoundResultsDirector` owns the unscaled camera/count/deduction/winner sequence and scene replay. `RoundResultsView` projects snapshot positions into UI Toolkit, draws zone masks and markers, and presents standings. Both use the existing HUD document. `RoundResults/ResultsCameraTarget` is authored by `RoundResultsSetup` through Editor APIs.

Dependencies currently flow in one direction:

```text
UI ────────> Multiplayer ────────> Gameplay
                    │
                    └────────────> Input System
```

Gameplay does not depend on UI or multiplayer types. Keep that boundary when adding reusable world objects.

## Scene and Prefab Composition

### Game scene

`Assets/Scenes/Game.unity` contains the stationary arena camera, play surface, player join manager, HUD document, flask spawner, and four collectors.

- `FlaskSpawner` owns `MachineFlaskShooter` and the grouped machine visuals.
- `FlaskSpawner/FlaskSpawnPoint` is assigned as the shooter's launch origin.
- `Collectors` contains `Collector_P1` through `Collector_P4`; each has a trigger collider and matching `PlayerFlaskCollector.playerNumber`.
- `GameManager/PlayerZones` contains `Player1Zone` through `Player4Zone`; the non-rendering box colliders cover the four arena quadrants in clockwise player order.

### Player prefab

`Assets/Multiplayer/Player.prefab` requires:

- `CouchPlayerController`
- `CharacterController`
- `PlayerInput`
- a body-sized trigger collider for touch pickup
- renderer(s) for player color
- an empty `CharacterRoot` transform and a `CharacterSkinCatalog` reference
- `HoldPoint`: authorable hand-local attachment offset; runtime character setup reparents it to the active Humanoid right hand
- `ThrowPoint`: release socket parented beneath `HoldPoint`
- `DirectionGizmo`: forward-facing ground marker

The three child names are runtime contracts because the controller finds the direction marker by name and serialized references point to the two sockets.

### Flask prefab

`Assets/Gameplay/FlaskPlaceholder.prefab` requires:

- `PickupFlask`
- a `Flask` definition
- `Rigidbody`
- at least one `Collider`
- a visible renderer

`PickupFlask` has `[RequireComponent]` protection for the Rigidbody and Collider, but visual and physics tuning still live on the prefab.

## Runtime Flow

### Player joining

CPU slots instantiate the existing player prefab under an inactive staging parent. The manager disables `PlayerInput`, initializes identity and the CPU driver, and only then activates the avatar. This avoids device pairing or temporary human roster entries. CPU difficulty and removal are lobby-only operations; pending destruction blocks starting. `GetParticipant` covers both participant types; `GetPlayer` returns a human's `PlayerInput` or null for a CPU/empty slot.

1. `CouchMultiplayerManager.Awake` configures manual `PlayerInputManager` joining. Lobby join actions explicitly assign a device to an empty selected slot; joining is disabled when the selected slots are full or play starts.
2. The Input System instantiates `Player.prefab` for a keyboard/mouse or gamepad device.
3. `CouchPlayerController.Awake` caches required components. `PlayerInput.OnEnable` completes identity assignment, device pairing, and action cloning.
4. `CouchMultiplayerManager.OnPlayerJoined` calls the controller's idempotent `InitializePlayer`, which caches the paired actions, assigns spawn/name/color, instantiates the default selected character, and publishes `PlayerJoined`. `Start` provides the same initialization for standalone players. Player numbering is cached for safe teardown notifications.
5. `CouchPlayerHud` receives the event and applies `is-connected` to the player's label.

Player slots proceed clockwise:

1. top-left
2. top-right
3. bottom-right
4. bottom-left

### Flask lifecycle

```text
Machine launch
    ↓
Dynamic Rigidbody flight
    ↓
Touch player trigger
    ↓
Kinematic attachment at HoldPoint
    ↓ attack action
Snap to ThrowPoint
    ↓
Dynamic ballistic throw
    ↓
Matching player collector → configured points awarded and flask returned to its machine pool
    or
World collision → small settling bounce / rest
```

The collector branch can happen at any point after a player throw. Entering another player's collector does not consume the flask or award points.

Held flasks disable their colliders, gravity, collision detection, and Rigidbody interpolation. They are parented to `HoldPoint` and locked to it in `LateUpdate`. Throwing restores the free-flight settings.

## Communication Patterns

- Use direct serialized references for required same-prefab or same-scene dependencies.
- Use static events for the small, global player roster (`PlayerJoined` and `PlayerLeft`).
- `PickupFlask.ActiveFlasks` tracks enabled flasks across pooling cycles. CPU targeting checks `IsAvailable`, excluding held, collected, inactive, and direct-transfer flasks. Gameplay owns this registry and has no dependency on CPU code.
- Subscribe in `OnEnable` and unsubscribe in `OnDisable` for listeners.
- Direct-transfer animation state belongs to `PickupFlask`, including receiver validity, completion, cancellation, and pooling reset. Collectors cache their completion delegate once; throws allocate neither a captured callback nor a coroutine.
- Avoid global lookups in normal gameplay loops. Editor smoke tests may use object lookup for inspection.
- Keep authoritative state with its owner: `PickupFlask.IsHeld` and last-thrower identity belong to the flask, while the held reference and score belong to the player controller.
- `GameManager.RoundTimeChanged` drives the HUD timer without creating a gameplay-to-UI dependency.

## Current Architectural Limits

- Spawn positions remain a static array in `CouchPlayerController`. `PlayerColorPalette` owns the 18 selectable color names/RGB values and preferred slot defaults. Each controller owns its selected `ColorIndex`; uniqueness is derived from the shared participant roster, with no separate reservation table.
- `CharacterSkinCatalog.asset` owns selectable character prefab/material/placement entries and the shared `AllCharacters.controller`. The Player prefab keeps only an empty `CharacterRoot`; character geometry is instantiated from the catalog so adding a skin does not change the prefab contract.
- Ballistic launches support finite, downward-only gravity; unsupported trajectories fail before changing flask state.
- The HUD supports exactly four player labels.
- The active flask cap counts live spawned instances; resting flasks remain active. Collected machine flasks are deactivated and reused by definition, with at most the active cap retained per definition. Inactive instances are children of their machine and are cleaned up with it. Standalone flasks are still destroyed on collection.
- `Assets/Editor/GameplaySmokeTests.cs` provides repeatable Play Mode checks through an Editor menu or batch command. It is an Editor-only harness, not a separate test assembly.
