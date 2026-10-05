# Gameplay Systems

## Controls and Joining

The game starts at a local connection menu. All players share one screen; online play and split-screen are not implemented. Select **Players** (1–4, default 2), connect the selected devices, then choose **Start Game**. The round timer, player movement/pickup, and physics wait until the round starts.

- Left/Right arrows or gamepad D-pad: change the selected player count.
- Enter or gamepad south button (A/Cross): join the lowest available selected slot.
- Space or gamepad Menu/Start: start when every selected slot has a connected device. The device issuing this command must already be paired to a player. The mouse can also activate **Start Game**.
- Escape or gamepad east button (B/Circle): leave that device's slot before starting. Each occupied card also has a **Leave** button, including when its controller is disconnected.
- One keyboard/mouse player is supported; every additional player needs a gamepad.
- The count cannot shrink past an occupied slot. Disconnecting a required device blocks starting until it reconnects or the player leaves. Joining and lobby changes lock when the round starts.

Lobby actions are owned by `CouchMultiplayerManager` and use the new Input System. They are separate from paired gameplay actions, allowing unpaired devices to join without driving an existing player. `SelectedPlayerCount` is authoritative in that manager; cards and readiness are derived from the paired `PlayerInput` roster.

The project uses `Assets/InputSystem_Actions.inputactions` and `PlayerInputManager`.

- Up to four local players may join.
- Keyboard/mouse and gamepad devices are supported.
- Player movement reads `Player/Move`.
- Flask throw reads `Player/Attack`.
- Mouse throw: left mouse button.
- Gamepad throw: south face button (A on Xbox-style controllers, Cross on PlayStation-style controllers).

Do not read devices globally inside a player controller. Always use the actions cloned and paired by that player's `PlayerInput`.

## Player Slots

Players spawn clockwise around the arena:

| Player | World position | HUD corner | Color role |
|---|---:|---|---|
| 1 | `(-6, 1, 6)` | top-left | cyan |
| 2 | `(6, 1, 6)` | top-right | red |
| 3 | `(6, 1, -6)` | bottom-right | yellow |
| 4 | `(-6, 1, -6)` | bottom-left | green |

`CouchPlayerController.Awake` caches components. The manager's join callback invokes `InitializePlayer` after Input System pairing and action cloning; `Start` is the fallback for standalone players. Initialization runs once, applying identity, placement, colors, paired actions, and the roster event. Missing required actions or an invalid player index disables the controller with one diagnostic. A missing hold point disables only pickup with one diagnostic. Each player owns and destroys its two instantiated color materials.

## Player Movement

Movement is intentionally immediate and arcade-like:

- `CharacterController.SimpleMove` applies planar movement.
- `moveSpeed` defaults to `9.5`.
- Facing follows the current movement vector.
- `turnSpeed` defaults to `30`, producing a fast `Quaternion.Slerp` response.
- The direction marker is a colored mesh under the player's feet and shares the player color.

Keep movement camera-independent unless the control design changes explicitly. Current input maps X/Z directly into world space.

## Machine Launches

`MachineFlaskShooter` is attached to the `FlaskSpawner` root. Its serialized `spawnPoint` is `FlaskSpawner/FlaskSpawnPoint`.

The machine:

- launches after an initial short delay and then at `launchInterval`;
- caps tracked active flasks at `maximumActiveFlasks`;
- chooses quadrants through a shuffled bag;
- randomizes the landing coordinates inside the selected quadrant;
- calculates a ballistic velocity with a configured apex;
- adds randomized angular velocity.

The shooter's `flasks` array contains `Flask` definitions. A definition selects the underlying `PickupFlask` prefab and provides its point value, Rigidbody properties, fall-speed cap, bounce behavior, and spin tuning. Add another definition to introduce a flask variant without duplicating spawner logic.

The shuffled bag contains each quadrant once. It is shuffled and consumed before being refilled, so every four machine launches serve each quadrant exactly once in random order.

Landing coordinates use both X and Z magnitudes in `quadrantLandingRange` (currently `4.5–8`). This keeps targets away from the center machine. The launch apex is currently `4.5` units above the launch point.

## Ballistic Formula

Both machine and player throws use `Ballistics.TryCalculateVelocity` as the single implementation of trajectory calculation and validation.

Given gravity magnitude `g` and apex rise `h`:

```text
verticalSpeed = sqrt(2 * g * h)
riseTime      = verticalSpeed / g
fallTime      = sqrt(2 * (apexY - targetY) / g)
flightTime    = riseTime + fallTime
horizontal    = horizontalDisplacement / flightTime
velocity      = horizontal + up * verticalSpeed
```

This produces a predictable parabola under the current global gravity. Downward gravity magnitude changes are supported. Non-finite inputs/results, negative apex rise, targets above the apex, and zero/upward/lateral gravity are rejected. A rejected player throw retains its held flask; a rejected machine launch creates no flask and disables the shooter with one diagnostic.

## Pickup and Attachment

Players pick up a flask by touching it. The player prefab has a trigger collider matching its body, and the controller accepts contacts through both trigger and `CharacterController` collision callbacks.

Pickup succeeds only when:

- the player is not already holding a flask;
- the candidate has `PickupFlask` in its parent chain;
- the flask is not already held;
- the player's `HoldPoint` reference exists.

`PickupFlask.TryPickUp` owns the transition guard and returns success; it rejects an already-held or disabled flask, a null socket, and a socket inside the flask itself. The controller assigns its held reference only on success. Contact callbacks skip component searches when pickup is unavailable or the player already carries a flask. `TryThrow` rejects free/disabled flasks and non-finite velocity without changing state.

While held, a flask:

- is parented to `HoldPoint` above the player's head;
- has gravity, collision detection, colliders, and Rigidbody interpolation disabled;
- becomes kinematic;
- is snapped to the socket in `LateUpdate` to prevent Rigidbody drift.

Do not replace this with a physics joint unless swaying or breakable attachment is an intentional design feature. The current carry state should be rigid and deterministic.

## Player Throws

Pressing the attack action while carrying a flask:

1. calculates and validates the forward ballistic target from `ThrowPoint`;
2. transitions the held flask to dynamic Rigidbody behavior, applying velocity and spin;
3. places it at `ThrowPoint` before the next physics step;
4. clears the controller's held reference only after a successful release.

Current defaults:

- distance: `8` world units;
- apex rise: `3` world units;
- target landing height: `0.35`.

The target follows `transform.forward`, so the direction marker previews the horizontal throw direction.

Each throw records the throwing player's number on the flask. A flask can only score in the matching `Collector_P1` through `Collector_P4` trigger under `Collectors`. A successful collection awards the definition's configured points, raises `CouchPlayerController.ScoreChanged`, and returns machine-spawned flasks to their pool (or destroys standalone flasks). The collection guard prevents duplicate scoring. A flask entering another player's collector remains in play.

Before calculating a ballistic throw, the controller checks whether the player's position is inside their collector's world-space collider bounds expanded by `directThrowPadding`. When in range, the flask skips dynamic physics and follows a short kinematic transfer to the collector center, then scores normally. `directThrowDuration` controls this transfer time. Throws outside the expanded bounds continue to use the standard ballistic path.

## Stylized Flask Physics

`PickupFlask` applies bounded arcade behavior:

- maximum downward speed: `14`;
- impact spin boost: `2.5`;
- one settling bounce;
- first bounce vertical speed: `1.5`;
- thrown spin speed: randomized magnitude `8`.

Bounce is only applied for sufficiently strong contacts with an upward-facing normal. These safeguards prevent wall hits from creating vertical hops and keep flasks from bouncing indefinitely.

## Round Lifecycle and Zone Penalties

`GameManager` initializes the serialized `roundDuration` (default 60 seconds) and waits at time scale zero. The connection menu starts the round only when the selected roster is connected. `HasStarted` and `IsRoundOver` define `IsPlaying`; it publishes remaining time for the HUD and ends the round when the timer reaches zero.

The four `GameManager/PlayerZones` box colliders cover the arena quadrants in the same clockwise order as player slots. At round end, each active `PickupFlask` is assigned to at most one zone based on its world position. Its configured `Flask.Points` value is subtracted from that player's score. Scores are not clamped, so zone penalties can make a score negative.

After penalties and score events are applied, the manager sets `Time.timeScale` to zero. Player input is ignored once the round is over. Destroying the manager restores the time scale for scene changes and Play Mode shutdown.

## Safe Extension Points

- Replace `FlaskPlaceholder.prefab` visuals or point/physics tuning through a `Flask` definition without changing the pickup contract.
- Add flask effects inside `PickupFlask` or a new sibling component rather than inside player input code.
- Use `Ballistics.TryCalculateVelocity` when another system needs trajectories.
- Move player slots and colors to a configuration asset if designers need to tune them frequently.
- Machine flasks are pooled by definition on collection. Reuse resets ownership, bounce count, transform/scale, colliders, Rigidbody state, and any direct-transfer coroutine. The active cap excludes pooled instances; each definition retains at most that cap. New instances are created on demand, so first-use creation still has a cost.
