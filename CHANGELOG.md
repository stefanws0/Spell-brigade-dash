# Changelog

## 0.4.0 (shop test build, verified in game 17 Nov)

- **The dash cards are in the game's own level-up shop.** They're added as character cards to the game's item list
  (`StoreItemDatabase.characterImprovementItems`), only while they're available (the Dash card while the dash is
  locked, the upgrades after it), with their titles and descriptions added to the game's string table. Picking one
  is seen by a single postfix on `StoreManager.ChooseShopItem` and applies the card. The cards carry the stat
  `HealOnDodge` with value 0, so the game's own stat change does nothing (`Upgrades.ShopCards`, `Upgrades.ShopCardStat`).
- **Test mode (on in this build):** `Debug.ShopCardTestMode` makes the dash cards Common and much more likely, so they
  show up at the first level-up.
- Fixed after the first shop test: picking any card while a dash card was in the shop threw inside the game's
  `ChooseShopItem` (its analytics parse every card's stat as `ImprovementStat`, which has no `HealOnDodge`), so the
  pick never reached the mod. While a shop is open the dash cards now carry `Dodge` + 0 (a no-op), the pick hook is a
  prefix that applies the card before the game's own code, and it's installed as soon as the shop exists (the first
  shop opens ~1.5 s into a run, before the other hooks).
- Every new Harmony patch now goes through a stricter guard (`NativeCode.CanPatch`, from the 17 Nov PC session): no
  abstract/interface methods, no leaf or tiny functions, no code shared with another method.
- The log and the discovery report list the game's own character cards and every shop that opens.
- Known limitation: the game treats all dash cards as one card (they share the carrier stat), so a shop shows at most
  one dash card, and banning one bans them all for the run.

## 0.3.2

- **Blazing Dash burns with the game's own fire.** The trail is now made of the fire the game shows on a burning wizard
  (`FireVisualizer` / VFX Graph `Effect_Fire`), lit and put out with the game's own `PlayEffect` / `StopEffect`, so it
  scales in, burns and dies out like in-game fire. The fires are pooled clones (no Harmony patches). New settings:
  `Effects.GameFireTrail` (off = the old sprite flames), `Effects.GameFireScale`, `Effects.GameFireTheme`
  (`Fire` / `FireDouble`).
- Blazing Dash also plays the game's fire-spell sound (`Sound.BlazingFmodEvent`,
  `event:/Elements/Fire/SFX_Element_Fire_Cast`).
- **Fixed: the dash's HP cost was never applied.** Two bugs: `HealthContainer.CurrentHealth` is read-only in this game
  (it reads the network-synced `SyncedCurrentHealth`) and the health finder skipped read-only members; and the new HP
  was boxed as a `double` for a `float` member, which reflection refuses. It now writes `SyncedCurrentHealth` with the
  member's exact type and logs the first cost with the value the game reads back.

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
