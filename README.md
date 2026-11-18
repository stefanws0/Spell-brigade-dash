# Spell Brigade Dash

A BepInEx mod for **The Spell Brigade**. It adds a short, animated **dash on Space / LB** with a
**segmented energy bar** under your wizard. The dash is unlocked and upgraded through rare level-up cards.

All design decisions are in [`docs/DESIGN.md`](docs/DESIGN.md).

| | |
|---|---|
| Dash | ~3 units in 0.12 s in your movement direction, i-frames during the dash, stops at walls |
| Energy | 1 charge that refills in 15 s (more with cards). Rounded violet bar: fades out when full, flashes on refill, shakes on an empty press |
| Cost | Each dash costs 5% of max HP (never your last point) until the Legendary **Ethereal Step** card |
| Effects | Violet glowing afterimages, arcane sparkles, speed streaks, dust burst, a whoosh reused from the game's own sounds; Blazing Dash leaves flickering flames with embers |
| Cards | **Dash** (Epic, unlock) · **+1 Charge** (Legendary) · **Quick Recovery** (Epic) · **Long Stride** (Epic) · **Blazing Dash** (Legendary) · **Ethereal Step** (Legendary, no HP cost) |
| Co-op | Everyone installs it. Other players' dashes are detected from movement, so they get i-frames too |

## Status: playable, before release (0.4.3)

The dash cards are in the game's own level-up shop at their real rarities, and **every run starts without the dash**:
pick the **Dash** card first, then the upgrades show up. Blazing Dash's fire damages enemies like a fire spell. Still
to do before a release: a co-op check, polish, and switching the discovery report and debug hotkeys off by default.

The mod still writes `BepInEx/SpellBrigadeDash-discovery.txt` a few seconds into each run (and on **Left Ctrl + 1**):
what it found in the game, including the game's damage numbers. Debug hotkeys stand in for picking cards while
testing. Hold **Left Ctrl** and press:

| Key | Action |
|---|---|
| Ctrl + 1 | Write the discovery report now |
| Ctrl + 2 | Lock / unlock the dash |
| Ctrl + 3 | +1 Charge |
| Ctrl + 4 | Quick Recovery |
| Ctrl + 5 | Long Stride |
| Ctrl + 6 | Blazing Dash |
| Ctrl + 7 | Ethereal Step (dashes stop costing HP) |

All of these can be rebound under `[Debug]` in the config (e.g. `<Keyboard>/k`, `<Keyboard>/numpad1`).

## Quick start (Windows)

IL2CPP mods compile against assemblies that BepInEx generates from *your* game install, so you build it once on
your PC.

**Downloads**

| What | Link |
|---|---|
| This mod's source (zip of the branch) | https://github.com/stefanws0/Spell-brigade-dash/archive/refs/heads/claude/cool-mccarthy-dscfuz.zip |
| .NET 8 SDK (x64 installer) | https://dotnet.microsoft.com/download/dotnet/8.0 |
| Mod manager: r2modman | https://thunderstore.io/c/the-spell-brigade/p/ebkr/r2modman/ |
| Mod manager: Gale (alternative) | https://thunderstore.io/c/the-spell-brigade/p/Kesomannen/GaleModManager/ |
| BepInExPack IL2CPP (the mod loader) | https://thunderstore.io/c/the-spell-brigade/p/BepInEx/BepInExPack_IL2CPP/ |

**Steps**

1. Install the **.NET 8 SDK**.
2. Install **r2modman** (or Gale), select *The Spell Brigade*, create a profile and install **BepInExPack IL2CPP** in it.
3. Click **Start modded** once and wait until you reach the main menu. BepInEx generates `BepInEx/interop/`
   on that first start, which can take a minute. Then close the game.
4. Find your profile's BepInEx folder: r2modman → *Settings* → *Browse profile folder* (Gale: *Open profile folder*).
5. Unzip this repo. The build finds BepInEx by itself if the repo sits **inside** that BepInEx folder, or if
   you use r2modman's *Default* profile. Otherwise copy `GamePaths.props.user.example` to `GamePaths.props.user` and
   set `BepInExDir` to that folder (it must contain `core` and `interop`). The build prints `Using BepInEx at: ...`.
6. In a terminal in the repo folder, build and install straight into the profile:

```bash
dotnet build src/SpellBrigadeDash -c Release -p:DeployToGame=true
```

7. **Start modded** again. The BepInEx console shows `SpellBrigadeDash 0.3.0 loaded`. Start a run and press Space.

Other build commands:

```bash
dotnet build src/SpellBrigadeDash -c Release
```

```bash
dotnet build src/SpellBrigadeDash -c Release -t:Package
```

The first builds `src/SpellBrigadeDash/bin/Release/SpellBrigadeDash.dll`. The second builds `dist/SpellBrigadeDash-0.3.0.zip`, which you can import
in r2modman/Gale (*Settings → Import local mod*) or upload to Thunderstore.

**Troubleshooting**

- **The game crashes:** before relaunching (that overwrites the logs), send `BepInEx/SpellBrigadeDash.log`. It's
  flushed line by line, so its last `STEP` line names what the mod was doing when the game died. Also send
  `BepInEx/LogOutput.log`. To narrow it down, set `SafeMode = true` under `[Debug]` in the config: that turns off
  all Harmony patches and leaves only the dash, bar and effects.

- `CS0656 Missing compiler required member 'System.Runtime.CompilerServices.NullableAttribute..ctor'`: fixed in
  `CompilerAttributes.cs`. Make sure you have the latest source.
- `cannot convert from 'SpellBrigadeDash.Game.PlayerInfo' to 'PlayerInfo'` (or similar clashes with game class
  names): fixed by keeping `using` directives inside namespaces. Make sure you have the latest source.
- `BepInEx not found` / `No interop assemblies`: check the `Using BepInEx at:` line, and start the game once with
  BepInEx so `interop/` gets generated.

**Files you'll need**

| File (inside the profile's BepInEx folder) | What |
|---|---|
| `SpellBrigadeDash-discovery.txt` | The discovery report to send back |
| `LogOutput.log` | BepInEx log, useful if something breaks |
| `config/com.stefanws0.spellbrigadedash.cfg` | All settings (exists after the first start with the mod) |

### Tests and compile-check (no game needed)

The pure dash logic (charges, cards, tuning, co-op dash detection) has unit tests:

```bash
dotnet test tests/SpellBrigadeDash.Tests
```

`tools/compile-check/check.sh` builds the mod against hand-written stand-ins for the game's interop assemblies
(Linux/macOS/WSL, needs `curl` and `unzip`). It catches C# mistakes but can't prove the code matches the real game.

```bash
tools/compile-check/check.sh
```

## Configuration

After the first launch, edit `BepInEx/config/com.stefanws0.spellbrigadedash.cfg` (with the game closed). Sections:

- **Dash**: distance, duration, charges, recharge, HP cost, i-frame grace, pass through enemies, input buffer
- **Controls**: `<Keyboard>/space`, `<Gamepad>/leftShoulder` (any Input System control path), plus legacy KeyCodes. Not Shift: the game uses it for the stats panel
- **Bar**: size, offset below the feet, colours, rounded shape or one of the game's own sprites, auto-hide delay, sorting order
- **Effects**: afterimages (interval, lifetime, start/fade colour), sparkles, speed streaks, dust, bar feedback, other players' afterimages
- **Sound**: on/off, exact clip name or name hints, volume
- **Upgrades**: rarity, stack limit and strength of every card
- **Multiplayer**: protect other players while they dash, detection threshold
- **GameHooks**: manual overrides for the player class, local-player / dead flags and damage methods
- **Discovery / Debug**: report, method trace, start with the dash, hotkeys

## Project layout

```
src/SpellBrigadeDash/      the plugin (C#, BepInEx 6 IL2CPP)
  Core/                    pure logic: charges, cards, tuning, remote-dash detection (unit tested)
  Game/                    talking to the game: player finder, damage hooks, sound, card/net bridges
  Controls/ Motion/ Fx/    input, collision-aware movement, bar + effects
  Discovery/               discovery report + level-up method tracer
tests/                     xUnit tests for Core/
thunderstore/              manifest.json, icon.png, store README
tools/compile-check/       stub-based compile check
docs/DESIGN.md             decisions and architecture
```
