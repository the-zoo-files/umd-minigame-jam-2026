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

### Multiplayer changes

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

### UI changes

- Open the UXML in UI Builder and confirm all four default labels are visible.
- Enter Play Mode and confirm disconnected labels are hidden.
- Join players in order and confirm labels appear clockwise.
- Test at more than one aspect ratio.

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
