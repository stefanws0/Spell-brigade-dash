# Dash

A mod for The Spell Brigade that gives your wizard a short dash. Press Space, or LB on a controller.

Runs start without it. Grab the Dash card in the level-up shop, and after that you can find upgrades for it: more
charges, a faster recharge, a longer dash, a fire trail, and one that removes the HP cost. The full list is in
[thunderstore/README.md](thunderstore/README.md).

Install it from Thunderstore with r2modman or Gale:
[StrayingFox/Dash](https://thunderstore.io/c/the-spell-brigade/p/StrayingFox/Dash/).

## Building

You need the .NET SDK and the game with BepInExPack IL2CPP, started once so BepInEx has made its `interop` folder.
If you don't use r2modman's Default profile, copy `GamePaths.props.user.example` to `GamePaths.props.user` and set
`BepInExDir` to your BepInEx folder. Then, with the game closed:

```bash
dotnet build src/SpellBrigadeDash -c Release -p:DeployToGame=true
```

Tests: `dotnet test tests/SpellBrigadeDash.Tests`. Thunderstore zip: `dotnet build src/SpellBrigadeDash -c Release -t:Package`.

## My other mods

- [Minimap](https://github.com/stefanws0/Spell-Bridge-Minimap)
- [Mouse Ping](https://github.com/stefanws0/Spell-brigade-mouse-ping)

MIT licensed.
