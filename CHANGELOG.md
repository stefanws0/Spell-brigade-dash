# Changelog

## 0.4.2

- **Blazing Dash's fire now hurts enemies** (rules agreed with Stefan, 18 Nov). Enemies standing in a flame take a
  fire hit every 0.5 s, so up to three in a 1.5 s trail (`Core/BlazeTicks`, unit-tested; one timer per enemy, so
  overlapping trails never stack). Base damage per tick = the game's own Trail spell's base damage (33.81), read live
  from its `SpellConfiguration`; scaled by your character's Damage % (`Core/BlazeMath.StatMultiplier`, unit-tested);
  crits rolled by the game; the game's fire burn follows (3 burns of 0.33 x the hit). First test (16:37 run): hits
  landed (33.8 each, a Flesh Crab burnt to 0), but `PlayerStats.GetSpellValue(Trail, ...)` throws for a wizard
  without a Trail, so Damage %, Spell Size and crits were missing: they now come from your character stats, and hits
  are credited to your Trail if you own one, else to your first spell (whose damage counter then includes them). The hit
  goes through the game's own `Hitbox.Hit` with a `PlayerSpellDamageInfo` (`Game/BlazeDamage`), on enemies only
  (`Enemy.AllEnemies`), never on you or teammates. No patches. Spell Size widens the flames and their reach.
  `Upgrades.BlazingDashDamage` is now 0 = "same as the Trail spell" (config v7 moves an untouched 15).
- **Discovery report section 19**: the game's damage numbers (every spell's base stats, fire burn, lava zone, meteor
  area, enemy health) and an "Enemy health now" log line each minute while enemies are around (`Discovery/DamageReference`).

## 0.4.1

- **Balance: the dash recharges in 15 s per charge** (was 12; Stefan, 18 Nov). Fully upgraded (3x Quick Recovery):
  8.6 s instead of 6.9 s. Config v6 moves an untouched `Dash.RechargeSeconds = 12` to 15; a custom value is kept.
- **Each dash card is its own card in the shop.** The game decides "same card" (one per shop, bans) by a card's
  stat, and all dash cards shared one, so a shop showed at most one dash card and banning one banned them all. Each
  card now has its own carrier stats (`Core/ShopCarriers`, unit-tested): a stat without a real card between shops, and
  one the game's analytics accept while a shop is open. Both carry value 0, so the game's own stats never change.
  `Upgrades.ShopCardStat` is gone.
- **Own card icons.** Cards use `art/icons/<card>.png` (built into the DLL) or an override in
  `BepInEx/plugins/SpellBrigadeDash/icons/`, and fall back to the game's Dodge icon. First icon: Dash. New icons are
  prepared with `tools/icons/make_icon.py` (round, transparent outside, 256x256).
- Fixed: dying and being revived wiped your dash cards. The game spawns a new wizard object on a revive, and the mod
  treated every new wizard as a new run. Cards now reset only when the game's own run changes (`RunData.RunId`,
  `Game/GameRun`, ported from the PC session's pc-backup branch).
- **Dash stats in the game's stats list** (Escape screen and the hold-to-show stats panel): Dash Charges, Dash
  Recharge, Dash Distance, Dash HP Cost ("None" after Ethereal Step) and, once picked, Blazing Dash, each with its
  card's icon. The list is a fixed set of game-stat rows, so the mod adds copies of its last row with the title's
  localizer switched off and its own text (`Game/StatPanelRows`, values from `Core/DashStatRows`, unit-tested; no
  patches). Hidden while the dash is locked. `Upgrades.StatsPanelRows` switches them off. The list's box has a fixed
  height with room for about two extra rows, so the gap between all rows is tightened just enough to fit
  (`Core/ListFit`, unit-tested); the game's own gap comes back when the dash rows are hidden. Value changes are logged.
  First try (fitting to the list's own box) failed: that box runs ~250 px past the visible frame, off the bottom of
  the screen. Measured on screen (18 Nov), the in-game frame holds exactly 15 rows at the game's spacing, so the rows
  are now fitted into that (`ListFit.DesignedRoom`), less half a row so the last row keeps some space above the
  frame's bottom edge (Stefan asked for it after seeing the 17 rows fit). Rows then sit 16-21% closer together. Each panel logs its real
  on-screen layout once ("measured on screen"), including the rest of its canvas. Escape menu: dash rows not seen
  until 18 Nov 16:06, then verified: they are on the Escape menu's Player page (its round wizard-hat button opens the
  list; the menu's front page has no stats list, which is why they seemed missing). The log says when the Escape menu
  opens, what it shows, and when each stats list comes on or off screen.
- Fixed: afterimages copied every mesh on the wizard, including its spells (the Sun Mage's whole Solar Flare circle)
  and effect meshes. They now copy only the character model (the parent of the main skinned renderer).
- Fixed: the wizard's "body" was the biggest mesh on it, which is the Solar Flare circle when the Sun Mage spawns with
  it out (18 Nov: afterimages copied 'Proximity(Clone)' again, and the dash effects were sized to the flare). The body
  is now the animated (skinned) model, never anything under `PlayerSpells` (`Core/BodyPick`, unit-tested); the log's
  "Local wizard" line names it.

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
