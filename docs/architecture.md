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
│   ├── PickupFlask.cs
│   └── FlaskPlaceholder.prefab
├── Multiplayer/
│   ├── CouchMultiplayerManager.cs
│   ├── CouchPlayerController.cs
│   └── Player.prefab
├── Scenes/
│   └── Game.unity
├── UI/
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

- `MachineFlaskShooter` schedules and launches flask instances.
- `PickupFlask` owns free-flight, collision response, held state, and the transition between physics and attachment modes.

### `UmdJam.Multiplayer`

Owns local player joining and per-player behavior.

- `CouchMultiplayerManager` configures `PlayerInputManager` and places joined players.
- `CouchPlayerController` reads player-scoped actions, moves and rotates the avatar, manages flask contact/pickup/throw, assigns player identity, and publishes join/leave events.

### `UmdJam.UI`

Owns runtime UI Toolkit presentation.

- `CouchPlayerHud` maps player events to named UXML labels and USS state classes.

Dependencies currently flow in one direction:

```text
UI ────────> Multiplayer ────────> Gameplay
                    │
                    └────────────> Input System
```

Gameplay does not depend on UI or multiplayer types. Keep that boundary when adding reusable world objects.

## Scene and Prefab Composition

### Game scene

`Assets/Scenes/Game.unity` contains the stationary arena camera, play surface, player join manager, HUD document, and the flask machine. The machine uses two scene cylinders:

- `Cylinder` owns `MachineFlaskShooter`.
- `Cylinder (1)` contains `FlaskLaunchPoint`, which is assigned as the shooter's launch origin.

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
- `Rigidbody`
- at least one `Collider`
- a visible renderer

`PickupFlask` has `[RequireComponent]` protection for the Rigidbody and Collider, but visual and physics tuning still live on the prefab.

## Runtime Flow

### Player joining

1. `CouchMultiplayerManager.Awake` configures and enables `PlayerInputManager` joining.
2. The Input System instantiates `Player.prefab` for a keyboard/mouse or gamepad device.
3. `CouchPlayerController.Awake` caches required components and actions, assigns the quadrant spawn, name, and color, then raises `PlayerJoined`.
4. `CouchMultiplayerManager.OnPlayerJoined` also calls `PlaceAtSpawn`; this makes placement robust against Input System lifecycle ordering.
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
Small settling bounce / rest
```

Held flasks disable their colliders, gravity, collision detection, and Rigidbody interpolation. They are parented to `HoldPoint` and locked to it in `LateUpdate`. Throwing restores the free-flight settings.

## Communication Patterns

- Use direct serialized references for required same-prefab or same-scene dependencies.
- Use static events for the small, global player roster (`PlayerJoined` and `PlayerLeft`).
- Subscribe in `OnEnable` and unsubscribe in `OnDisable` for listeners.
- Avoid global lookups in normal gameplay loops. Editor smoke tests may use object lookup for inspection.
- Keep authoritative state with its owner: `PickupFlask.IsHeld` belongs to the flask, while the player's current held reference belongs to the controller.

## Current Architectural Limits

- Spawn positions and player colors are static arrays in `CouchPlayerController`.
- Ballistic velocity calculation is duplicated by the player and machine. Extract a shared utility if a third caller appears or trajectory rules diverge.
- The HUD supports exactly four player labels.
- The active flask cap counts spawned instances until they are destroyed; resting flasks remain active.
- No automated test assembly exists yet; validation currently relies on script validation and Play Mode smoke tests.
