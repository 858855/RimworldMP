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
mkdir -p "$output_dir/About" "$output_dir/Assemblies" "$output_dir/THIRD_PARTY_LICENSES"
cp "$project_root/mod/About/About.xml" "$output_dir/About/About.xml"
for assembly in Multiplayer.Foundation.Protocol Multiplayer.Foundation.Network Multiplayer.Foundation.Lobby Multiplayer.Foundation.Session Multiplayer.Foundation.Hosting Multiplayer.Foundation.Api Multiplayer.Foundation.RimWorld; do
  source_file="$project_root/src/$assembly/bin/Release/$(if [[ "$assembly" == Multiplayer.Foundation.RimWorld || "$assembly" == Multiplayer.Foundation.Hosting || "$assembly" == Multiplayer.Foundation.Api ]]; then echo netstandard2.1; else echo netstandard2.0; fi)/$assembly.dll"
  cp "$source_file" "$output_dir/Assemblies/$assembly.dll"
done
harmony_package_dir="${NUGET_PACKAGES:-${HOME}/.nuget/packages}/lib.harmony/2.4.3"
if [[ ! -f "$harmony_package_dir/lib/net472/0Harmony.dll" || ! -f "$harmony_package_dir/LICENSE" ]]; then
  echo "Restore pinned Lib.Harmony 2.4.3 before packaging the mod" >&2
  exit 1
fi
cp "$harmony_package_dir/lib/net472/0Harmony.dll" "$output_dir/Assemblies/0Harmony.dll"
cp "$harmony_package_dir/LICENSE" "$output_dir/THIRD_PARTY_LICENSES/Harmony-MIT.txt"
echo "Installable mod: $output_dir"
