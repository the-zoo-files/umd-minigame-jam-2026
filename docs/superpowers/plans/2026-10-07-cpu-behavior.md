# CPU behavior and difficulty pass

Approved scope: delayed awareness, committed targets, stable decision errors, smoother
steering/braking, four tunable presets with Noob as the default, and repeatable balance checks.
Keep existing names/UI and shared movement, collection, scoring, and input rules.

1. Add focused Play Mode checks for delayed discovery, target commitment, invalidation,
   movement continuity, bounded steering, and the lobby default. Observe failures first.
2. Extend CpuSettings with awareness radius, commitment time, switch advantage, acceleration,
   braking distance, and throw preparation. Retune all four profiles. Store a stable bias per
   observed flask; expire unavailable/out-of-range observations. Reaction becomes discovery
   latency, not a pause every time a valid target changes. Continue movement while deciding.
3. Smooth CPU command vectors, brake near the final waypoint, keep collision recovery and
   immediate round-end stops. All levels stage toward the arena when nothing is noticed.
4. Add a seeded benchmark rotating difficulty through all four spawn slots. Record deliveries,
   final scores/penalties, idle time, and target switches. It measures behavior, not human skill.
5. Run focused and existing CPU, gameplay, connection, and results suites; inspect Console and
   leave Edit Mode. Update gameplay/validation docs and obtain a fresh review.

Ruling: retain the Unity-connected checkout on codex/round-results after checkpoint 75a1377.
No new commit or push is implied for this CPU pass. Keep progress under ignored .utmp.

Integration update: friend commit 91e3490 softened Noob/Pro/Hacker and retained God's five
original cognitive values. Preserve those values alongside the new awareness/commitment/steering
controls. Results PR was merged as 74db31d; continue on codex/cpu-behavior from that base.
The previous benchmark predates this combined tuning and must be rerun.

## Final validation on the merged base

- Focused CPU behavior, full CPU, gameplay, connection-menu, and round-results suites: PASS.
- Strengthened checks cover known alternatives during commitment, eventual switching,
  pooled reuse and pickup/rethrow between reads, rediscovery delay, and immediate round-end stop.
- Eight 20-second benchmark rounds (two seeded layouts, four spawn rotations): PASS.
  Mean delivered points: Noob 3.5, Pro 7.25, Hacker 7.75, God 10.625.
  Mean final scores: Noob 2.125, Pro 6.125, Hacker 6.625, God 9.375.
  These are short-match throughput samples, not a human difficulty guarantee; Pro/Hacker overlap
  merits human playtesting. The reproducible harness writes `.utmp/cpu-balance.csv`.
- Short-round benchmark guard previously passed all eight rotations at a 0.25-second timeout.
- Final Console: zero errors/warnings; Editor outside Play Mode and not compiling.
  Navigation dependency hash matches the current scene. No unresolved Git conflicts.
- Changes remain uncommitted on codex/cpu-behavior; nothing pushed. Recovery stash retained.
