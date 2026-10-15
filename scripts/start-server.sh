#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_bin="${DOTNET_BIN:-dotnet}"
config_path="${1:-$project_root/dist/server/serverconfig.json}"
exec "$dotnet_bin" "$project_root/dist/server/Multiplayer.Foundation.Server.dll" "$config_path"
