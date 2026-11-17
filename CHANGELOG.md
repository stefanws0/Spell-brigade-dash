# Changelog

## 0.3.1

- **Fixed the crash at level-up.** It came from the level-up method trace: in this game some methods have no
  machine code of their own (interfaces) and identical small methods share one body, so patching them detoured
  address 0 or several unrelated methods at once. The trace is now off by default, and if switched on it skips
  methods without their own code (checked through the method's native pointer).
- Damage hooks are pinned to `PlayerHealthContainer.TakeDamage` and `PlayerHealthContainer.RemoveHealthOnOwner`
  (the base-class hooks also ran on every enemy hit).
- The dash's HP cost retries finding the health fields (health initialises after the wizard spawns) and logs what it
  saw if it can't.
- Config migrates to v5 for these defaults.

## 0.3.0 (wired to the game, from the first discovery report)

- **The dash stops at walls and the map edge.** This build has no `Physics.SphereCastAll` and returns nonsense for the
  layer collision matrix, so dashes went through everything. Walls are now the `Obstacles` / `TargetableObstacles`
  layers (`Dash.BlockingLayers`), checked with `SphereCast` (falling back to raycasts), and the map edge is the end of
  the NavMesh (`Dash.StayOnNavMesh`).
- **Wizard detection uses the game's `GameplayPlayer` class** with its `IsOwner` flag, instead of scanning every object.
  Ghosts (downed wizards) can't dash.
- **HP cost goes through the game's health:** `HealthContainer.CurrentHealth` plus its network-synced copy
  (`SyncedCurrentHealth`), so the health bar updates and other players see it.
- **Native effects:** the game's own teleport sparkle burst and fast-running speed lines play with the dash, on top of
  the violet effects (`Effects.GameTeleportBurst`, `Effects.GameSpeedLines`).
- **Dash sound through FMOD** (the game doesn't use Unity audio): an event is picked by name, or set
  `Sound.FmodEvent`. The report lists every event.
- Co-op: the game applies damage on each player's own PC, so movement-based i-frames for other players are now off by
  default (`Multiplayer.RemoteIFrames`).
- Discovery report: wall-check self-test, FMOD event list (section 17) and the level-up shop classes (section 18).
  The method trace now focuses on the shop and is **on** for this test (`Discovery.TraceLevelUpMethods`).
- Trace safety (the start crash in 0.1.0 came from the trace): no methods on game structs, no struct-by-value
  parameters/returns, no Netcode RPC plumbing; every traced call leaves a breadcrumb, and a method the game crashed in
  is written to `BepInEx/config/SpellBrigadeDash.trace-skip.txt` on the next start and never traced again.
- The previous session's log is kept as `BepInEx/SpellBrigadeDash.prev.log`, and a clean quit is recorded, so crash
  logs survive a restart.

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
