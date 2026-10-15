#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_bin="${DOTNET_BIN:-dotnet}"
rimworld_managed="${RIMWORLD_MANAGED:-/mnt/Spiele_1/SteamLibrary/steamapps/common/RimWorld/RimWorldLinux_Data/Managed}"
if [[ ! -f "$rimworld_managed/Assembly-CSharp.dll" ]]; then
  echo "RIMWORLD_MANAGED must point to installed RimWorld 1.6 Managed assemblies" >&2
  exit 1
fi
"$dotnet_bin" build "$project_root/src/Multiplayer.Foundation.RimWorld/Multiplayer.Foundation.RimWorld.csproj" \
  -c Release --nologo "-p:RimWorldManaged=$rimworld_managed"
output_dir="$project_root/dist/RimWorldMultiplayerFoundation"
mkdir -p "$output_dir/About" "$output_dir/Assemblies"
cp "$project_root/mod/About/About.xml" "$output_dir/About/About.xml"
for assembly in Multiplayer.Foundation.Protocol Multiplayer.Foundation.Network Multiplayer.Foundation.RimWorld; do
  source_file="$project_root/src/$assembly/bin/Release/$(if [[ "$assembly" == Multiplayer.Foundation.RimWorld ]]; then echo netstandard2.1; else echo netstandard2.0; fi)/$assembly.dll"
  cp "$source_file" "$output_dir/Assemblies/$assembly.dll"
done
echo "Installable mod: $output_dir"
