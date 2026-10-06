# Browser performance and robustness audit

Reviewed 2026-10-06 against `d8a2c73` and the current color changes. This is a source/settings audit with a desktop measurement, not a browser performance certification.

## Scope and evidence

Reviewed every project-owned runtime C# file: gameplay/ballistics/flask pooling, player and CPU controllers, navigation, roster/device lifecycle, scoring/round lifecycle, collectors, and UI. Also inspected the Editor validation harnesses, physics/time settings, Web player settings, render pipelines, texture import settings, and build scene list.

A hidden Unity copy ran a warmed desktop probe with four God bots and 12 stationary available flasks. Across 200 simultaneous four-bot planning bursts:

| Measurement | Desktop result |
|---|---:|
| Start, including shared navigation build and spawn-path checks | 13.114 ms |
| Planning burst median | 0.286 ms |
| Planning burst p95 | 0.337 ms |
| Planning burst maximum | 0.543 ms |
| Managed allocations during 200 bursts | 0 bytes |

These timings exclude rendering, normal physics simulation, initial JIT warmup, browser/Wasm overhead, and native allocation accounting. Flask motion was frozen to keep candidates stable. They establish that AI planning is currently cheap on this desktop, rather than proving a browser frame-rate target.

The installed editor has only Windows Standalone playback support. Actual Web compilation, deployment headers, GPU timings, controller discovery, and browser memory could not be verified here. CPU, gameplay, and graphical menu suites passed, including color-selection lifecycle checks. A first-import warning from an old ProBuilder sample material was absent on the cached rerun; sample assets were preserved.

## Prioritized findings

### 1. Validate a real browser build before release

Editor tests do not exercise the Web renderer, browser controller discovery, WebAssembly memory growth, shader support, or hosting configuration. Install matching Web Build Support and create a Development build for profiling plus a release build for final verification. Exercise four God bots at the 12-flask cap, actual human controllers, unplug/replug, tab hiding/resuming, resizing/fullscreen, round completion, and repeated scene restarts. Verify that the browser heap stabilizes after warmup and that deployment compression and MIME settings match the build.

This is an unverified platform boundary, not a confirmed crash.

### 2. Prebake static navigation: startup improvement

`CpuNavigation.Build` synchronously calls `BuildNavMeshData` from `CouchMultiplayerManager.TryStartGame`. The measured Start operation consumes roughly 13 ms on this desktop. The arena is static, so editor-baked scene navigation can remove runtime baking and its temporary source/build memory from browser startup. Preserve player capsule dimensions and geometry exclusions in one bake configuration, validate the data at build time, and retain clear failure handling for missing/incompatible navigation.

A valid registered NavMesh alone does not guarantee usable paths; the existing spawn-to-collector checks should remain.

### 3. Cap browser rendering resolution: likely GPU risk

WebGL defaults to quality index 0, selecting the Mobile pipeline. This is already cheaper than PC: render scale 0.8, one 1024 main-light shadow cascade, no additional-light shadows, no SSAO renderer feature, and GPU Resident Drawer disabled. The heavier PC configuration is not the default Web profile.

The project uses the default Web template and has no explicit canvas/DPI budget. Unity's default browser canvas follows device pixel ratio. DPR 2 produces four times the pixels of DPR 1 at the same render scale. Introduce a deliberate DPI/maximum render-resolution policy, then profile HDR, shadow distance, light count, and imported lightmap/sky texture residency in the Web build. Source EXR/HDR file sizes are not runtime texture memory measurements.

[Unity canvas sizing](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-canvas-size.html)

### 4. Tune Web memory using measured residency

Player settings specify a 32 MiB initial heap, geometric growth, and a 2048 MiB maximum. The maximum is a ceiling, not an initial allocation or a guarantee that the browser can supply it. Set initial memory using profiled startup usage with headroom; raising the maximum arbitrarily does not solve allocation failure. Include decoded asset data, texture/graphics memory, and peak initialization in measurements.

[Unity Web memory](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-memory.html)

### 5. Bound and spread AI path work: preventative improvement

God plans every 0.08 s. Each available flask can cause an incoming and return path calculation: up to 24 per bot, 96 per simultaneous four-bot burst, or roughly 1200 per second at the current cap. Calls are synchronous and bots share similar planning phases. Carrying bots also recalculate largely static return paths; idle strong bots repeatedly calculate staging paths.

The probe shows this is inexpensive on this desktop and allocation-free on the managed side. Prefer staggered planning, caching unchanged home/staging routes, and a shared query budget if browser profiling warrants it. Preserve response to target loss and pickups; lowering every difficulty blindly would weaken behavior. Invalidate caches when navigation or destinations change.

[Unity CalculatePath](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMesh.CalculatePath.html)

### 6. Define tab suspension and catch-up behavior

`runInBackground` is false, but there is no explicit focus/pause state owned by `GameManager`. The physics step is 0.02 s and maximum allowed timestep is approximately 0.333 s, allowing about 16 catch-up steps in a sufficiently slow frame. This can amplify a frame-time spike on weaker devices. Profile slow frames and tab resumes; centralize pause behavior with round state and choose a smaller catch-up budget after confirming the intended timer/game-speed tradeoff.

The product policy must decide whether a hidden local game pauses or continues. Avoid competing writers to `Time.timeScale`.

### 7. Remove direct-transfer transient allocations if profiling warrants it

A direct throw creates a captured callback in `CouchPlayerController.ThrowHeldFlask` and a coroutine iterator in `PickupFlask.MoveDirectly`. Pooling the GameObject does not pool these allocations. At the default 2.25 s launch interval this is a small bounded allocation stream, not an established GC bottleneck. A flask-owned transfer state could remove it while retaining cancellation, scoring guards, and reset behavior.

### 8. Tighten lifecycle and invalid-configuration failures

- Destroy an unregistered previous `NavMeshData` before replacement after failed registration/retry. Successful navigation is cleaned up on teardown; failed registration is a potential resource-retention edge case.
- Give actionable lobby feedback on start failure and avoid repeating expensive work or diagnostics on repeated clicks.
- Define recovery when a direct-transfer collector disappears, or a transfer is interrupted without returning to the pool. Otherwise a kinematic, non-colliding flask can remain in the active cap. Normal scene unloading destroys these objects, so this concerns runtime removal/disable.
- `Ballistics` clamps fall-height difference to 0.01. For zero-rise launches with equal origin/target height, it reports a finite trajectory despite there being no positive-time exact solution; large horizontal velocities can result. Validate that boundary before supporting such tuning. Current 3/4.5-unit rises avoid it.
- Inspector range attributes do not validate runtime finite values. Validate duration, launch interval/cap, movement, and CPU tuning at configuration boundaries if runtime tuning is introduced. Current checked-in values are finite and bounded.

## Healthy existing behavior

- Four participant slots and 12 active machine flasks bound the normal gameplay population.
- Pools reuse collected flasks, bound retained instances per definition, exclude inactive/held/transfer flasks from AI targets, and destroy inactive instances with their owner. No ordinary unbounded pooling leak was found in the current single-definition scene.
- AI uses lifecycle registries and reusable path corner buffers instead of per-frame scene searches.
- HUD timer formatting occurs once per displayed second. Lobby, scores, and colors update through events.
- Owned player materials are created once and destroyed on teardown. Color changes reuse them. Supported collector material instances also have teardown cleanup.
- Held/direct-transfer flasks disable unnecessary collision work. Physics population is capped and collision callback reuse is enabled.
- The round-end object search and small penalty allocation happen once, not in a gameplay hot loop.
- Component/action caching, subscriptions, pooling resets, duplicate scoring, unique color ownership, and human/CPU lifecycle behavior have repeatable Play Mode coverage.

## Recommended implementation order

1. Establish an actual Web build and browser baseline.
2. Prebake navigation and set a deliberate canvas resolution budget.
3. Measure/tune startup heap, texture residency, and background/catch-up behavior.
4. Add AI scheduling/caching if browser CPU timings justify it.
5. Address small transfer allocations and lifecycle/configuration edge cases with focused tests.

The desktop timing probe does not justify a browser frame-rate guarantee or a rendering-quality reduction by itself.

## Implemented follow-up

- Static navigation now prebakes through Editor APIs and is regenerated/route-validated before builds. Runtime registers compatible data, retains the spawn checks, and falls back to the same shared bake configuration when needed. Teardown preserves shared assets and destroys owned fallback data, including failed registrations.
- Web build postprocessing installs a canvas resolution budget independent of DPR, bounded by a 1920-pixel longest edge and 1920×1080 pixels. The visible CSS size/aspect ratio and render effects remain unchanged; high-DPI/large displays intentionally use fewer render pixels. Resize/fullscreen/hidden-canvas behavior and build-loader integration have dedicated checks.
- A fixed 64-entry exact-endpoint cache reuses unchanged return-route distances. Failed and successful static queries are bounded, changed destinations miss the cache, and replacing navigation clears it. Bot cadence and decision weights remain unchanged; scheduling/query throttling was deferred to preserve response timing without browser profiling evidence.
- Direct transfers now use flask-owned animation state and a cached collector delegate, removing the captured callback and coroutine iterator. Disable/lost-receiver/unconsumed-completion recovery restores free physics; callbacks clear before pooling to protect immediate reuse.
- Start failures now have lobby feedback and an unscaled retry throttle. Zero-rise/equal-height trajectories reject an impossible positive-time solution, and targets exactly at the apex use the exact fall time.

Real Web build profiling remains required: this Editor installation lacks Web Build Support. Heap sizing, decoded texture residency, shadow/HDR tradeoffs, tab pause policy, and physics catch-up limits remain measurement/product decisions. They were not changed speculatively.

Validation completed in a separate hidden Unity project copy: navigation bake/route validation, CPU suite, gameplay suite, graphical connection-menu suite, build-loader integration checks, and Node canvas-resolution tests all passed. The suites measured zero managed allocations for 200 warmed distance-cache hits and 200 warmed direct-transfer setup/cancellation cycles. These allocation checks exclude scoring/HUD updates and do not measure browser or GPU frame time. Every Play Mode suite returned to Edit Mode before exit.
