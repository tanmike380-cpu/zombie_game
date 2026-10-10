#!/usr/bin/env bash
set -euo pipefail

# CPU regression only: no Unity GUI, asset import, source snapshot or game-state mutation.
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
build_directory="$repository_root/Builds/BalanceTests"
dotnet_path="${ZOMBIE_DOTNET_PATH:-}"
if [[ -z "$dotnet_path" ]]; then
    dotnet_path="$(command -v dotnet || true)"
fi
if [[ -z "$dotnet_path" ]]; then
    dotnet_path="/Applications/Unity/Hub/Editor/6000.6.1f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet"
fi
if [[ ! -x "$dotnet_path" ]]; then
    echo "Set ZOMBIE_DOTNET_PATH to a .NET 8 SDK dotnet executable; no SDK was found." >&2
    exit 1
fi

export DOTNET_CLI_HOME="$build_directory/cli"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
"$dotnet_path" build "$repository_root/tools/balance/RankBalanceTests.csproj" \
    --ignore-failed-sources \
    --property:NuGetAudit=false \
    --property:BaseIntermediateOutputPath="$build_directory/obj/" \
    --property:BaseOutputPath="$build_directory/bin/"
"$dotnet_path" "$build_directory/bin/Debug/net8.0/RankBalanceTests.dll" "$repository_root"
