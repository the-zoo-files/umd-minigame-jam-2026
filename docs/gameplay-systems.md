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

`MachineFlaskShooter` is attached to the machine base. Its serialized `launchPoint` is the child transform on the upper cylinder.

The machine:

- launches after an initial short delay and then at `launchInterval`;
- caps tracked active flasks at `maximumActiveFlasks`;
- chooses quadrants through a shuffled bag;
- randomizes the landing coordinates inside the selected quadrant;
- calculates a ballistic velocity with a configured apex;
- adds randomized angular velocity.

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

## Stylized Flask Physics

`PickupFlask` applies bounded arcade behavior:

- maximum downward speed: `14`;
- impact spin boost: `2.5`;
- one settling bounce;
- first bounce vertical speed: `1.5`;
- thrown spin speed: randomized magnitude `8`.

Bounce is only applied for sufficiently strong contacts with an upward-facing normal. These safeguards prevent wall hits from creating vertical hops and keep flasks from bouncing indefinitely.

## Safe Extension Points

- Replace `FlaskPlaceholder.prefab` visuals without changing the `PickupFlask` contract.
- Add flask effects inside `PickupFlask` or a new sibling component rather than inside player input code.
- Use `Ballistics.TryCalculateVelocity` when another system needs trajectories.
- Move player slots and colors to a configuration asset if designers need to tune them frequently.
- Replace the active-flask limit with pooling only when profiling or gameplay scale justifies it.
