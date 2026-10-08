# Round Results Implementation Plan

> **For agentic workers:** Use `superpowers:executing-plans` to implement this plan task-by-task. Use `superpowers:subagent-driven-development` instead if the user chooses delegated execution. Steps use checkbox syntax for tracking.

**Goal:** At timeout, freeze the arena, move the camera overhead, visibly count each participating player's zone penalties, announce the winner or tie, and offer another game.

**Architecture:** `GameManager` captures an immutable results snapshot and applies scores once. A presentation director sequences the camera and reveal using unscaled time, while a separate UI Toolkit view renders zone masks, flask markers, deductions, and results controls. Presentation never changes the scoring outcome.

**Tech Stack:** Unity `6000.6.3f1`, existing URP setup, C#, UI Toolkit, Unity Input System, existing Editor smoke-test harness, Unity MCP for scene inspection and authoring. No new packages.

**Spec:** The user's end-screen sequence and the design in this conversation, made concrete by the behavior contract below. This is a proposed implementation plan, not an already approved or implemented design.

## Behavior contract and assumptions

1. Timeout means an immediate freeze; there is no extra settling time for airborne flasks.
2. Preserve current scoring: every active, uncollected flask whose position is inside a zone contributes its `PointValue`. Held and direct-transfer flasks remain eligible. Do not filter by `IsAvailable`, which would exclude them. Above-zone and out-of-arena flasks are excluded by the current 3D bounds rule. Shared boundaries use `GetZonePlayer`'s first matching zone.
3. Only participants present at timeout receive result cards and compete for the win. Empty player slots do not get a counting sequence. CPU players have exactly the same rules.
4. Preserve negative scores. Highest final score wins; all equal highest scorers share a tie. With one participant, say `Round complete` rather than implying a multiplayer victory.
5. Final scores are authoritative immediately at timeout, as today. The results UI temporarily displays snapshot starting scores and reveals deductions progressively. A missing or interrupted presentation cannot affect scoring.
6. Count zones in player order, clockwise. Within each zone, sort snapshot entries from back to front by world Z descending, then X ascending; preserve insertion order for exact ties.
7. Dim the screen outside the active zone with UI overlays. The nine inspected arena spotlights are baked; disabling them is not the mechanism for darkening the results view. Do not alter the lighting pipeline.
8. `Play Again` reloads `Game.unity` and returns to the existing connection menu. Show `Returns to player setup` below the button so this behavior is clear. Do not promise retained devices, characters, colors, or CPU settings. An instant same-roster rematch is a separate extension.
9. A `Skip count` button can jump to the completed winner display, without scoring again. It cannot also activate replay from the same submit press.
10. Tick audio is optional polish: use a serialized clip only if a suitable existing asset is available. Silence must work without warnings or missing assets.
11. All new UI must match the existing player-selection screen's vibe and style, as explicitly requested by the user. `Assets/UI/CouchConnectionMenu.uss` and its rendered screen are the visual reference. Results are an extension of this game's menu design, not a separate visual theme.

## Global constraints

- Unity: `6000.6.3f1`.
- Use the new Input System; do not add legacy `Input` polling.
- Use UI Toolkit for runtime HUD and menu work.
- Match the existing player-selection UI: CascadiaMono, deep purple backdrop, light cards, rounded corners, bold pink primary controls, and participant-colored borders. Preserve its existing layout and appearance.
- Put tunable values in private `[SerializeField]` fields.
- Do not hand-edit Unity YAML (`.unity`, `.prefab`, `.asset`) while the Editor is available. Use Unity APIs and save through the Editor.
- Never edit `Library/`, `Temp/`, `Logs/`, or generated IDE files.
- Keep generated assets and their `.meta` files together.
- Preserve the existing `RoundEnded`, `RoundStarted`, `IsPlaying`, and immediate final-score contracts.
- Preserve unrelated work, including existing changes in `ProjectSettings/ProjectAuditorSettings.asset` and `.vscode/`.
- Validate scripts, run relevant Play Mode smoke tests, inspect the Console, and leave Play Mode before handoff.

## Review focus

- A direct transfer or collection callback at timeout must not change the recorded result afterward: Tasks 1 and 6.
- A paused game must still finish its camera/reveal animation and accept results input: Tasks 2, 4, and 6.
- Resizing or changing aspect ratio must not shift masks and markers away from the arena: Tasks 3 and 6.
- Repeated end/skip/replay commands and scene teardown must not duplicate scoring, loads, subscriptions, or input actions: Tasks 1, 4, 5, and 6.
- Participant loss, a destroyed flask visual, empty zones, and tied/negative results must not strand the reveal or select a nonexistent winner: Tasks 1, 4, and 6.

## File map

| File | Responsibility |
| --- | --- |
| `Assets/Multiplayer/GameManager.cs` | Capture and expose results; finalize round once |
| `Assets/Multiplayer/RoundResults.cs` (new) | Read-only round snapshot and capture calculation |
| `Assets/Multiplayer/PlayerRoundResult.cs` (new) | One participant's identity, score breakdown, zone, and entries |
| `Assets/Multiplayer/FlaskPenaltyEntry.cs` (new) | Frozen flask point value and world position |
| `Assets/Multiplayer/PlayerFlaskCollector.cs` | Reject collections/transfers after round closure |
| `Assets/UI/RoundResultsDirector.cs` (new) | Sequence ownership, camera, input phase, replay request |
| `Assets/UI/RoundResultsView.cs` (new) | UI binding, projections, masks, labels and button events |
| `Assets/UI/RoundResults.uss` (new) | Results appearance and responsive layout |
| `Assets/UI/CouchPlayerHud.uxml` | Add results hierarchy and stylesheet reference |
| `Assets/UI/CouchPlayerHud.cs` | Hide gameplay HUD while a valid results presentation owns the screen |
| `Assets/Editor/RoundResultsSetup.cs` (new) | Idempotent scene setup and wiring through Editor APIs |
| `Assets/Editor/RoundResultsSmokeTests.cs` (new) | Dedicated behavioral and visual smoke exercise |
| `Assets/Editor/GameplaySmokeTests.cs` | Register results suite in existing runner |
| `Assets/Scenes/Game.unity` | Editor-authored director, view references, and overhead target |
| `docs/architecture.md`, `docs/gameplay-systems.md`, `docs/ui-toolkit.md`, `docs/development-workflow.md` | Update ownership, behavior, UI contract, and validation |

## Task 1: Capture results and close scoring atomically

**Interfaces to introduce** (all in `UmdJam.Multiplayer`):

```csharp
// Read-only runtime data; constructors copy supplied collections.
// One primary type per file; no serialized asset required.
public readonly struct FlaskPenaltyEntry
{
    public Vector3 WorldPosition { get; }
    public int Points { get; }
    public FlaskPenaltyEntry(Vector3 worldPosition, int points);
}

public sealed class PlayerRoundResult
{
    public int PlayerNumber { get; }
    public string DisplayName { get; }
    public Color PlayerColor { get; }
    public Bounds ZoneBounds { get; }
    public int StartingScore { get; }
    public int Penalty { get; }
    public int FinalScore { get; }
    public IReadOnlyList<FlaskPenaltyEntry> Flasks { get; }
    public PlayerRoundResult(int playerNumber, string displayName,
        Color playerColor, Bounds zoneBounds, int startingScore,
        IReadOnlyList<FlaskPenaltyEntry> flasks);
}

public sealed class RoundResults
{
    public IReadOnlyList<PlayerRoundResult> Players { get; }
    public IReadOnlyList<int> WinnerPlayerNumbers { get; }
    public static RoundResults Capture(GameManager gameManager,
        IReadOnlyList<CouchPlayerController> players,
        IReadOnlyList<PickupFlask> flasks);
}

// Added to GameManager.
public RoundResults Results { get; private set; }
```

- [ ] Add focused assertions to the new results smoke suite using the existing `Require`/iterator conventions. Reuse existing player/flask fixture patterns rather than introducing a test framework. Establish these exact cases before implementation:

```text
P1 starting 40, zone entries worth 3 and 7 => penalty 10, final 30.
P2 starting 5, one entry worth 7 => penalty 7, final -2.
Two participants at final 30 => both winner numbers, in player order.
No participant in zone 4 => no zone-4 result or winner.
Boundary x=0 at valid y,z => counted once, using GetZonePlayer.
Held/in-transfer flask in bounds => included; collected/inactive => excluded.
Flask above max Y or outside all zones => excluded.
Capture, move/destroy flask, then inspect => entry position/value unchanged.
EndRound twice => scores unchanged by the second call.
```

- [ ] Register `RunRoundResults` and a mutually exclusive suite flag in `GameplaySmokeTests`; reset the flag in every existing entry point. Compile, run the suite, and confirm failures identify absent behavior rather than fixture setup mistakes.
- [ ] Implement copied, read-only snapshots. Use `PickupFlask.ActiveFlasks`, exclude null/disabled/collected entries, and call the existing `GetZonePlayer`. Snapshot participant identity/color as values so later roster changes cannot rewrite results. Return read-only wrappers, not arrays that consumers can cast and mutate.
- [ ] Replace `ApplyZonePenalties` with snapshot capture followed by one application per participant. Set `IsRoundOver` and `Time.timeScale` before invoking end-related events. Desired sequence:

```csharp
if (!IsPlaying) return;
IsRoundOver = true;
remainingTime = 0f;
Time.timeScale = 0f;
Results = RoundResults.Capture(this,
    CouchPlayerController.ActivePlayers, PickupFlask.ActiveFlasks);
foreach (PlayerRoundResult player in Results.Players)
    CouchPlayerController.ChangeScore(player.PlayerNumber, -player.Penalty);
RoundTimeChanged?.Invoke(remainingTime);
RoundEnded?.Invoke();
```

- [ ] Add a round-state guard to `PlayerFlaskCollector.Collect` before `TryCollect` and to `TryTransfer` before starting a transfer: reject when a `GameManager` exists and `!IsPlaying`. Preserve standalone collector use when no manager exists. Inspect pending animation/transfer callbacks and verify frozen time cannot complete scoring after the snapshot.
- [ ] Run results assertions and existing gameplay/CPU/connection-menu suites. Fix any tests that depended on collecting outside an active round by starting their fixture round, not by weakening production guards.

**Done when:** The frozen snapshot exactly matches immediate final scores, repeats are harmless, and UI is unnecessary for correct scoring.

## Task 2: Add the results director and overhead camera move

**Files:** New `RoundResultsDirector.cs`; subsequent tasks supply its view binding. Namespace `UmdJam.UI`.

**Interfaces:** `public bool IsPresenting { get; }`, `public bool IsComplete { get; }`, `public void SkipToResults()`. Serialized references to `GameManager`, arena `Camera`, `Transform resultsCameraTarget`, and `RoundResultsView`.

- [ ] Add a camera progression check with `Time.timeScale == 0`: start/end poses differ, midpoint progresses, completion lands at the target within tolerance, and an inactive director does not acquire ownership.
- [ ] Validate references in `Awake`; missing required references produce one contextual error and disable presentation. Preserve the gameplay HUD as fallback.
- [ ] Subscribe to `RoundEnded` in `OnEnable`, unsubscribe and stop the active routine in `OnDisable`. Cache the starting camera pose/FOV before acquisition. If enabled after timeout, present `gameManager.Results` immediately without recounting or applying scores.
- [ ] Implement one owned coroutine with explicit phases: camera move, zone count, deduction, winner, replay menu. Ignore a repeated begin while already presenting. Use an elapsed-duration interpolation, not frame-by-frame `Lerp(current, target, speed)`:

```csharp
float elapsed = 0f;
while (elapsed < cameraMoveDuration)
{
    elapsed += Time.unscaledDeltaTime;
    float t = Mathf.Clamp01(elapsed / cameraMoveDuration);
    float eased = t * t * (3f - 2f * t);
    arenaCamera.transform.SetPositionAndRotation(
        Vector3.Lerp(startPosition, resultsCameraTarget.position, eased),
        Quaternion.Slerp(startRotation, resultsCameraTarget.rotation, eased));
    yield return null;
}
arenaCamera.transform.SetPositionAndRotation(
    resultsCameraTarget.position, resultsCameraTarget.rotation);
```

- [ ] Keep perspective projection. The target looks down world -Y with world +Z at the top of the screen, preserving player order. Fit the X/Z arena rectangle to both vertical and horizontal FOV at the current aspect ratio, with serialized padding; recompute at resize. Store authoring target and runtime framing offset separately.
- [ ] Restore the acquired camera pose/FOV and release view/HUD ownership on disable. Do not unpause gameplay on a results-component disable; the game manager owns round pause and teardown.

**Done when:** Camera animation completes while gameplay stays frozen, including at 4:3 and 16:9.

## Task 3: Build the UI Toolkit view and projection

**Files:** New `RoundResultsView.cs`, `RoundResults.uss`; modify `CouchPlayerHud.uxml` and `CouchPlayerHud.cs`.

**View interface:**

```csharp
public bool IsReady { get; }
public event Action SkipRequested;
public event Action ReplayRequested;
public void Begin(RoundResults results);
public void FocusZone(PlayerRoundResult player);
public void ShowFlask(FlaskPenaltyEntry entry, int counted, int count, int penalty);
public void ShowDeduction(PlayerRoundResult player, float progress);
public void ShowWinners(RoundResults results);
public void ShowReplay();
public void End();
```

- [ ] Add a hidden sibling to `gameplayHud`, not a child that will disappear with it:

```xml
<ui:VisualElement name="roundResults" class="round-results is-hidden">
    <ui:VisualElement name="zoneMasks" picking-mode="Ignore">
        <ui:VisualElement name="maskTop" class="zone-mask" />
        <ui:VisualElement name="maskBottom" class="zone-mask" />
        <ui:VisualElement name="maskLeft" class="zone-mask" />
        <ui:VisualElement name="maskRight" class="zone-mask" />
    </ui:VisualElement>
    <ui:VisualElement name="flaskMarker" class="flask-marker">
        <ui:Label name="flaskPenalty" />
    </ui:VisualElement>
    <ui:VisualElement name="zoneScore" class="zone-score">
        <ui:Label name="zonePlayerName" />
        <ui:Label name="zoneCount" />
        <ui:Label name="zoneDeduction" />
        <ui:Label name="zoneFinalScore" />
    </ui:VisualElement>
    <ui:Button name="skipCount" text="Skip count" />
    <ui:VisualElement name="winnerPanel" class="is-hidden">
        <ui:Label name="winnerTitle" />
        <ui:VisualElement name="resultsStandings" />
        <ui:Button name="playAgain" text="Play Again" />
        <ui:Label text="Returns to player setup" />
    </ui:VisualElement>
</ui:VisualElement>
```

- [ ] Query once per enable, validate required elements, and detach button/layout callbacks on disable. Use classes for visibility/focus state and inline styles only for projected geometry and participant colors.
- [ ] Use `CouchConnectionMenu.uss` as the concrete style reference: CascadiaMono; backdrop `rgba(15, 12, 43, 0.97)` for the final panel; primary pink `rgb(224, 13, 91)` with border `rgb(252, 114, 164)`; light cards `rgb(246, 243, 255)` with dark purple text `rgb(37, 25, 64)`; rounded card corners around 16px and participant-colored 4px borders. Reuse the existing `start-game` button class for `playAgain` where its layout fits, adding a results-scoped sizing rule if needed. Keep count-stage masks translucent so the arena remains visible. Use the menu's large bold title hierarchy and generous spacing for winner/standings. Do not introduce an unrelated font, sharp-edged dashboard style, or new accent palette.
- [ ] Project through `RuntimePanelUtils.WorldToPanel` using the actual arena camera and target panel; convert panel coordinates to the results root's local coordinates. Do not mix raw screen pixels with panel-scaled layout. Reject behind-camera points and clamp labels near screen edges.
- [ ] After camera arrival, project the four X/Z zone corners on the arena floor plane. Construct four nonoverlapping dark rectangles around the projected active-zone rectangle, covering the rest of the screen. At exact overhead with the current axis-aligned zones, this creates a clear rectangular opening. Wait for valid UI layout before projecting; update on viewport/layout changes and while the camera framing changes.
- [ ] Render the selected flask's saved position as a bright ring and `−{Points}` label. Animate the marker's scale/opacity with unscaled time. Reuse elements; do not instantiate a marker for every flask every frame.
- [ ] Place the deduction label at the zone's floor-center projection, not its collider's elevated Y center. Use the project's font and participant palette; include text and outlines so dark player colors remain legible.
- [ ] Add `CouchPlayerHud.SetResultsPresentationActive(bool active)` and cache its `gameplayHud` root. When active, hide gameplay labels and timer as a unit; normal score event subscriptions may still update hidden text. On release, restore visibility according to current round/lobby state. Acquire only after view validation succeeds.
- [ ] Visually verify empty, counting, deduction, tie, and replay layouts at 16:9 and 4:3. No runtime USS transitions may be relied on without confirming they advance during pause; explicit unscaled animation is the default.

**Done when:** Masks, marker, and score stay aligned across viewport sizes; final scores do not leak early through the corner HUD.

## Task 4: Sequence zone counting and winner reveal

**Files:** `RoundResultsDirector.cs`, `RoundResultsView.cs`, `RoundResults.uss`.

**Initial private serialized tuning:**

| Value | Default |
| --- | ---: |
| Camera move duration | 1.0 s |
| Zone introduction | 0.25 s |
| Normal flask interval | 0.20 s |
| Minimum flask interval | 0.06 s |
| Preferred counting budget per zone | 3.0 s |
| Deduction animation | 0.65 s |
| Deduction hold | 0.55 s |
| Winner hold before replay | 1.0 s |
| Mask opacity | 0.65 |

- [ ] Add progression assertions for zero, one, and many flasks, and a missing flask GameObject after snapshot. Presentation uses saved positions, so destroying a visual cannot stall or change totals.
- [ ] Iterate participant results in order. Call `FocusZone`, wait using realtime waits, then iterate entries with running count and penalty. Display both `2 / 5 flasks` and the accumulated point penalty; flask count is not interchangeable with point value.
- [ ] Calculate interval with `Clamp(3f / count, 0.06f, 0.20f)` when count is positive. The three-second budget is a preference, not a hard cap: every flask is still shown, so unusually large counts can take longer. Skip is always available.
- [ ] For zero entries, show `No penalty`, omit the per-flask loop, and still show starting/final score. For nonempty zones show the full deduction, then animate the displayed score from `StartingScore` to `FinalScore`; never call `ChangeScore` here.
- [ ] Show standings from the saved result, ordered by final score descending and player number for ties. Winner text uses copied names, not a lookup in the live roster. Handle zero participants defensively with `Round complete` and no winner.
- [ ] Implement skip by stopping the active routine, snapping to the correctly framed overhead view, clearing masks/markers, and showing final standings. It must be idempotent and must not reload the scene.
- [ ] If optional tick audio is wired, reuse one AudioSource and play one tick per step; skip must stop pending ticks. Do not pause all audio with `AudioListener.pause` as part of round closure.

**Done when:** Full playback and skipping produce the same standings and leave simulation paused.

## Task 5: Results input and replay

**Files:** `RoundResultsDirector.cs`, `RoundResultsView.cs`.

- [ ] Inspect the active UI input configuration before adding input plumbing. Use existing UI Toolkit navigation/submit delivery if it works with the paused scene. If missing, configure the Input System's UI support through Editor APIs; do not run two submit handlers in parallel.
- [ ] Enable mouse clicks plus keyboard/gamepad navigation and submit. Only the visible phase's button is focusable: `Skip count` during counting, `Play Again` after results. Do not route gameplay attack into replay.
- [ ] On skip, defer replay activation until the triggering submit has been released and a short unscaled debounce of 0.2 seconds has elapsed. Focus `playAgain` only after this gate. Verify holding A/Enter cannot skip and reload in one press.
- [ ] Add a private replay guard and disable the button before loading. Use the current scene's verified path and `SceneManager.LoadSceneAsync(..., LoadSceneMode.Single)`; reload from time scale zero. Keep the old round frozen throughout loading. Existing manager teardown restores time scale, and the new manager's `Awake` reestablishes the paused lobby.
- [ ] Clear all view subscriptions, routines, owned input actions if any, and presentation state on teardown. Avoid a persistent replay manager or static saved roster for this first version.
- [ ] Test two complete round/replay cycles with a mixed human/CPU roster, CPU-only play, disconnected human controller, rapid repeated clicks, and held submit. Verify fresh scores/time, no active old flasks, no stale player registry entries, and no duplicate callbacks.

**Done when:** Replay opens clean player setup exactly once and the next round starts normally.

## Task 6: Author the scene and run integrated validation

**Files:** `RoundResultsSetup.cs`, `RoundResultsSmokeTests.cs`, `GameplaySmokeTests.cs`, Editor-authored `Game.unity`.

- [ ] Implement `Tools > UmdJam > Configure Round Results` following `ConnectionMenuSetup` conventions. Require Edit Mode; use Undo and serialized references; do not rewrite unrelated scene data.
- [ ] Create/reuse a `RoundResults` scene object for the director and an overhead `ResultsCameraTarget` centered on `GameManager.ArenaBounds` in X/Z. Attach the view beside the existing HUD's `UIDocument`. Assign camera, manager, target, view, HUD, and optional audio references explicitly. Re-running setup must reuse these objects and preserve authored tuning.
- [ ] Use Unity MCP to execute setup, inspect references, save the scene with Editor APIs, and let Unity generate `.meta` files. Check that new scripts and styles import successfully. No scene/prefab/material YAML edits.
- [ ] Add `Tools > UmdJam > Run Round Results Smoke Tests`, implemented by `GameplaySmokeTests.RunRoundResults`, and run it outside Play Mode. Extend the existing harness rather than starting a second competing Editor update loop.
- [ ] Exercise the actual timer reaching zero, not only a direct `EndRound` call. Use a shortened duration in test runtime state. Assert `00:00`, frozen players/physics, immutable totals, overhead camera completion, expected zone order, complete winner UI, and functioning replay at time scale zero.
- [ ] Include tests for a pending direct transfer at timeout, forced collector calls after timeout, second `EndRound`, director disable/re-enable, destroyed flask visual, empty zone, tie, negative winner, one participant, absent slot, and repeated skip/replay. Use wall-clock deadlines so a stalled sequence fails rather than hanging the Editor.
- [ ] For authored UI screenshots, capture the Game view with the UI document included; a camera-only capture may omit screen-space UI. Save screenshots to `.utmp/round-results-*.png`. Inspect camera arrival, each zone focus, deduction, tie, and replay at 1920x1080 and 1440x1080, plus a resize during counting.
- [ ] Capture the existing player-selection screen at the same resolutions and compare it beside the results screen. Verify matching typography, pink primary button styling, card borders/radii, purple/light color treatment, and spacing rhythm; verify that reuse of menu styles has not changed player selection itself.
- [ ] Run gameplay, connection-menu, CPU, and results smoke suites. Clear the Console before each focused run, inspect new errors/warnings, and leave Play Mode after each suite. Validate changed scripts after the final code edit.
- [ ] Confirm no custom render feature, altered baked lighting, new material-per-flask allocations, or inactive pooled flasks being counted. Check that the moving camera still frames arena edges and frozen airborne objects acceptably.

**Done when:** All behavioral suites pass, screenshots are visually reviewed, and the Editor is in Edit Mode with authored changes saved.

## Task 7: Documentation and handoff

- [ ] Update architecture ownership and file map for snapshot/director/view.
- [ ] Update gameplay rules to explicitly document timeout freeze, held/airborne/transfer treatment, boundary ownership, ties, negative scores, and replay-to-setup behavior.
- [ ] Update UI guide with results element names, presentation ownership, world-to-panel projection, and input behavior.
- [ ] Update validation guide with results menu commands and the new test scenarios. Retain existing immediate-score expectations and explain the delayed visual reveal.
- [ ] Review the final diff for unrelated edits and missing `.meta` files. Stage only task-owned files if committing; use coherent commits after validated milestones rather than sweeping all workspace changes into a commit.
- [ ] Handoff with a concise behavior summary, validation results, screenshot paths, serialized tuning locations, and any remaining limitation. Do not describe unrun graphics/controller checks as passed.

## Implementation order and acceptance

Run Tasks 1–7 in order. Task 1 establishes correct scoring independent of presentation; Tasks 2–4 establish the reveal; Task 5 closes the replay loop; Task 6 verifies the actual authored scene; Task 7 records the contracts.

The feature is accepted when timeout produces exactly one scoring result, camera and counting run while the world is paused, every eligible flask is visibly counted once, each participant sees their deduction centered in their zone, winner/tie display is correct, and replay reliably returns to fresh player setup.

Deferred extensions: instant same-roster rematch, X/Z-only airborne scoring, actual runtime lighting changes, world-space flask outlines, and custom victory character animation. None is required for the sequence requested here.

## Implementation and validation record — 2026-10-07

Implemented on `codex/round-results` in the Unity-connected checkout. Scene wiring was saved through Unity APIs, and the CPU navigation bake was regenerated for the changed scene dependency hash.

Verified in the graphics-enabled Unity Editor:

- Round results smoke suite: PASS, including mixed-value penalties, held/transfer/boundary filtering, ties/negative scores, frozen snapshot positions, timeout-frame transfer/attachment/throw/spawner guards, actual gamepad skip/replay, keyboard replay after controller removal, and repeated scene reloads.
- Gameplay smoke suite: PASS. Updated two outdated test-fixture assumptions: the ballistic throw position now lies outside the enlarged collector, and release position is compared with the event-time socket pose rather than the hand's later animated pose.
- CPU smoke suite: PASS with regenerated baked navigation.
- Connection-menu smoke suite: PASS.
- Results visual checks: PASS at 1920x1080 and 1440x1080. Screenshots of player selection, counting, deduction, and winner were inspected; marker/mask projection and replay bounds are also asserted.

Implementation refinements: use the installed `CameraTransformWorldToPanel` API; defer focus until styles resolve; hide only serialized ceiling-fixture renderer geometry while results owns the camera; keep the count summary above the arena so it does not cover flasks, then center the deduction. Existing baked lighting and the player-selection visual style are preserved. Tick audio remains optional and unassigned.

Screenshots are `.utmp/round-results-{lobby,count,deduction,winner}-{1920,1440}.png`; tie capture is `.utmp/round-results-tie.png`. The Editor was left outside Play Mode. Changes remain uncommitted for user review.
