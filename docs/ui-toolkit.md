# UI Toolkit Guide

## Files and Responsibilities

- `Assets/UI/CouchPlayerHud.uxml`: hierarchy, element names, default label text, and editing-time state.
- `Assets/UI/CouchPlayerHud.uss`: visual styling, corner anchors, colors, typography, and connected-state visibility.
- `Assets/UI/CouchPlayerHud.cs`: player and score event binding, label text updates, and runtime class toggling.
- `Assets/UI/Fonts/CascadiaMono.ttf`: monospaced HUD font.

Keep those responsibilities separate so UI Builder and design tooling can edit structure and presentation without absorbing gameplay logic.

## HUD Layout Contract

The document contains `connectionMenu` and `gameplayHud`; the shared `is-hidden` class switches between them. `CouchConnectionMenu` presents the initial local multiplayer menu, headed **Players**, with a **Start Game** action. The button remains disabled until every selected slot has a ready CPU or a human with the required device(s). The menu uses four color-coded cards, a pink count selector, and a dark background; graphics are UI Toolkit elements and need no external art.

Connection bindings are `playerCount`, `connectionStatus`, `previousPlayerCount`, `nextPlayerCount`, and `startGame`. Card elements are `connectionPlayer1` through `connectionPlayer4`, with matching `connectionDeviceN`, `connectionStateN`, and `leavePlayerN` elements. Card states are `is-selected`, `is-connected`, and `is-disconnected`. Labels reflect manager state; buttons call its validated methods. Device loss refreshes after Input System finishes updating the paired-device list.

Each occupied card also exposes `characterSelectorN`, `playerCharacterN`, `previousCharacterN`, and `nextCharacterN`. The selector reads from the participant's shared `CharacterSkinCatalog`; arrows are disabled when only one skin is installed and selection locks after the round starts.

CPU controls are `addCpuN` and `cpuDifficultyN`. Empty selected slots show **Add CPU**; CPU slots show **Ready**, a cycling difficulty button, and **Remove**. The `is-cpu` class replaces the controller glyph with **CPU**. Readiness counts humans with connected devices and initialized CPUs. The status uses **{ready} / {selected} ready**, and the gameplay HUD names bots **CPU N**. No device bindings are displayed on buttons.

Menu wording: **Players**, **Start Game**, **Leave**, **Waiting for player**, **Connected**, **Reconnect device**, **Not selected**, **Open slot**, **Ready to start**, and **{ready} / {selected} ready**. Waiting selected slots show **Not connected**; the menu omits keybind instructions.

A CPU navigation failure displays **CPU cannot reach its collector. Check arena navigation and try again.** The manager owns this failure state and throttles repeated unchanged start attempts to one per unscaled second. A lobby change clears the failure and permits an immediate fresh attempt.

The UXML root is `hudRoot`. Player labels must retain these names:

| Element name | Position |
|---|---|
| `player1Name` | top-left |
| `player2Name` | top-right |
| `player3Name` | bottom-right |
| `player4Name` | bottom-left |

All labels use `player-name`, one player-specific class, and the runtime state class `is-connected`. Their text contains the player name and current score on separate lines.

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

Use custom properties for values shared by several selectors. Player colors come from `PlayerColorPalette` and the controller's `ColorIndex`. Runtime inline border colors reflect that selection; USS controls layout rather than duplicating the palette.

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
