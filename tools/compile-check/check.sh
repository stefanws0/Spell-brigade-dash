#!/usr/bin/env bash
# Compile-checks the mod WITHOUT the game installed, against hand-written stand-ins for the
# game's interop assemblies (stubs/) plus the real BepInEx 6 IL2CPP core DLLs from GitHub.
# It catches C# mistakes; it cannot prove the code matches the real game's API.
# The real build is `dotnet build` on a PC where the game + BepInEx are installed (see README).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
CACHE="$HERE/.cache"
BEP="$CACHE/BepInEx"
BEPINEX_ZIP_URL="https://github.com/BepInEx/BepInEx/releases/download/v6.0.0-pre.2/BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip"

if [ ! -f "$BEP/core/BepInEx.Unity.IL2CPP.dll" ]; then
  echo "Downloading BepInEx IL2CPP core DLLs..."
  mkdir -p "$CACHE/dl"
  curl -sSL -o "$CACHE/dl/bepinex.zip" "$BEPINEX_ZIP_URL"
  rm -rf "$CACHE/dl/x" && mkdir -p "$CACHE/dl/x"
  unzip -q -o "$CACHE/dl/bepinex.zip" -d "$CACHE/dl/x"
  mkdir -p "$BEP/core"
  cp "$CACHE/dl/x/BepInEx/core/"*.dll "$BEP/core/"
fi

mkdir -p "$BEP/interop"
dotnet build "$HERE/stubs/UnityEngine.CoreModule/UnityEngine.CoreModule.csproj" -c Release \
  -p:BepInExCore="$BEP/core" -o "$CACHE/stubs-out" -nologo -v q
dotnet build "$HERE/stubs/Assembly-CSharp/Assembly-CSharp.csproj" -c Release -o "$CACHE/stubs-out" -nologo -v q
cp "$CACHE/stubs-out/UnityEngine.CoreModule.dll" "$CACHE/stubs-out/Il2Cppmscorlib.dll" "$CACHE/stubs-out/Assembly-CSharp.dll" "$BEP/interop/"

# Output goes to .cache/mod-out: a DLL built against stubs does NOT run in the real game.
dotnet build "$ROOT/src/SpellBrigadeDash/SpellBrigadeDash.csproj" -c Release -p:BepInExDir="$BEP" \
  -p:BaseIntermediateOutputPath="$CACHE/mod-obj/" -o "$CACHE/mod-out" -nologo "$@"

dotnet test "$ROOT/tests/SpellBrigadeDash.Tests" -nologo
