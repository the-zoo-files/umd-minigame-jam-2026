# Lobby UI Figma Audit

Target: Figma node `59:161`, **Desktop - 2 — Red palette**, at 1920×1080.

The implementation uses `Assets/UI/Documents/Lobby.uxml` and
`Assets/UI/Styles/CouchConnectionMenu.uss`. Measurements below are authored pixels from Figma;
UI Toolkit scales the document through the existing panel settings.

## Node-by-node fixes

| Figma node | Element | Required implementation |
|---|---|---|
| `59:161` | Screen | 1920×1080 reference frame; warm white `#f4f1ee`; vertical header, flexible stage, and 208px footer. |
| `59:162` | Header | 1920×160, orange `#eb5628`, centered content, 32px bottom padding. |
| `59:163` | Header title | Fredoka One Regular, 64px, `#f4f1ee`, no wrap, a strong downward 25% black shadow, and a 6px **outside** dark-orange edge. UI Toolkit cannot place a text outline outside, so render an outlined duplicate behind an unoutlined cream foreground label. A third, backmost stroked duplicate is shifted down to make the shadow follow the complete post-stroke silhouette. The edge remains `#c94721` so it stays distinct from the `#eb5628` header while retaining the red-palette hue. All labels use the dedicated Fredoka One SDF asset with 24px atlas padding; a 12px centered backing outline leaves 6px visible after the foreground masks the inner half. |
| `59:164` | Pattern field | Begins below the header and fills the remaining stage; use the local beaker pattern at crop scale over `#f4f1ee`. |
| `59:165`–`59:211` | Flask/beaker motifs | Preserve the local background texture’s Figma-derived 96px motifs, positions, rotations, palette tints, and edge clipping. |
| `59:213` | Card stage | Flexible height, bottom-aligned; 155px horizontal inset. |
| `59:214` | Card row | Four cards, 32px visual separation, centered, 652px rotated bounding height. |
| `59:215` | Ready red card | 360×640; −2°; `#eb5628` surface; 24px corners; 16px top/side and 32px bottom padding. White keyboard icon at 96px, 256px portrait, 64px ready name, and cream action pill. The name shadow is a synchronized duplicate behind the outlined foreground. |
| `59:216` | Joined blue card | 360×640; +2°; neutral `#e7e2da`; controller icon 96px; blue `#4570bc` portrait ring/name; 56×96 arrows; 48px name; cream action pill. The name uses synchronized foreground/shadow labels. |
| `59:217` | Joined yellow card | Same as node `59:216`, −2°, with yellow `#f5d548` accent. |
| `59:218` | Empty card | 360×640; +2°; gray `#cbc8c1`; faint 512px dot field; overlapping 180px keyboard/controller marks; cream action pill. |
| `I59:215;59:94`–`59:101` | Ready-card content | 16px device/content separation; portrait overlaps the 64px character row by 32px; name uses Fredoka One with the Figma shadow. |
| `I59:216;59:136`–`59:145` | Joined-card content | 16px main gap, 24px portrait-section top/gap, full-width selector, and symmetric arrows. |
| `I59:215;59:102`–`59:108` | Cancel action | 64px-high cream pill, 32px horizontal/8px vertical padding, 48px prompt glyph, 8px gap, Fredoka One 36px orange label, 2px-down shadow. |
| `I59:216;59:147`–`59:153` | Ready action | Same pill metrics and typography as Cancel. |
| `I59:218;59:125`–`59:134` | Join state | Preserve clipped waiting artwork. “Join” is Fredoka Regular **36px** in cyan `#4fafc1`, not 48px, with the 2px-down Figma shadow. |
| `59:219` | Footer | 208px total height with 64px vertical padding; centered action. |
| `59:220` | Start action | 360×96 orange pill; 32px horizontal/16px vertical padding; Fredoka One 48px cream “OK!” with a separate 3px-down shadow silhouette. It stays visually enabled; invalid starts are rejected by the multiplayer manager. |
| `I59:220;59:159` | Pointer | 72px art in a 102px bound, positioned beyond the lower-right button edge and oriented up-left. It has a dark shadow/outline and cream hand, and is visible only while OK is hovered or navigation-focused. |

## Comparison findings

- The current macro geometry matches the design closely and should not be rewritten.
- The current title is the correct face and nominal size, but its stroke is missing/incorrect because a single UI Toolkit outline consumes the glyph. It must use the duplicated-label outside-stroke construction above.
- The current empty-card “Join” label is 48px; Figma specifies 36px.
- The pointer uses a dark shadow, orange outside edge, and cream-white foreground; the hand itself must remain white.
- Thin prompt glyphs and the Fredoka Regular “Join” label must not inherit the shared text outline because UI Toolkit draws it into the glyph and destroys their legibility.
- The shared 2px text outline must not override the dedicated title treatment.
- Existing runtime state names and controls are functional contracts and remain unchanged.

