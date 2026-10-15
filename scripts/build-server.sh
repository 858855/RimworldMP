#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_bin="${DOTNET_BIN:-dotnet}"
"$dotnet_bin" publish "$project_root/src/Multiplayer.Foundation.Server/Multiplayer.Foundation.Server.csproj" \
  -c Release -o "$project_root/dist/server" --nologo
cp "$project_root/deployment/serverconfig.example.json" "$project_root/dist/server/serverconfig.json"
echo "Server: $project_root/dist/server"
