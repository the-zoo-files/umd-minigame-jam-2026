# Architecture and Project Structure

## Technology Baseline

- Unity `6000.6.3f1`
- Universal Render Pipeline `17.6.0`
- Input System `1.20.0`
- UI Toolkit runtime UI
- Three-dimensional PhysX gameplay
- One stationary perspective camera showing the whole arena

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
│   ├── GameManager.cs
│   ├── PlayerFlaskCollector.cs
│   └── Player.prefab
├── Scenes/
│   └── Game.unity
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
- `CouchPlayerController` reads player-scoped actions, moves and rotates the avatar, manages flask contact/pickup/throw, owns the player's score, and publishes roster and score events.
- `GameManager` owns the round timer, player-zone definitions, end-of-round flask penalties, and final pause state.
- `PlayerFlaskCollector` validates thrown flasks against its player number and awards their configured points.

### `UmdJam.UI`

Owns runtime UI Toolkit presentation.

- `CouchPlayerHud` maps player events to named UXML labels and USS state classes.
- `CouchConnectionMenu` presents lobby state in the same `UIDocument`, references `CouchMultiplayerManager`, and never keeps a separate roster or readiness state. Its appearance lives in `CouchConnectionMenu.uss`.

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
- `HoldPoint`: attachment socket above the head
- `ThrowPoint`: release socket at the body's front-center
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

1. `CouchMultiplayerManager.Awake` configures manual `PlayerInputManager` joining. Lobby join actions explicitly assign a device to an empty selected slot; joining is disabled when the selected slots are full or play starts.
2. The Input System instantiates `Player.prefab` for a keyboard/mouse or gamepad device.
3. `CouchPlayerController.Awake` caches required components. `PlayerInput.OnEnable` completes identity assignment, device pairing, and action cloning.
4. `CouchMultiplayerManager.OnPlayerJoined` calls the controller's idempotent `InitializePlayer`, which caches the paired actions, assigns spawn/name/color, and publishes `PlayerJoined`. `Start` provides the same initialization for standalone players. Player numbering is cached for safe teardown notifications.
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
Matching player collector → configured points awarded and flask destroyed
    or
World collision → small settling bounce / rest
```

The collector branch can happen at any point after a player throw. Entering another player's collector does not consume the flask or award points.

Held flasks disable their colliders, gravity, collision detection, and Rigidbody interpolation. They are parented to `HoldPoint` and locked to it in `LateUpdate`. Throwing restores the free-flight settings.

## Communication Patterns

- Use direct serialized references for required same-prefab or same-scene dependencies.
- Use static events for the small, global player roster (`PlayerJoined` and `PlayerLeft`).
- Subscribe in `OnEnable` and unsubscribe in `OnDisable` for listeners.
- Avoid global lookups in normal gameplay loops. Editor smoke tests may use object lookup for inspection.
- Keep authoritative state with its owner: `PickupFlask.IsHeld` and last-thrower identity belong to the flask, while the held reference and score belong to the player controller.
- `GameManager.RoundTimeChanged` drives the HUD timer without creating a gameplay-to-UI dependency.

## Current Architectural Limits

- Spawn positions and player colors are static arrays in `CouchPlayerController`.
- Ballistic launches support finite, downward-only gravity; unsupported trajectories fail before changing flask state.
- The HUD supports exactly four player labels.
- The active flask cap counts spawned instances until they are destroyed; resting flasks remain active.
- `Assets/Editor/GameplaySmokeTests.cs` provides repeatable Play Mode checks through an Editor menu or batch command. It is an Editor-only harness, not a separate test assembly.
