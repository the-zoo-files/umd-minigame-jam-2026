# UI Toolkit Guide

## Files and Responsibilities

- `Assets/UI/CouchPlayerHud.uxml`: hierarchy, element names, default label text, and editing-time state.
- `Assets/UI/CouchPlayerHud.uss`: visual styling, corner anchors, colors, typography, and connected-state visibility.
- `Assets/UI/CouchPlayerHud.cs`: player and score event binding, label text updates, and runtime class toggling.
- `Assets/UI/Fonts/CascadiaMono.ttf`: monospaced HUD font.

Keep those responsibilities separate so UI Builder and design tooling can edit structure and presentation without absorbing gameplay logic.

## HUD Layout Contract

The UXML root is `hudRoot`. Player labels must retain these names:

| Element name | Position |
|---|---|
| `player1Name` | top-left |
| `player2Name` | top-right |
| `player3Name` | bottom-right |
| `player4Name` | bottom-left |

All labels use `player-name`, one player-specific class, and the runtime state class `is-connected`. Their text contains the player name and current score on separate lines.

The `roundTimer` label is inside the full-width `timerContainer`, which anchors it at the top center without fixed-width spacers. It displays the ceiling of the remaining round time in `mm:ss` format and reaches `00:00` before the game pauses.

The labels intentionally include `is-connected` in UXML so they remain visible by default in UI Builder. At runtime, `CouchPlayerHud.OnEnable` removes or applies that class based on actual player state.

## Styling Conventions

Global HUD tokens live under `:root`:

```css
:root {
    --hud-edge: 28px;
    --player-one: rgb(56, 199, 255);
}
```

Use custom properties for values shared by several selectors. Keep player colors aligned with `CouchPlayerController.PlayerColors` until a shared design-token solution is introduced.

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

When adding a per-player HUD element:

1. add it to each player corner container or label structure in UXML;
2. give queried elements stable names;
3. add shared presentation to USS;
4. bind state in `CouchPlayerHud` or a focused HUD component;
5. keep the four corners symmetrical unless the design intentionally differs;
6. confirm default UI Builder visibility and runtime disconnected behavior.
