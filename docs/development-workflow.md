# Development and Validation Workflow

## Safe Editing Boundaries

Directly edit project-owned text files:

- `.cs`
- `.uxml`
- `.uss`
- `.md`
- input action JSON when the Editor is not concurrently changing it

Use Unity Editor APIs for:

- scenes;
- prefabs;
- materials, meshes, and other serialized assets;
- serialized component references;
- hierarchy changes.

Do not edit Unity YAML by hand while the Editor is connected. Do not modify `Library/`, package cache contents, generated solution files, or temporary directories.

## Typical Change Sequence

1. Inspect the relevant scripts, prefab contract, scene objects, and current Console state.
2. Make the smallest coherent source change.
3. Let Unity import and compile.
4. Validate changed scripts.
5. Apply prefab or scene wiring through Unity APIs and save assets/scenes.
6. Clear the Console.
7. Enter Play Mode and exercise the affected path.
8. Inspect state and the Console for errors or warnings.
9. Exit Play Mode.
10. Update documentation if a documented contract changed.

## Minimum Smoke Tests

### Repeatable gameplay checks

Run **Tools > UmdJam > Run Gameplay Smoke Tests** outside Play Mode. The harness opens `Game.unity`, enters Play Mode, uses temporary virtual gamepads, and returns to Edit Mode. Save any authored scene changes first. It checks four-player initialization and paired actions, HUD state, contact pickup and release, rejected transitions/trajectories, machine launch validation, material cleanup, rejoining, and missing-reference diagnostics. Unexpected runtime warnings and errors fail the run; intentional invalid-configuration errors are explicitly matched.

For unattended validation with the project closed in other Editors:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe' -batchmode -nographics -projectPath $PWD -executeMethod UmdJam.Editor.GameplaySmokeTests.Run -logFile smoke.log
```

Do not add `-quit`: the harness exits after leaving Play Mode, with exit code 0 on success and 1 on failure. This complements manual visual and physical-controller checks.

The gameplay harness also checks pooled flask reuse, active-cap accounting, duplicate collection, physics/transform reset, completed and interrupted direct transfers, destroyed pool entries, and HUD timer boundaries/re-enabling. When the working project is already open, run batch validation against a separate copy of `Assets`, `Packages`, and `ProjectSettings`; do not interrupt the user's Editor session.

### Rendering performance

Web builds automatically receive the resolution policy in `Assets/Editor/browser-resolution.js` through `BrowserBuildPostprocessor`. The backing buffer follows the canvas CSS aspect ratio at at most one render pixel per CSS pixel, with a 1920-pixel longest edge and 1920×1080 pixel budget. Browser DPR does not multiply the render buffer; small canvases retain their native CSS resolution. Resize/fullscreen events and `ResizeObserver` update only changed dimensions; hidden canvases retain their last valid buffer. The shader/effects pipeline is unchanged, but high-DPI/large displays intentionally render fewer pixels. This uses Unity's documented `matchWebGLToCanvasSize` option. A custom loader/template must integrate the policy explicitly if it does not use the standard `createUnityInstance(canvas, config, ...)` call; unsupported templates fail the build rather than silently losing the cap.

Run `node tools/tests/browser-resolution.test.cjs` for resolution bounds, hidden/resized/fullscreen canvases, unchanged-buffer reuse, and observer fallback. Run **Tools > UmdJam > Validate Browser Build Integration** (`UmdJam.Editor.BrowserBuildChecks.Run`) for generated-loader integration, repeated processing, and unsupported-template rejection. These checks do not substitute for running a real Web player.

### Navigation baking

**Tools > UmdJam > Bake CPU Navigation** (`UmdJam.Editor.CpuNavigationBaker.Bake`) creates `Assets/Resources/CpuNavigation/Game.asset` through Editor APIs. The build preprocessor regenerates it and checks every spawn-to-collector route before a build. The bake shares `CpuNavigation.CreateBuildSettings` and `CpuNavigation.Bake` with runtime fallback; save authored scene edits before baking. Scene path, bounds, capsule/build settings, and Editor dependency hash guard compatibility. Scene teardown removes only the registration, preserving the shared asset for subsequent rounds.

The CPU suite checks baked-data use across scene restarts, settings mismatch rejection, exact cached path distances, invalid inputs, and zero managed allocations for 200 warmed cache hits. The gameplay suite checks exact apex/zero-rise ballistic boundaries, collector disappearance/disable recovery, interrupted transfer reuse, and zero managed allocations across 200 warmed transfer setup/cancellation cycles.

The PC pipeline already enables the SRP Batcher and GPU Resident Drawer. The arena uses real-time shadowed lights, soft shadows, and screen-space ambient occlusion. Changing shadow resolution/distance, light coverage, or ambient occlusion can change the image; retain these settings until a graphics-enabled player profile and visual comparison justify a specific adjustment. Headless smoke tests validate gameplay and lifecycle behavior, not GPU performance.

### Multiplayer changes

- **Tools > UmdJam > Run CPU Behavior Checks** checks the Noob default, delayed discovery, bounded awareness, target commitment/invalidation, continued movement while observing alternatives, input acceleration, and immediate stopping. **Run CPU Balance Benchmark** runs two seeded flask layouts with all four difficulty-to-spawn rotations (eight 20-second rounds). It writes `.utmp/cpu-balance.csv` with delivered points, zone penalties, final scores, idle seconds, and voluntary target switches. Compare aggregates across rotations; Unity frame/physics timing means seeds do not promise identical results. The benchmark uses falling flasks and does not establish moving-flask interception accuracy or human difficulty.

- The CPU suite also exercises all 18 colors, duplicate rejection, wraparound/occupied-color skipping, invalid requests, material/marker/HUD updates, pending-removal reservations, released-color reuse, and mixed-roster join fallback. The connection-menu suite checks the actual color-arrow event and its layout at 4:3.

- Run **Tools > UmdJam > Run CPU Smoke Tests** (`UmdJam.Editor.GameplaySmokeTests.RunCpu` in batch mode) for difficulty selection, device-free CPU creation, mixed/CPU-only rosters, removal/start races, obstacle navigation, actual pickup/delivery/scoring at every level, round-end stopping, and scene reload cleanup. Run the existing gameplay and connection-menu suites as regressions after changes to shared player or lobby code.

- Run **Tools > UmdJam > Run Connection Menu Smoke Tests** for selected-count bounds, simultaneous joining, duplicate/full-slot rejection, disconnect/reconnect readiness, leaving/rejoining, keyboard/mouse plus gamepads, and round-start/end gating. It captures the empty and ready menu into `.utmp/connection-menu-*.png` with a graphics-enabled Editor.
- The connection menu is wired in `Game.unity`. **Tools > UmdJam > Configure Connection Menu** can restore its component and serialized references using Editor APIs.
- Verify pointer controls and the count selector at 16:9 and 4:3, confirm controls remain readable, and confirm leaving an occupied high-numbered slot permits reducing the count.

- Join Player 1 with keyboard/mouse.
- Join additional players with controllers when device access is available.
- Confirm unique player numbers, quadrant positions, colors, and HUD labels.
- Confirm the stationary camera contains the arena and players.

### Movement or input changes

- Confirm each player reads only its paired device.
- Check diagonal speed and turn response.
- Confirm attack remains left mouse and the gamepad south face button.
- Verify no action-map lookup errors occur.

### Flask machine changes

- Confirm launch origin is the top socket, not the machine base.
- Verify an arc has positive vertical velocity and predicts a landing outside the machine footprint.
- Sample at least eight launches and confirm all quadrants appear; for shuffled-bag validation, a multiple of four should divide evenly.
- Confirm the active-object cap still stops new automatic launches.

### Pickup and throw changes

- Touch a flask and confirm pickup without pressing attack.
- Move and rotate while holding; attachment position and rotation error should remain effectively zero.
- Confirm the flask is above the head while held.
- Throw and confirm release begins at `ThrowPoint`.
- Verify the predicted throw apex and distance match serialized tuning.
- Confirm collision, gravity, and interpolation are restored after release.
- Let the flask land and confirm it settles after the configured small bounce count.
- Throw a flask into the matching player collector and confirm the configured points are awarded once.
- Throw a flask into another player's collector and confirm no score is awarded and the flask remains active.
- Stand inside the matching collector's padded bounds and confirm throwing uses the direct kinematic transfer and awards points.
- Stand outside the padded bounds and confirm throwing still uses the normal ballistic physics path.

### Flask definition or spawner changes

- Confirm the spawner emits the prefab selected by its `Flask` definition from `FlaskSpawner/FlaskSpawnPoint`.
- Confirm the spawned Rigidbody receives mass, damping, gravity, interpolation, and collision-detection values from the definition.
- Change the definition's point value and confirm the collector and HUD use the new value.

### UI changes

- Open the UXML in UI Builder and confirm all four default labels are visible.
- Enter Play Mode and confirm disconnected labels are hidden.
- Join players in order and confirm labels appear clockwise.
- Score a flask and confirm the matching label updates without affecting the other scores.
- Test at more than one aspect ratio.

### Round manager changes

- Run **Tools > UmdJam > Run Round Results Smoke Tests** (`GameplaySmokeTests.RunRoundResults`) for timeout capture, mixed-value penalties, held/direct-transfer eligibility, boundary/outside exclusions, ties, negative scores, paused camera/counting, repeated end/skip, held-submit gating, and replay cleanup.
- **Tools > UmdJam > Configure Round Results** (`RoundResultsSetup.Apply`) idempotently wires the results components in the open Game scene and saves it through Editor APIs. It preserves existing target placement/tuning.
- MCP can select the suite's SessionState flags and call `GameplaySmokeTests.BeginRun` directly when delayed menu callbacks do not advance. The ordinary menu/batch entry points remain available.
- Compare player-selection and results screenshots at 16:9 and 4:3; verify masks, marker projection, deduction center, tie cards, button focus, and resizing. Capture the full Game view including UI; camera-only images may omit the UI document. Screenshots belong in `.utmp/`.
- **Tools > UmdJam > Run Round Results Visual Checks** (`GameplaySmokeTests.RunRoundResultsVisuals`) captures the actual player-selection, count, deduction, and winner UI at 1920x1080 and 1440x1080. It asserts projected marker/mask alignment and replay-button bounds, restores the selected Game view size, and exits Play Mode.

- Confirm a new round starts at the serialized duration and the top-center HUD timer counts down in `mm:ss` format.
- Place known flask configurations in each player zone and confirm the configured point values are deducted from the matching scores exactly once.
- Confirm a flask on a shared zone boundary is assigned to only one player.
- Confirm the timer displays `00:00`, player input stops, and `Time.timeScale` becomes zero after penalties are applied.
- Exit Play Mode and confirm `Time.timeScale` returns to one.

## Console Policy

A change is not ready while it introduces Console errors. Investigate new warnings rather than normalizing them. Framework or validator advisories may be recorded when they are false positives, but runtime warnings from physics state changes, missing references, action lookup, or UI queries must be fixed.

Clear old Console entries before a focused smoke test so the result is attributable to the current change.

## Play Mode Hygiene

- Treat Play Mode mutations as temporary test state.
- Do not rely on a runtime object move as an authored scene change.
- Stop Play Mode before editing persistent scene or prefab state.
- Leave the Editor outside Play Mode after validation.

## Prefab and Scene Contracts to Recheck

After modifying serialized fields or required components, inspect:

- `Player.prefab`: controller, `PlayerInput`, `CharacterController`, touch trigger, sockets, and direction marker.
- `FlaskPlaceholder.prefab`: `PickupFlask`, Rigidbody, collider, and serialized bounce values.
- `Game.unity`: `PlayerInputManager` prefab assignment, HUD document, camera, shooter, flask prefab, and launch point.

## Documentation Maintenance

- Architecture ownership changed: update `architecture.md`.
- Coding or asset convention changed: update `code-style.md`.
- Controls, physics, or gameplay contract changed: update `gameplay-systems.md`.
- UXML names, state classes, or layout policy changed: update `ui-toolkit.md`.
- Test expectations changed: update this guide.

Keep `AGENTS.md` and `CLAUDE.md` as indexes and guardrails, not duplicate manuals.
