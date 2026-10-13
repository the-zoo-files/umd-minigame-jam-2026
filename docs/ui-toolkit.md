# UI Toolkit Guide

## Files and Responsibilities

- `Assets/UI/Documents/CouchPlayerHud.uxml`: hierarchy, element names, default label text, and editing-time state.
- `Assets/UI/Documents/Lobby.uxml`: the reusable four-card lobby hierarchy and prompt elements.
- `Assets/UI/Styles/CouchPlayerHud.uss`: visual styling, corner anchors, colors, typography, and connected-state visibility.
- `Assets/UI/Styles/CouchConnectionMenu.uss`: the Figma-matched lobby layout, card variants, palette classes, and device prompts.
- `Assets/Scripts/UI/CouchPlayerHud.cs`: player and score event binding, label text updates, and runtime class toggling.
- `Assets/UI/Fonts/CascadiaMono.ttf`: monospaced HUD font.

Keep those responsibilities separate so UI Builder and design tooling can edit structure and presentation without absorbing gameplay logic.

## HUD Layout Contract

The `eventOptions` row contains `eventEarthquake`, `eventTornadoes`, and `eventLightning` buttons.
They reuse the master switch's `is-on` colors and show explicit On/Off labels. The row is disabled
while the master switch is off, and all event controls lock after starting. Individual choices
are authoritative in `CouchMultiplayerManager` and remain selected when toggling the master.

The `randomEvents` button toggles the manager's authoritative **Random Events: Off / On** setting before play. `is-on` styles the enabled option. The gameplay `randomEventStatus` label lives below the timer, listens to event transitions, and shows Earthquake, Tornadoes, or Lightning; it is hidden during quiet intervals and results. Both controls use the existing document and do not own hazard state.

The document contains a `Lobby.uxml` template instance named `connectionMenu` plus `gameplayHud`; the shared `is-hidden` class switches between them. `CouchConnectionMenu` presents the Figma lobby headed **Join and select a character.**, with four alternating tilted cards and an **OK!** action. The lobby starts with four empty slots and no player-count selector. OK fills empty slots with Noob CPUs; existing humans must explicitly confirm readiness and have their required devices. Empty, joined, and ready card states are USS classes; joined cards stay neutral with participant-colored portrait/name accents, while ready cards use the participant color as their surface.

Connection bindings are `connectionStatus` and `startGame`; player-count controls are removed. Card elements are `connectionPlayer1` through `connectionPlayer4`, with matching `cardActionN`, `cardPromptGlyphN`, `cardPromptTextN`, `connectionDeviceN`, `connectionStateN`, and `leavePlayerN` elements. Card states include `is-empty`, `is-connected`, `is-ready`, `is-disconnected`, `is-keyboard`, and `player-color-N`. Labels reflect manager state; buttons call its validated methods. Device loss refreshes after Input System finishes updating the paired-device list and clears explicit readiness.

Each occupied card also exposes `characterSelectorN`, `playerCharacterN`, `previousCharacterN`, and `nextCharacterN`. The selector reads from the participant's shared `CharacterSkinCatalog`; arrows are disabled when only one skin is installed and selection locks after the round starts.

CPU controls `addCpuN` and `cpuDifficultyN` remain in the hidden compatibility data region used by tooling. CPUs are immediately ready, keep their difficulty/color APIs before play, and the visible card action removes them. Readiness counts only explicitly confirmed humans plus initialized CPUs. The hidden status announces the number of CPUs that will fill empty slots, or uses **{ready} / {selected} ready** while existing participants are unavailable, and the gameplay HUD names bots **CPU N**.

Visible menu wording is **Join and select a character.**, **Join**, **Ready!**, **Cancel**, and **OK!**. Device-specific glyphs come from the imported Kenney input prompt fonts; title/name/button typography uses Fredoka One and the waiting prompt uses Fredoka. Unknown gamepads deliberately fall back to Xbox-style glyphs.

The lobby title's 6px outside stroke is represented by three labels in `lobby-title-stack`: a shifted, fully outlined shadow silhouette; an orange outlined duplicate; and an unoutlined cream foreground label. All three use `FredokaOne-Lobby-SDF.asset`, generated through **Tools > UmdJam > Configure Lobby Font Asset** with 24px atlas padding so the outline and shadow have enough distance-field range. The backing labels use a 12px centered outline; the foreground label masks the orange layer's inner half, leaving the intended 6px outside edge. The shadow duplicates that complete outlined silhouette so it follows the outside edge rather than the raw glyph. Keep their text, font, size, alignment, and bounds identical.

Character names, card prompts, and the OK label use the same three-layer construction as the header: a stroked shadow silhouette, an outside-stroke backing label, and an unoutlined foreground label. Their 6px centered backing outlines leave 3px visible after the foreground masks the inner half. Runtime code must update all three members of each dynamic stack. Fredoka One and Fredoka Regular each have a 24px-padded lobby SDF asset so these outlines do not clamp. Join and OK remain enabled as UI elements even when their actions are unavailable; the multiplayer manager remains the authority that rejects an invalid ready/start request. This avoids runtime-theme disabled fading. The OK pointer is hidden normally and appears only while the button is hovered or focused by navigation.

A CPU navigation failure displays **CPU cannot reach its collector. Check arena navigation and try again.** The manager owns this failure state and throttles repeated unchanged start attempts to one per unscaled second. A lobby change clears the failure and permits an immediate fresh attempt.

The UXML root is `hudRoot`. Player labels must retain these names:

| Element name | Position |
|---|---|
| `player1Name` | top-left |
| `player2Name` | top-right |
| `player3Name` | bottom-right |
| `player4Name` | bottom-left |

All labels use `player-name`, one player-specific class, and the runtime state class `is-connected`. Their text contains the player name and current score on separate lines. Score cards match the ready-up cards' light lavender surface, dark purple text, and rounded corners, with a compact 4px border in the participant's selected color. Shared styling keeps every player corner consistent; padding preserves clearance above/below the risk labels.

The `roundTimer` label is inside the full-width `timerContainer`, which anchors it at the top center without fixed-width spacers. It displays the ceiling of the remaining round time in `mm:ss` format and reaches `00:00` before the game pauses. The HUD formats and assigns text only when the displayed second changes, resetting its presentation cache on enable; the authoritative timer and its events remain frame-accurate.

The labels intentionally include `is-connected` in UXML so they remain visible by default in UI Builder. At runtime, `CouchPlayerHud.OnEnable` removes or applies that class based on actual player state.

The connection menu is the default document preview. To inspect the gameplay labels in UI Builder, temporarily remove `is-hidden` from `gameplayHud` and add it to `connectionMenu`; restore the default classes before saving. Runtime visibility is always derived from lobby state.

## Styling Conventions

Global HUD tokens live under `:root`:

```css
:root {
    --hud-edge: 28px;
}
```

Use custom properties for values shared by several selectors. Player colors come from `PlayerColorPalette` and the controller's `ColorIndex`. The lobby maps those indices to `player-color-N` USS classes so the same color drives the ready surface, portrait border, device outline, and character name without per-element inline styles.

Corner labels use absolute positioning:

- top-left: `top` and `left`;
- top-right: `top` and `right`;
- bottom-right: `bottom` and `right`;
- bottom-left: `bottom` and `left`.

Do not add fixed-width spacer elements to position corner HUD items.

## Runtime State

Disconnected labels have zero opacity through the base class. Connected labels override opacity:

```css
.player-name {
    opacity: 0;
}

.player-name.is-connected {
    opacity: 1;
}
```

The positive state name keeps C# readable:

```csharp
label.EnableInClassList("is-connected", isConnected);
```

Use additional semantic classes for future states (`is-ready`, `is-stunned`, or `has-flask`) rather than setting many inline styles from C#.

## C# Binding Rules

- Query elements once in `OnEnable` and cache them.
- Log a contextual error when a required named element is absent.
- Subscribe to player events after the visual tree is available.
- Subscribe to `ScoreChanged` and update only the matching cached label.
- Subscribe to `GameManager.RoundTimeChanged` and cache the `roundTimer` label alongside player labels.
- Replay the existing active-player list after subscribing so scene reloads and enable-order differences produce the same HUD.
- Unsubscribe in `OnDisable`.
- Keep displayed player numbering one-based.

## Figma-Friendly Practices

- Keep meaningful layer/element names in UXML.
- Prefer flat, comprehensible visual hierarchy over layout-only nesting.
- Store colors, spacing, and typography in USS rather than C#.
- Use a project font asset instead of an operating-system font reference.
- Keep runtime states represented by classes so imported or translated designs can map to variants.
- Verify imported style changes at common aspect ratios; corner anchoring should remain stable.

## Adding HUD Features

### Round results

`RoundResults.uss` and the `roundResults` sibling in `CouchPlayerHud.uxml` define the end screen. Match the player-selection screen: CascadiaMono, deep purple background, pink winner banner, light rounded cards with participant-colored borders, and the existing light `start-game` button style for replay. Compare both screens at the same resolution after visual changes.

`RoundResultsView` queries `zoneMasks`, `maskTop/Bottom/Left/Right`, `flaskMarker`, `flaskPenalty`, `zoneScore`, `zonePlayerName`, `zoneCount`, `zoneDeduction`, `zoneFinalScore`, `winnerPanel`, `winnerTitle`, `resultsStandings`, `skipCount`, and `playAgain`. The four masks leave the overhead zone's projected floor rectangle clear. Positions use `RuntimePanelUtils.CameraTransformWorldToPanel` and root-local coordinates; layout and camera changes are reflected each presentation frame. Final standings contain only snapshot participants.

`CouchPlayerHud.SetResultsPresentationActive` toggles `results-active` on the document root. The results stylesheet hides `gameplayHud` under that class, so a late connection-menu refresh cannot expose final scores during counting. Existing HUD score subscriptions remain active underneath. Invalid/disabled results presentation releases this ownership and leaves authoritative scores accessible through the existing HUD.

Results buttons use UI Toolkit navigation/submit. A release-observation Input Action in the director only gates replay availability; it does not dispatch submit, avoiding duplicate button activation. Count animations and waits use unscaled time. UI structure remains previewable in UI Builder by changing `is-hidden` classes temporarily, restoring defaults before saving.

When adding a per-player HUD element:

1. add it to each player corner container or label structure in UXML;
2. give queried elements stable names;
3. add shared presentation to USS;
4. bind state in `CouchPlayerHud` or a focused HUD component;
5. keep the four corners symmetrical unless the design intentionally differs;
6. confirm default UI Builder visibility and runtime disconnected behavior.

## Ready-Up Color Selection

Each occupied card exposes `colorSelectorN`, `playerColorN`, `previousColorN`, and `nextColorN`. Arrow buttons call the manager's validated color-cycling operation. The name and swatch share the palette color, with contrasting text for dark/light entries. Empty cards hide the selector. Selection is event-driven and does not add per-frame palette or material work. HUD corner classes define position only; color is applied from participant state.

## Gameplay Feedback

`GameAudioFeedback` owns the three prepared WAV sounds under `Assets/Sounds/Resources/Audio`: `ButtonClick`, `Ready`, and `Point`. Original MP3s live outside Resources in `Assets/Sounds/Source`. Prepared assets trim leading silence, share measured BS.1770 loudness, and retain at least 3 dB of true-peak headroom. They import as preloaded PCM; the owner also requests decoded data at initialization to avoid first-interaction loading. The HUD installs it once. Lobby/results buttons share one `clicked` subscription each, covering mouse and UI Toolkit submit. Readiness announces only a false-to-true `CanStart` transition; scoring listens to positive `FlaskCollected` rewards. Three cached 2D sources allow one voice per cue, bounding rapid overlap. Disable unsubscribes and stops playback; re-enable seeds readiness without repeating it. One privately serialized volume applies equally to all prepared cues. Missing clips warn once per cue at initialization and remain silent. `GameplayFeedback` retains only the generated countdown sound.

`GameplayFeedback.uss` styles the urgent/critical timer, `countdownWarning`, the four `zoneRiskN` labels, and the `feedbackOverlay` children `penaltyMarkers` and `collectionFeedback`. The overlay is inside `gameplayHud`, uses ignored picking, and is hidden with gameplay during the lobby/results. Runtime creates bounded marker labels and one `scorePopupN`/`collectorPulseN` pair per slot. Popups and markers project world positions through the same camera-to-panel convention used by results. Color borders identify participants; marker text remains white for readability across all palette choices. Scaled animation time freezes with gameplay. Display caches avoid rebuilding unchanged risk/marker text, and pools eliminate per-frame object creation. Begin/re-enable binds current round state; disable removes transient elements, restores timer scale/classes, stops sounds, and unsubscribes.
