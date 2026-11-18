# Spell Brigade Dash

Press **Space** (or **LB** on a controller) for a quick, short dash.

- **Short hop with i-frames:** about 3 units in 0.12 s, and you can't be hit while dashing.
- **Charges:** the rounded bar under your wizard starts with 1 dash that refills in 15 s. Cards add segments and speed it up.
- **Costs HP:** each dash costs 5% of your max HP (it never takes your last point) until you find **Ethereal Step**.
- **Violet magic:** glowing afterimages, arcane sparkles, speed streaks and a dust burst, plus the game's own teleport
  burst, speed lines and a whoosh from its sounds.
- **The bar fades out** once it's full, flashes when a segment refills, and shakes red if you press dash while it's empty.

## Getting the dash

Every run starts **without** the dash. Find the **Dash** card (Epic) in the level-up shop; it turns up more often than
other Epic cards. Once you have it, its upgrades can appear, now and then, like any other card:

| Card | Rarity | Effect |
|---|---|---|
| **+1 Dash Charge** | Legendary | Another segment on the bar (up to 2 times) |
| **Quick Recovery** | Epic | Charges refill 25% faster (up to 3 times) |
| **Long Stride** | Epic | Dash 30% farther (up to 2 times) |
| **Blazing Dash** | Legendary | Leaves the game's own fire behind you. Enemies standing in it take a fire hit every 0.5 s, as strong as the Trail spell's, boosted by your Damage % and crits, and they keep burning. Spell Size makes the flames bigger |
| **Ethereal Step** | Legendary | Dashing no longer costs HP |

Your dash stats show in the game's stats list (hold Shift, or press Escape and pick the wizard button).

## Multiplayer

**Every player in the lobby should install the mod.** Dashes move your wizard like normal movement does. Each client
also spots other players' dashes from their movement and doesn't let hits land on them mid-dash.
Co-op hasn't been tested yet: please report what you find.

## Configuration

`BepInEx/config/com.stefanws0.spellbrigadedash.cfg` is created after the first launch. You can set the distance,
duration, charges, recharge time, HP cost, keybinds (Input System paths such as `<Keyboard>/space` or
`<Gamepad>/leftTrigger`), bar size, colours and position, effects, card rarities, how often the Dash card shows up,
stack limits, and more. `[Debug]` has test aids (start every run with the dash, hotkeys that grant cards), all off by default.

## If something goes wrong

- The mod logs to `BepInEx/SpellBrigadeDash.log`. Turn on `[Discovery] Enabled` for more detail.
- If the game crashes with the mod, set `[Debug] SafeMode = true`: no game patches at all (no i-frames), only the dash itself.
