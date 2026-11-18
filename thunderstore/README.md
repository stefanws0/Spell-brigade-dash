# Spell Brigade Dash

Press **Space** (or **LB** on a controller) for a quick, short dash.

- **Short hop with i-frames:** about 3 units in 0.12 s, and you can't be hit while dashing.
- **Charges:** the rounded bar under your wizard starts with 1 dash that refills in 15 s. Cards add segments and speed it up.
- **Costs HP:** each dash costs 5% of your max HP (it never takes your last point) until you find the Legendary **Ethereal Step**.
- **Violet magic:** glowing afterimages, arcane sparkles, speed streaks and a dust burst, plus a whoosh taken from the game's own sounds.
- **The bar fades out** once it's full, flashes when a segment refills, and shakes red if you press dash while it's empty.
- **Level-up cards:** the dash is an **Epic** card, and it can be upgraded with **+1 Charge** (Legendary), **Quick Recovery** (Epic), **Long Stride** (Epic), **Blazing Dash** (Legendary) and **Ethereal Step** (Legendary).

## ⚠️ Early version (0.3.0, "discovery build")

This first version finds the game's internals by name at runtime. Because of that:

- You **start every run with the dash** (`Debug.StartRunWithDash = true`), because the level-up card hook isn't wired yet.
  To test the upgrades, hold **Left Ctrl** and press **2** to toggle the dash, **3** for +1 Charge, **4** for Quick Recovery,
  **5** for Long Stride, **6** for Blazing Dash or **7** for Ethereal Step. You can rebind these under `[Debug]` in the config.
- The mod writes **`BepInEx/SpellBrigadeDash-discovery.txt`** a few seconds into a run, and again whenever you press **Left Ctrl + 1**.
  Level up a few times during a run, press Ctrl + 1 and send that file to the author. It's what the next version uses to
  wire the real level-up cards, co-op sync and Blazing Dash damage.

## Multiplayer

**Every player in the lobby should install the mod.** Dashes move your wizard like normal movement does.
Each client also spots other players' dashes from their movement and doesn't let hits land on them mid-dash, so
i-frames work for non-host players too.

## Configuration

`BepInEx/config/com.stefanws0.spellbrigadedash.cfg` is created after the first launch. You can set the distance,
duration, charges, recharge time, keybinds (Input System paths such as `<Keyboard>/space` or `<Gamepad>/leftTrigger`),
bar size, colours, position and auto-hide, effects, card rarities and stack limits, and more.

## Source

https://github.com/stefanws0/Spell-brigade-dash
