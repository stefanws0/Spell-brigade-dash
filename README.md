# Spell Brigade Dash

A BepInEx mod for **The Spell Brigade**. It adds a short, animated **dash on Left Shift / LB** with a
**segmented energy bar** under your wizard. The dash is unlocked and upgraded through rare level-up cards.

All design decisions are in [`docs/DESIGN.md`](docs/DESIGN.md).

| | |
|---|---|
| Dash | ~3 units in 0.12 s in your movement direction, i-frames during the dash, stops at walls |
| Energy | 2 charges, each refills separately (~2 s). The bar fades out when full, flashes on refill and shakes on an empty press |
| Effects | Afterimage trail, dust puff, a whoosh reused from the game's own sounds |
| Cards | **Dash** (Epic, unlock) · **+1 Charge** (Legendary) · **Quick Recovery** (Epic) · **Long Stride** (Epic) · **Blazing Dash** (Legendary) |
| Co-op | Everyone installs it. Other players' dashes are detected from movement, so they get i-frames too |

## Status: 0.1.0 is a *discovery build*

The Spell Brigade is an IL2CPP game. Its code can only be inspected through files BepInEx generates on a PC with the
game installed, and those weren't available while writing this mod. So this version **finds the game's player,
damage and level-up code at runtime by name** and writes a report:

1. Play a run with the mod installed. A few seconds in, it writes `BepInEx/SpellBrigadeDash-discovery.txt`.
2. **Level up a few times** (the mod traces the game's level-up methods), dash into some enemies, then press
   **Left Ctrl + 1** to refresh the report.
3. Send the report back. The next version uses it to wire the real level-up cards, the co-op sync message
   and Blazing Dash damage. See "Pending after discovery" in `docs/DESIGN.md`.

Until then, **you start each run with the dash**, and debug hotkeys stand in for picking cards.
Hold **Left Ctrl** and press:

| Key | Action |
|---|---|
| Ctrl + 1 | Write the discovery report now |
| Ctrl + 2 | Lock / unlock the dash |
| Ctrl + 3 | +1 Charge |
| Ctrl + 4 | Quick Recovery |
| Ctrl + 5 | Long Stride |
| Ctrl + 6 | Blazing Dash |

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
5. Unzip this repo, copy `GamePaths.props.user.example` to `GamePaths.props.user`, and set `BepInExDir` to that
   folder (it must contain `core` and `interop`).
6. In a terminal in the repo folder, build and install straight into the profile:

```bash
dotnet build src/SpellBrigadeDash -c Release -p:DeployToGame=true
```

7. **Start modded** again. The BepInEx console shows `SpellBrigadeDash 0.1.0 loaded`. Start a run and press Shift.

Other build commands:

```bash
dotnet build src/SpellBrigadeDash -c Release
```

```bash
dotnet build src/SpellBrigadeDash -c Release -t:Package
```

The first builds `src/SpellBrigadeDash/bin/Release/SpellBrigadeDash.dll`. The second builds `dist/SpellBrigadeDash-0.1.0.zip`, which you can import
in r2modman/Gale (*Settings → Import local mod*) or upload to Thunderstore.

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

- **Dash**: distance, duration, charges, recharge, i-frame grace, pass through enemies, input buffer
- **Controls**: `<Keyboard>/leftShift`, `<Gamepad>/leftShoulder` (any Input System control path), plus legacy KeyCodes
- **Bar**: size, offset below the feet, colours, auto-hide delay, sorting order
- **Effects**: afterimages (interval, lifetime, colour), dust puffs, bar feedback, other players' afterimages
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
