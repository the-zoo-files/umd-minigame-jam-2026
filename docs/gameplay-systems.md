# Gameplay Systems

## Controls and Joining

The game starts at a local connection menu. All players share one screen; online play and split-screen are not implemented. Select **Players** (1–4, default 2), fill the selected slots with connected humans or CPUs, then choose **Start Game**. The round timer, player movement/pickup, and physics wait until the round starts.

- Left/Right arrows or gamepad D-pad: change the selected player count.
- Enter or gamepad south button (A/Cross): join the lowest available selected slot.
- Space or gamepad Menu/Start: start when every selected slot has a connected device. The device issuing this command must already be paired to a player. The mouse can also activate **Start Game**.
- Escape or gamepad east button (B/Circle): leave that device's slot before starting. Each occupied card also has a **Leave** button, including when its controller is disconnected.
- One keyboard/mouse player is supported; every additional player needs a gamepad.
- The count cannot shrink past an occupied slot. Disconnecting a required device blocks starting until it reconnects or the player leaves. Joining and lobby changes lock when the round starts.

Lobby actions are owned by `CouchMultiplayerManager` and use the new Input System. They are separate from paired gameplay actions, allowing unpaired devices to join without driving an existing player. `SelectedPlayerCount` is authoritative in that manager; cards and readiness are derived from the paired `PlayerInput` roster.

The project uses `Assets/InputSystem_Actions.inputactions` and `PlayerInputManager`.

## Random Events

The ready-up menu offers **Random Events: Off / On**, defaulting to Off. The lobby owns this setting, it locks after Start Game, and replay returns it to Off. With it enabled, a scene-owned `RandomEventDirector` waits a randomly chosen 12–20 seconds before the first event and after each 6-second event. This guarantees at least 12 seconds without hazards between events, averaging about 2–3 events per minute. A randomized bag cycles through Earthquake, Tornadoes, and Lightning without an immediate repeat at bag boundaries. Only one event runs at a time; round time controls scheduling, so a paused simulation does not advance hazards.

- **Earthquake:** changing per-player planar drift disturbs movement without changing paired input actions. Free flasks receive bounded horizontal shaking and a small upward force. A ground ripple and additive camera shake animate the effect.
- **Tornadoes:** two moving mini tornadoes apply inward/tangential wind and upward lift. Players retain reduced steering while inside; leaving launches them with circular momentum and gravity brings them back down. Free flasks follow the same circular wind through Rigidbody forces. Animated tapered spiral funnels mark the tornadoes.
- **Lightning:** a ground ring warns at a participant's position for 0.9 seconds, allowing escape before the strike. The bolt and scene/point-light flash stun players inside its 1.8-unit planar radius for 0.9 seconds. Stun blocks movement input, pickup, and throwing; existing held flasks remain attached. A rotating halo marks stunned players.

Humans and CPUs, including God, obey the same world hazards. Held, collected, pooled, and direct-transfer flasks are excluded from environmental forces. Player gravity/collision recovery and stun expiry belong to `CouchPlayerController`; `PickupFlask` validates forces against its own availability state and cached Rigidbody. Tunable event values are private serialized fields on the director, validated at configuration boundaries. Disabling the owner or ending a round clears player effects and restores the camera/light before results capture; ordinary event expiry preserves airborne release momentum and lets outstanding stuns expire normally. Replay/scene teardown removes procedural visuals and subscriptions. No scene/prefab YAML wiring is required: the lobby adds the director only when enabled.

The effects use a shared `Resources/RandomEvents/Effects.mat` material and the project-owned URP vertex-color shader `RandomEvent.shader`. **Tools > UmdJam > Configure Random Event Assets** creates the material through Editor APIs. Visual geometry is bounded and reused (two funnels, one ripple, one warning ring/bolt, and four stun halos); updates do not create new objects or materials.

## CPU Players

Use **Add CPU** on an empty selected lobby slot. Click its difficulty button to cycle **Noob → Pro → Hacker → God**; the default is **Noob**. **Remove** frees a CPU slot. Humans and CPUs share the four-slot limit, and CPU-only matches are supported through the **Start Game** button. Bots do not require or pair devices. Human disconnection still blocks starting until that human reconnects or leaves.

All difficulty levels share humans' maximum movement speed, character turning, touch pickup, carrying limit, throw action, collectors, and score/penalty rules. CPU input ramps and brakes like an analog stick; difficulty changes awareness, decisions, and input timing:

| Difficulty | Planning interval | Reaction delay | Prediction horizon | Decision noise | Strategy weight |
|---|---:|---:|---:|---:|---:|
| Noob | 0.90 s | 0.80 s | 0 s | 0.80 | 0 |
| Pro | 0.45 s | 0.35 s | 0.25 s | 0.40 | 0.2 |
| Hacker | 0.22 s | 0.12 s | 0.8 s | 0.12 | 0.7 |
| God | 0.08 s | 0 s | 2 s | 0 | 1 |

Tune the profiles on the disabled `CpuPlayerController` component in `Assets/Multiplayer/Player.prefab`. The lobby enables it only for CPU participants; human controllers remain human. `CpuSettings` defines the fields, and a missing component falls back to the code defaults. God applies `CpuSettings.WithoutHandicaps` to its effective profile: serialized reaction/noise/awareness/commitment/switching/throw restrictions are ignored, strategy is full strength, and natural movement smoothing/braking are bypassed. Its planning interval and prediction horizon remain tunable; the planning interval bounds path-query work rather than adding a separate reaction wait. For the other levels, reaction delay is the minimum time to notice a newly observed flask, followed by the next planning tick; it does not stop movement toward an existing target.

| Difficulty | Awareness radius | Commitment | Required switch advantage | Input acceleration / s | Throw preparation |
|---|---:|---:|---:|---:|---:|
| Noob | 7 m | 1.4 s | 40% | 4 | 0.35 s |
| Pro | 9 m | 1.0 s | 30% | 5 | 0.25 s |
| Hacker | 12 m | 0.7 s | 20% | 6 | 0.15 s |
| God | Unlimited | 0 s | 0% | Immediate | 0 s |

Noob, Pro, and Hacker brake over the final 0.8 m. God uses immediate movement input without artificial approach braking; the normal step-distance clamp still prevents overshooting waypoints. Decision noise is sampled once per observed flask, so replanning does not reroll its appeal. Commitment protects a valid target; after it expires, another flask must beat the configured score advantage. God has neither commitment nor a switch advantage requirement. Unavailable or out-of-range flasks are forgotten. `PickupFlask.AvailabilityRevision` invalidates observations across pooling and pickup/rethrow even when both happen between planning ticks. Awareness uses planar distance rather than facing/line of sight, matching the shared overhead arena. Bots use independent random streams seeded from the match's Unity random state, allowing varied play and seeded checks.

Bots navigate on a shared NavMesh prebaked from colliders, excluding player/flask bodies and triggers. `CpuNavigationBaker` refreshes the bake before builds and validates all spawn-to-collector routes. Missing/incompatible data falls back to the same runtime bake configuration; unsaved authored geometry should be saved before rebaking. They evaluate noticed flasks using reachable route length, point value, predicted motion, return route, nearby rival arrival estimates, and round-end penalties. Identical static return-distance queries share a bounded 64-entry cache; changed endpoints miss the cache, and navigation replacement clears it. Carrying bots travel to their collector's direct-throw region, prepare briefly, and use the normal throw action. All levels stage toward the middle when idle; Hacker/God attempt to leave their penalty zone if there is insufficient time to deliver. Bots replan when targets become unavailable, steer around players without teleporting, and stop immediately at round end.

The navigation contract is a static arena with the player prefab's capsule dimensions. Moving arena geometry requires rebuilding the map. Motion prediction estimates the first floor impact; it does not perfectly simulate future collisions. God is the strongest configured heuristic, not a proven optimal or unbeatable policy. Measure difficulty balance with human playtests; deterministic scoring tests establish correctness, not competitive strength.

After removing God's artificial handicaps, the 2026-10-08 seeded benchmark (eight 20-second rounds, rotating every difficulty through all four spawn slots) recorded mean final scores of Noob 1.5, Pro 4.75, Hacker 6.25, and God 13. God delivered 13.875 points per round on average. CPU behavior and full CPU regression suites passed. These short CPU-only matches establish the observed ordering for that sample, not human win rates or a guarantee of unbeatable play.

- Up to four local players may join.
- Keyboard/mouse and gamepad devices are supported.
- Player movement reads `Player/Move`.
- Flask throw reads `Player/Attack`.
- Mouse throw: left mouse button.
- Gamepad throw: south face button (A on Xbox-style controllers, Cross on PlayStation-style controllers).

Do not read devices globally inside a player controller. Always use the actions cloned and paired by that player's `PlayerInput`.

## Player Colors

Every human and CPU has a unique color. Use the arrows beside the color name on an occupied ready-up card to cycle through available colors. Paired humans can also use Q/E on keyboard or the controller shoulder buttons. Colors taken by other participants are skipped. Disconnected players and players pending removal retain their colors until teardown; leaving releases the color. Selection locks when the round starts.

The palette contains the 18 selectable Among Us colors: Red, Blue, Green, Pink, Orange, Yellow, Black, White, Purple, Brown, Cyan, Lime, Maroon, Rose, Banana, Gray, Tan, and Coral. Names and RGB values live only in `PlayerColorPalette`; Fortegreen is a fallback rather than a selectable color and is excluded. Palette reference: [Among Us colors](https://among-us.fandom.com/wiki/Colors).

Preferred slot defaults are Cyan, Red, Yellow, and Lime. Joining chooses the next unused color if a preferred color is occupied. The selected color updates the direction marker, lobby border/swatch, HUD border, and the matching collector's material when its shader supports a color property. The hidden placeholder renderer retains its per-player material as a compatibility contract. Color changes reuse existing owned materials; collector material instances are destroyed on teardown. Color choice never changes scoring ownership or player-zone assignments.

## Character Skins and Animation

Each occupied ready-up card exposes a character selector. Paired humans can also cycle with Z/X on keyboard or D-pad down/up; selection locks when the round starts. `CharacterSkinCatalog.asset` is the authoritative list. Criminal is the default entry and replaces the hidden capsule renderer, while the capsule mesh remains on the prefab as a non-rendering material/color contract for existing systems and validation.

All catalog entries use `Assets/Meshes/Characters/Animations/AllCharacters.controller`. It drives idle, running, running while carrying, and throw states from the controller's `Speed`, `Carrying`, and `Throw` parameters. The clips import as Humanoid, copy the valid avatar from `characterMedium.fbx`, and bake root rotation and translation into their poses. Running clips loop in place; root motion stays disabled because `CharacterController` owns movement. New skins must provide a valid Humanoid avatar in their catalog entry.

The Player prefab owns `CharacterRoot/HoldPoint/ThrowPoint`. When a skin is instantiated, `HoldPoint` is reparented to its Humanoid right-hand bone while preserving the prefab-authored local offset, so designers can tune flask placement without code changes. Pickup preserves the flask's world scale instead of inheriting the imported skeleton's bone scale. The Throw state plays at 1.5x speed. Throw input starts the animation while the flask remains attached; the release event remains about 43% through the clip (0.60 seconds of clip time, about 0.40 seconds of playback), with the controller's serialized normalized release point acting as a fallback if an imported event is skipped.

## Player Slots

Players spawn clockwise around the arena:

| Player | World position | HUD corner | Color role |
|---|---:|---|---|
| 1 | `(-6, 1, 6)` | top-left | cyan |
| 2 | `(6, 1, 6)` | top-right | red |
| 3 | `(6, 1, -6)` | bottom-right | yellow |
| 4 | `(-6, 1, -6)` | bottom-left | lime |

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

At timeout the manager closes the round and sets `Time.timeScale` to zero, captures `Results`, applies penalties exactly once, and publishes score/time/end events. Player input is ignored once the round is over, and collectors reject late collection/transfer requests. Destroying the manager restores the time scale for scene changes and Play Mode shutdown.

The snapshot includes held and direct-transfer flasks, but excludes collected or disabled instances. Assignment uses the existing three-dimensional zone bounds: flasks above the zone's maximum Y or outside all zones are excluded. Shared boundaries belong to the first matching zone. Only actual participants receive results; negative scores are valid. The highest final score wins, equal highest scores tie, and a one-player round displays **Round complete**.

The results director eases the camera overhead using unscaled time, then counts each participant's flasks clockwise by slot. Within a zone, entries appear in descending world Z then ascending X order. Each marker shows its point value; the zone displays its combined deduction and starting-to-final score. Authoritative scores are already final; the reveal is presentation only. The arena's baked lighting remains unchanged; translucent UI masks darken areas outside the active zone. The director temporarily hides its serialized ceiling-fixture renderers to keep the overhead arena unobstructed and restores their previous render flags on release. Flask attachment/transfer updates stop immediately at time scale zero, including later callbacks in the timeout frame; late throw animation events also reject round-over releases.

**Skip count** immediately shows final standings. UI Toolkit supports pointer and keyboard/gamepad submit; replay is enabled only after submit release and a short debounce, so holding submit cannot skip and replay together. **Play Again** reloads the scene and returns to fresh player setup, without retaining the roster or selections. Director timing and camera framing are private serialized fields on the scene's `RoundResults` object. The view's mask opacity lives beside the HUD document. Audio is optional and requires both the director's tick source and clip.

## Safe Extension Points

- Replace `FlaskPlaceholder.prefab` visuals or point/physics tuning through a `Flask` definition without changing the pickup contract.
- Add flask effects inside `PickupFlask` or a new sibling component rather than inside player input code.
- Use `Ballistics.TryCalculateVelocity` when another system needs trajectories.
- Move player slots and colors to a configuration asset if designers need to tune them frequently.
- Machine flasks are pooled by definition on collection. Reuse resets ownership, bounce count, transform/scale, colliders, Rigidbody state, and direct-transfer state. The active cap excludes pooled instances; each definition retains at most that cap. New instances are created on demand, so first-use creation still has a cost.
- Direct transfers animate in the flask's `Update` without a coroutine or per-throw callback allocation. Disabling a flask, losing/disabling its collector, or completing without collection clears the pending callback and restores free physics, preventing an unavailable flask from occupying the active cap indefinitely. Collection clears transfer state before pooling, so a previous callback cannot affect a reused instance.
