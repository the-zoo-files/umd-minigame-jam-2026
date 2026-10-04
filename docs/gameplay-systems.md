# Gameplay Systems

## Controls and Joining

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

`CouchPlayerController.Awake` applies player identity and initial placement. `CouchMultiplayerManager` repeats placement from the join callback to protect against Input System callback ordering.

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

Both machine and player throws derive a launch velocity from an origin, target, gravity, and desired apex height.

Given gravity magnitude `g` and apex rise `h`:

```text
verticalSpeed = sqrt(2 * g * h)
riseTime      = verticalSpeed / g
fallTime      = sqrt(2 * (apexY - targetY) / g)
flightTime    = riseTime + fallTime
horizontal    = horizontalDisplacement / flightTime
velocity      = horizontal + up * verticalSpeed
```

This produces a predictable parabola and landing point under the current global gravity. If project gravity changes, trajectories automatically adapt.

## Pickup and Attachment

Players pick up a flask by touching it. The player prefab has a trigger collider matching its body, and the controller accepts contacts through both trigger and `CharacterController` collision callbacks.

Pickup succeeds only when:

- the player is not already holding a flask;
- the candidate has `PickupFlask` in its parent chain;
- the flask is not already held;
- the player's `HoldPoint` reference exists.

While held, a flask:

- is parented to `HoldPoint` above the player's head;
- has gravity, collision detection, colliders, and Rigidbody interpolation disabled;
- becomes kinematic;
- is snapped to the socket in `LateUpdate` to prevent Rigidbody drift.

Do not replace this with a physics joint unless swaying or breakable attachment is an intentional design feature. The current carry state should be rigid and deterministic.

## Player Throws

Pressing the attack action while carrying a flask:

1. moves it to `ThrowPoint` at the front-center of the player;
2. restores dynamic Rigidbody behavior;
3. calculates a forward ballistic target;
4. applies the launch velocity and randomized spin.

Current defaults:

- distance: `8` world units;
- apex rise: `3` world units;
- target landing height: `0.35`.

The target follows `transform.forward`, so the direction marker previews the horizontal throw direction.

Each throw records the throwing player's number on the flask. A flask can only score in the matching `Collector_P1` through `Collector_P4` trigger under `Collectors`. A successful collection awards the definition's configured points, raises `CouchPlayerController.ScoreChanged`, and destroys the flask so it cannot score twice. A flask entering another player's collector remains in play.

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

`GameManager` starts a round at the serialized `roundDuration`, which defaults to 60 seconds. It publishes the remaining time for the HUD and ends the round when the timer reaches zero.

The four `GameManager/PlayerZones` box colliders cover the arena quadrants in the same clockwise order as player slots. At round end, each active `PickupFlask` is assigned to at most one zone based on its world position. Its configured `Flask.Points` value is subtracted from that player's score. Scores are not clamped, so zone penalties can make a score negative.

After penalties and score events are applied, the manager sets `Time.timeScale` to zero. Player input is ignored once the round is over. Destroying the manager restores the time scale for scene changes and Play Mode shutdown.

## Safe Extension Points

- Replace `FlaskPlaceholder.prefab` visuals or point/physics tuning through a `Flask` definition without changing the pickup contract.
- Add flask effects inside `PickupFlask` or a new sibling component rather than inside player input code.
- Extract a shared ballistic utility when another system needs trajectories.
- Move player slots and colors to a configuration asset if designers need to tune them frequently.
- Replace the active-flask limit with pooling only when profiling or gameplay scale justifies it.
