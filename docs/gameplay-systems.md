# Gameplay Systems

## Controls and Joining

The game starts at a four-card local lobby. All players share one screen; online play and split-screen are not implemented. The lobby opens with four empty slots and no player-count selector. Join any human players, confirm each human with **Ready!**, then choose **OK!**. Remaining empty slots automatically receive Noob CPUs; existing participants retain their settings. The round timer, player movement/pickup, and physics wait until the round starts. The scene defaults to four slots, while the manager's existing 1–4 player-count API remains available for alternate flows and tests.

- Enter or gamepad south button: join the lowest available slot, then confirm that player's ready state. The card prompt uses the matching keyboard, Nintendo, or Xbox-style input font; unknown controllers use the Xbox-style glyphs.
- Space or gamepad Menu/Start: start when every joined human is explicitly ready and every existing CPU is initialized, filling empty slots with CPUs. The device issuing this command must already be paired to a player. The mouse can also activate **OK!**, including from an empty lobby for a CPU-only match.
- Escape or gamepad east button: cancel ready first, then leave that device's slot on a second press. The ready card's **Cancel** action follows the same rule. A CPU's **Cancel** action removes it.
- One keyboard/mouse player is supported; every additional player needs a gamepad.
- Disconnecting a required device blocks starting until it reconnects or the player leaves. Joining and lobby changes lock when the round starts.

Lobby actions are owned by `CouchMultiplayerManager` and use the new Input System. They are separate from paired gameplay actions, allowing unpaired devices to join without driving an existing player. `SelectedPlayerCount` is authoritative in that manager; device connectivity and explicit player confirmation are tracked separately, and reconnecting requires a fresh ready confirmation.

The project uses `Assets/Input/InputSystem_Actions.inputactions` and `PlayerInputManager`.

## Random Events

The ready-up menu offers **Random Events: Off / On**, defaulting to Off, plus separate **Earthquake**, **Tornadoes**, and **Lightning** switches. All three individual choices default to On; turning the master switch off preserves them and disables their controls. Turn on the master switch to choose any combination, including none for a hazard-free round. The lobby owns these settings and locks them after Start Game; replay resets the master to Off and individual choices to On. With at least one event enabled, a scene-owned `RandomEventDirector` waits a randomly chosen 12–20 seconds before the first event and after each 6-second event. This guarantees at least 12 seconds without hazards between events, averaging about 2–3 events per minute. A randomized bag visits only selected events, avoiding immediate repeats when more than one is selected; a single selected event repeats after each quiet interval. Disabled events also reject direct activation. Only one event runs at a time; round time controls scheduling, so a paused simulation does not advance hazards.

- **Earthquake:** changing per-player planar drift disturbs movement without changing paired input actions. Free flasks receive bounded horizontal shaking and a small upward force. A ground ripple and additive camera shake animate the effect.
- **Tornadoes:** two moving mini tornadoes apply inward/tangential wind and upward lift. Players retain reduced steering while inside; leaving launches them with circular momentum and gravity brings them back down. Free flasks follow the same circular wind through Rigidbody forces. Animated tapered spiral funnels mark the tornadoes.
- **Lightning:** a ground ring warns at a participant's position for 0.9 seconds, allowing escape before the strike. The bolt and scene/point-light flash stun players inside its 1.8-unit planar radius for 0.9 seconds. Stun blocks movement input, pickup, and throwing; existing held flasks remain attached. A rotating halo marks stunned players.

Humans and CPUs, including God, obey the same world hazards. Held, collected, pooled, and direct-transfer flasks are excluded from environmental forces. Player gravity/collision recovery and stun expiry belong to `CouchPlayerController`; `PickupFlask` validates forces against its own availability state and cached Rigidbody. Tunable event values are private serialized fields on the director, validated at configuration boundaries. Disabling the owner or ending a round clears player effects and restores the camera/light before results capture; ordinary event expiry preserves airborne release momentum and lets outstanding stuns expire normally. Replay/scene teardown removes procedural visuals and subscriptions. No scene/prefab YAML wiring is required: the lobby adds the director only when enabled.

The effects use a shared `Resources/RandomEvents/Effects.mat` material and the project-owned URP vertex-color shader `RandomEvent.shader`. **Tools > UmdJam > Configure Random Event Assets** creates the material through Editor APIs. Visual geometry is bounded and reused (two funnels, one ripple, one warning ring/bolt, and four stun halos); updates do not create new objects or materials.

## CPU Players

CPUs turn toward their collector before preparing a delivery, so the assisted-throw facing gate applies to humans and CPUs alike. While stunned, CPU steering and attack output are zero, preparation/recovery timers reset, and navigation replans on recovery without treating the stun pause as a blockage.

While carrying a flask away from their collector, bots occasionally consider an in-range opponent for a stun throw. The opportunity check runs every four seconds, with Noob/Pro/Hacker/God chances of 4%/8%/12%/18%; attempts then have 30/26/22/18-second cooldowns. These are opportunity probabilities, not guaranteed attacks per match. God retains this tactical restraint even though its movement/observation handicaps are removed. Tune the four `*StunChance` fields, `stunCheckInterval`, base `stunCooldown`, and `stunAimTimeout` on `CpuPlayerController`.

An opponent's carried points (weighted by 1.5) plus a capped score-lead bonus must outweigh the bot's carried flask value. Already-stunned, inactive, out-of-range, and empty-handed non-leading opponents are ignored. Bots use the same capsule targeting, facing gate, throw animation, direct transfer, and stun duration as humans; an attack costs the carried flask. The bot pauses movement while aiming, applies its normal reaction/preparation delays, abandons invalid targets, and resumes delivery afterward. A nearby collector takes priority, and bots never chase a player out of their zone for an attack.

Use **Add CPU** on an empty selected lobby slot. Click its difficulty button to cycle **Noob → Pro → Hacker → God**; the default is **Noob**. **Remove** frees a CPU slot. Humans and CPUs share the four-slot limit, and CPU-only matches are supported through the **Start Game** button. Bots do not require or pair devices. Human disconnection still blocks starting until that human reconnects or leaves.

All difficulty levels share humans' maximum movement speed, character turning, touch pickup, carrying limit, throw action, collectors, and score/penalty rules. CPU input ramps and brakes like an analog stick; difficulty changes awareness, decisions, and input timing:

| Difficulty | Planning interval | Reaction delay | Prediction horizon | Decision noise | Strategy weight |
|---|---:|---:|---:|---:|---:|
| Noob | 0.90 s | 0.80 s | 0 s | 0.80 | 0 |
| Pro | 0.45 s | 0.35 s | 0.25 s | 0.40 | 0.2 |
| Hacker | 0.22 s | 0.12 s | 0.8 s | 0.12 | 0.7 |
| God | 0.08 s | 0 s | 2 s | 0 | 1 |

Tune the profiles on the disabled `CpuPlayerController` component in `Assets/Prefabs/Multiplayer/Player.prefab`. The lobby enables it only for CPU participants; human controllers remain human. `CpuSettings` defines the fields, and a missing component falls back to the code defaults. God applies `CpuSettings.WithoutHandicaps` to its effective profile: serialized reaction/noise/awareness/commitment/switching/throw restrictions are ignored, strategy is full strength, and natural movement smoothing/braking are bypassed. Its planning interval and prediction horizon remain tunable; the planning interval bounds path-query work rather than adding a separate reaction wait. For the other levels, reaction delay is the minimum time to notice a newly observed flask, followed by the next planning tick; it does not stop movement toward an existing target.

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

## Player Movement

Human keyboard/stick commands use a short exponential response for acceleration and direction changes (`movementSmoothingTime`, default 0.05 seconds), with a quicker release (`stoppingSmoothingTime`, default 0.03 seconds). These are response time constants: roughly 95% of the change is reached after 0.15/0.09 seconds. Zero disables the corresponding smoothing. Values and state belong to `CouchPlayerController`. Diagonal input is bounded to unit magnitude, partial stick input keeps its speed, and residual motion snaps to exact rest. Human facing follows current paired input directly with a separate frame-rate-independent exponential response (`humanTurnSpeed`, default 90 per second, about 95% after 0.033 seconds). Facing does not inherit the movement filter's delay and remains unchanged on input release or stun. CPU facing retains `turnSpeed` and its existing command path.

Top speed stays 9.5 units/second. CharacterController gravity and environmental movement continue to run at zero input. Spawn, disable, stun, and the non-playing round gate clear residual human input. The human filter allocates no managed memory per update. CPUs bypass it: their existing natural steering and God's immediate input/rotation behavior are preserved. Pickup, attack, and throw rules are unchanged.

## Player Colors

Every human and CPU has a unique color. Use the arrows beside the color name on an occupied ready-up card to cycle through available colors. Paired humans can also use Q/E on keyboard or the controller shoulder buttons. Colors taken by other participants are skipped. Disconnected players and players pending removal retain their colors until teardown; leaving releases the color. Selection locks when the round starts.

The palette contains the 18 selectable Among Us colors: Red, Blue, Green, Pink, Orange, Yellow, Black, White, Purple, Brown, Cyan, Lime, Maroon, Rose, Banana, Gray, Tan, and Coral. Names and RGB values live only in `PlayerColorPalette`; Fortegreen is a fallback rather than a selectable color and is excluded. Palette reference: [Among Us colors](https://among-us.fandom.com/wiki/Colors).

Preferred slot defaults are Red, Blue, Yellow, and Green. `CouchMultiplayerManager.defaultPlayerColors` can override that order per scene; `PlayerColorPalette.ConfigureDefaults` validates the four unique palette indices. Joining chooses the next unused color if a preferred color is occupied. The selected color updates the direction marker, lobby card, HUD border, and the matching collector's material when its shader supports a color property. The hidden placeholder renderer retains its per-player material as a compatibility contract. Color changes reuse existing owned materials; collector material instances are destroyed on teardown. Color choice never changes scoring ownership or player-zone assignments.

## Character Skins and Animation

Each occupied ready-up card exposes a character selector. Paired humans can also cycle with Z/X on keyboard or D-pad down/up; selection locks when the round starts. `CharacterSkinCatalog.asset` is the authoritative list. Testudo is the default entry and replaces the hidden capsule renderer, while the capsule mesh remains on the prefab as a non-rendering material/color contract for existing systems and validation. The Criminal assets remain available for future catalog entries but are not used by the game.

The default entry uses `Assets/Animations/Characters/Testudo/TestudoAnimController.controller`. It drives dedicated idle, running, idle-while-carrying, running-while-carrying, and throw states from the controller's `Speed`, `Carrying`, and `Throw` parameters. Testudo's regular clips import as Humanoid against the valid avatar from `testudo.fbx`; the differently named throw skeleton has its own explicit Humanoid mapping. Because the source throw FBX omits the model's outer -90-degree X-axis transform, the controller uses `ThrowCorrected.anim`, which rotates both its root orientation and translation into the same upright space as the other clips. Every clip bakes root rotation and translation into its pose, locomotion and idle clips loop in place, and root motion stays disabled because `CharacterController` owns movement. The base Testudo prefab is authored at 1.5 scale and its catalog entry uses a 0.75 runtime scale, 1.5 times the previous in-game size. New skins must provide a valid Humanoid avatar in their catalog entry.

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

Landing coordinates use both X and Z magnitudes in `quadrantLandingRange` (currently `2–2.5`). First landings stay before the goals' inward-facing collection bounds, which begin around 3 units on each axis. Later bounces use normal physics. The main scene launch apex is `2.5` units above the launch point.

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

1. at animation release, capsule-casts along the player's horizontal facing direction for the nearest active opponent within `stunThrowDistance` (default 6 units/tiles, measured between player positions), requiring the opponent to be within `assistedThrowFacingAngle` (default 35 degrees) of facing;
2. if an opponent is found, transfers the flask from `ThrowPoint` to their body using the collector-style kinematic animation, following their movement and applying stun on arrival; this takes priority even within collector padding;
3. otherwise uses nearby matching-collector delivery when available and facing its center within the same angle tolerance; facing away uses a normal ballistic throw;
4. otherwise calculates and validates the forward ballistic target from `ThrowPoint`, restores dynamic Rigidbody behavior, and applies velocity and spin;
5. clears the controller's held reference only after a successful release.

The body-height targeting capsule uses `stunCastRadius` (default 0.45 units) and also checks initial overlaps for adjacent opponents. It ignores the thrower, zone triggers, and non-player geometry. The assisted flight disables collisions, so zone boundaries cannot block it. Ordinary ballistic flasks also pass through the existing trigger-only player zones. Player movement remains zone-constrained.

The receiving player's `flaskStunDuration` defaults to 0.9 seconds. Stun blocks movement input, pickup, and throwing, preserving any held flask. The attacking flask drops with free physics restored after arrival; a disabled or destroyed receiver cancels the transfer without stun. Round-end pause freezes transfers and prevents further stun application. Transfer duration shares `directThrowDuration` with collector delivery (default 0.2 seconds).

Current defaults:

- distance: `8` world units;
- apex rise: `3` world units;
- target landing height: `0.35`.

The target follows `transform.forward`, so the direction marker previews the horizontal throw direction.

Each throw records the throwing player's number on the flask. A flask can only score in the matching `Collector_P1` through `Collector_P4` trigger under `Collectors`. A successful collection awards the definition's configured points, raises `CouchPlayerController.ScoreChanged`, and returns machine-spawned flasks to their pool (or destroys standalone flasks). The collection guard prevents duplicate scoring. A flask entering another player's collector remains in play.

Each collector uses three tall, narrow `BoxCollider` walls that follow its triangular footprint. The original convex trigger remains responsible for scoring, while the open-topped solid walls prevent characters from walking through or climbing onto the visible collector without blocking flask entry from above.

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

Players are placed at the center of their assigned quadrant when they join and again when the round starts. Humans and CPUs can move across all quadrant dividers; zones only determine spawn placement and end-of-round flask penalties. Movement uses normal arena collision without quadrant clamping. CPUs observe nearby flasks across zones, consider rivals across zones, and follow unmodified navigation routes back to their own collectors.

The flask machine launches every `0.8` seconds with an active cap of `20`. During the final 10 seconds, its interval ramps smoothly down to `0.25` seconds while the active cap rises to `36`, ensuring a full arena cannot suppress the visible spawn surge.

At timeout the manager closes the round and sets `Time.timeScale` to zero, captures `Results`, applies penalties exactly once, and publishes score/time/end events. Player input is ignored once the round is over, and collectors reject late collection/transfer requests. Destroying the manager restores the time scale for scene changes and Play Mode shutdown.

The snapshot includes held and direct-transfer flasks, but excludes collected or disabled instances. Assignment uses the existing three-dimensional zone bounds: flasks above the zone's maximum Y or outside all zones are excluded. Shared boundaries belong to the first matching zone. Only actual participants receive results; negative scores are valid. The highest final score wins, equal highest scores tie, and a one-player round displays **Round complete**.

The results director eases the camera overhead using unscaled time, then counts each participant's flasks clockwise by slot. Within a zone, entries appear in descending world Z then ascending X order. Each marker shows its point value; the zone displays its combined deduction and starting-to-final score. Authoritative scores are already final; the reveal is presentation only. The arena's baked lighting remains unchanged; translucent UI masks darken areas outside the active zone. The director temporarily hides its serialized ceiling-fixture renderers to keep the overhead arena unobstructed and restores their previous render flags on release. Flask attachment/transfer updates stop immediately at time scale zero, including later callbacks in the timeout frame; late throw animation events also reject round-over releases.

**Skip count** immediately shows final standings. UI Toolkit supports pointer and keyboard/gamepad submit; replay is enabled only after submit release and a short debounce, so holding submit cannot skip and replay together. **Play Again** reloads the scene and returns to fresh player setup, without retaining the roster or selections. Director timing and camera framing are private serialized fields on the scene's `RoundResults` object. The view's mask opacity lives beside the HUD document. Audio is optional and requires both the director's tick source and clip.

## Countdown and Scoring Feedback

During the final 10 seconds, the timer pulses on each displayed second, becomes amber, and displays **Clear your zone**. The final three seconds use red emphasis. Short synthesized countdown ticks grow louder toward timeout; skipped seconds do not replay as a burst. The presentation follows scaled round time and clears before results.

Each occupied corner shows **At risk: −N**, with color-coded **P1 −3**-style labels above eligible flasks. These are current potential deductions, not predictions of where a moving flask will finish. The shared `GameManager.GetFlaskPenaltyPlayer` query retains existing 3D zone/boundary rules, including held and transferring flasks; collected, disabled, above-zone, and outside flasks are excluded. Empty slots have no markers or totals. Zero-point flasks need no warning. The default 32-marker pool is configurable up to 128; totals include every eligible flask even beyond that visual budget.

A successful collector reward displays a rising **+N** popup and expanding ring in the participant's color, with the prepared `Point.wav` cue owned by `GameAudioFeedback`. Rewards arriving while that player's popup is active accumulate into one number. Wrong collectors, duplicate collection, zero-point rewards, and final penalty deductions create no scoring feedback. Popup lifetime defaults to 0.85 seconds. Private serialized fields on `GameplayFeedback` control countdown threshold, popup duration, marker budget, and countdown volume. The countdown clip is generated once and cleaned up with its owned source and transient UI on teardown. `GameAudioFeedback` separately owns shared button clicks, readiness, and point sounds, with its own volume. Imported clips are shared resources and are never destroyed by runtime owners.

Successful flask pickup publishes `PickupFlask.PickedUp` after attachment/physics state is committed. `GameAudioFeedback` plays the prepared `Pickup.wav` during active rounds for human and CPU pickups alike. Rejected/null/already-held pickup attempts produce no event or sound. A later successful pickup of the same flask can sound again. The cue extracts only the first isolated opening from `BottleOpen.mp3`, removes leading silence, starts within 1 ms, shares the existing UI/score cue loudness and volume, and uses preloaded PCM. It has one reusable voice to bound simultaneous pickups. Source MP3 is preserved outside Resources, and subscribers detach on disable.

The central machine emits the supplied bubbling ambience during gameplay only. `MachineFlaskShooter` installs `MachineAmbience` without scene wiring. Two preloaded mono voices crossfade random 2.8–4.2 second excerpts over 0.65 seconds, with subtle 0.95–1.05 pitch and 0.85–1.05 gain variation. Excerpt offsets vary to avoid a fixed full-clip loop. Default ambience volume is 0.18, deliberately below interaction cues; the emitter uses 3D linear attenuation from the machine's position with no Doppler shift. Private serialized fields own these tunings. A separate `System.Random` preserves flask/hazard randomness. Scheduling allocates no managed memory per frame. Pausing suspends both voices; round end, component disable, and machine teardown stop playback. Original `Bubbling.mp3` is retained outside Resources; the preparation tool creates trimmed mono `Bubbling.wav` at -27 LUFS with peak headroom, and the audio import setup preloads PCM.

`GameAudioFeedback` plays prepared `PotionLaunch.wav` (the supplied potion-drink sound) when `MachineFlaskShooter.FlaskLaunched` confirms a successful launch during gameplay. Spawn-cap checks and failed launches do not trigger it. `Connection.wav` plays for a human joining or reconnecting, including regained devices during a round. CPU additions, color/difficulty changes, and component re-enable do not repeat connection announcements. Both cues share the normalized interaction volume and have one reusable voice each.

The supplied `End.mp3` is prepared as `End.wav` with leading silence trimmed and interaction cue loudness matched. `GameAudioFeedback` subscribes to the authoritative `GameManager.RoundEnded` event, so it sounds once when gameplay ends, before results presentation. Playback continues despite the round setting time scale to zero; repeated end requests and component re-enable cannot repeat the cue. Disable stops it and removes the subscription.

`RoundResultsDirector.WinnersRevealed` publishes when final winners are displayed, after counting or after a skip. `GameAudioFeedback` plays prepared `Winner.wav` for a winner or tie; no winner emits no cue. The source MP3 stays outside Resources, leading silence is trimmed, and loudness matches the other interaction effects. Repeated skip requests after completion and audio re-enable do not replay the cue.

Successful ballistic release and direct transfer publish `PickupFlask.Thrown` after their held/free state transition. `GameAudioFeedback` plays prepared `Throw.wav` at release, including collector/opponent transfers, for both humans and CPUs. Rejected and out-of-round throws stay silent; animation wind-up does not play the cue. The preparation tool keeps the first isolated whoosh and removes trailing source noise.

`CharacterAnimationEvents.Footstep(int)` plays one isolated 220 ms mono step at foot-contact events in the run and carry clips. Importer events are derived from each foot's forward swing followed by its low planted pose, rather than a fixed walking timer. The original walking recording is retained outside Resources; its first step is trimmed, tail-faded, and normalized to -30 LUFS. A spatial voice per model uses volume 0.3 and alternating 0.98/1.02 pitch without changing gameplay RNG. Private serialized fields tune volume and the 0.12 second crossfade-event cooldown. Actual grounded planar velocity gates playback, suppressing idle, blocked, airborne, stunned, disabled, paused, and out-of-round contacts. Round end and disable stop the voice synchronously. Animation import setup preserves other events and is safe to rerun.

## Safe Extension Points

- Replace `FlaskPlaceholder.prefab` visuals or point/physics tuning through a `Flask` definition without changing the pickup contract.
- Add flask effects inside `PickupFlask` or a new sibling component rather than inside player input code.
- Use `Ballistics.TryCalculateVelocity` when another system needs trajectories.
- Move player slots and colors to a configuration asset if designers need to tune them frequently.
- Machine flasks are pooled by definition on collection. Reuse resets ownership, bounce count, transform/scale, colliders, Rigidbody state, and direct-transfer state. The active cap excludes pooled instances; each definition retains at most that cap. New instances are created on demand, so first-use creation still has a cost.
- Direct transfers animate in the flask's `Update` without a coroutine or per-throw callback allocation. Disabling a flask, losing/disabling its collector, or completing without collection clears the pending callback and restores free physics, preventing an unavailable flask from occupying the active cap indefinitely. Collection clears transfer state before pooling, so a previous callback cannot affect a reused instance.
