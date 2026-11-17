# Changelog

## 0.2.0 (after the first playtest)

- **Dash key is now Space** (Shift also opened the game's stats panel). Rebindable as before.
- **Balance:** the dash starts with 1 charge that refills in 12 s (was 2 charges / 2 s). Each dash also costs
  5% of max HP (never your last point) until the new Legendary card **Ethereal Step** removes the cost (Ctrl + 7).
- **Effects, now violet:** colour-shifting glowing afterimages, arcane sparkles along the dash plus a landing burst,
  speed streaks, and a bigger dust burst lifted off the ground so it's actually visible.
- **Blazing Dash fire redone:** upright, flickering two-layer flames with a hot core and rising embers, instead of
  orange blobs. Sparkles, streaks and flame cores glow when the game ships an additive shader.
- **Rounded bar** (pill-shaped segments) in violet, optionally drawn with one of the game's own sprites
  (`Bar.GameSpriteName`).
- Discovery report: new sections listing the game's UI bars (sprites and colours) and the health fields on your wizard.
- Existing config files are updated automatically (`Internal.ConfigVersion`), so the new defaults apply.

## 0.1.0 (discovery build)

- Shift / LB dash in the movement direction (or the facing direction when standing still), with collision against walls.
- Charge-based energy: 2 segments that refill one at a time (2 s each), shown in a bar under your wizard
  with auto-hide, a refill flash and an empty-press shake.
- I-frames during the dash (plus 0.08 s grace), using auto-detected damage hooks.
- Effects: afterimage trail, dust puff at start and landing, and a whoosh reused from the game's sounds.
- Co-op: other players' dashes are detected from their movement, so they get i-frames and afterimages.
- Card system (Dash, +1 Charge, Quick Recovery, Long Stride, Blazing Dash) with configurable rarities and stack
  limits. It is applied with the debug hotkeys (Left Ctrl + 2..6, rebindable) until the level-up hook is wired.
- Discovery report (`BepInEx/SpellBrigadeDash-discovery.txt`, Left Ctrl + 1) and an opt-in level-up method trace
  (`Discovery.TraceLevelUpMethods`, experimental).
- Crash safety: a line-flushed `BepInEx/SpellBrigadeDash.log` with step breadcrumbs, Harmony patches installed a few
  seconds into a run instead of at game start, and `Debug.SafeMode` to switch off all patches.
