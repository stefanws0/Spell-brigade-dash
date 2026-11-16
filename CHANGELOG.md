# Changelog

## 0.1.0 (discovery build)

- Shift / LB dash in the movement direction (or the facing direction when standing still), with collision against walls.
- Charge-based energy: 2 segments that refill one at a time (2 s each), shown in a bar under your wizard
  with auto-hide, a refill flash and an empty-press shake.
- I-frames during the dash (plus 0.08 s grace), using auto-detected damage hooks.
- Effects: afterimage trail, dust puff at start and landing, and a whoosh reused from the game's sounds.
- Co-op: other players' dashes are detected from their movement, so they get i-frames and afterimages.
- Card system (Dash, +1 Charge, Quick Recovery, Long Stride, Blazing Dash) with configurable rarities and stack
  limits. It is applied with the debug hotkeys F6 to F10 until the level-up hook is wired.
- Discovery report (`BepInEx/SpellBrigadeDash-discovery.txt`) and a level-up method trace.
