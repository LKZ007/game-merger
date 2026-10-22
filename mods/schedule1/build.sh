#!/bin/sh
# Builds Frankenstein.dll (the Schedule I item menu and story mode) with Mono's C# compiler.
# References: S1API.Forked, LavaGang.MelonLoader and UnityEngine.Modules from NuGet, unpacked into $REFS.
#   REFS/S1API.dll  REFS/MelonLoader.dll (lib/net35)  REFS/UnityEngine.*.dll (lib/netstandard2.0)
set -e
cd "$(dirname "$0")"
API=/usr/lib/mono/4.7.2-api
mcs -target:library -langversion:latest -nostdlib -optimize $DEFINE -out:../../gamemerger/mods/schedule1/Frankenstein.dll \
  -r:$API/Facades/netstandard.dll -r:$API/mscorlib.dll -r:$API/System.Core.dll -r:$API/System.dll \
  -r:"$REFS/S1API.dll" -r:"$REFS/MelonLoader.dll" -r:"$REFS/UnityEngine.CoreModule.dll" \
  -r:"$REFS/UnityEngine.IMGUIModule.dll" -r:"$REFS/UnityEngine.InputLegacyModule.dll" *.cs
