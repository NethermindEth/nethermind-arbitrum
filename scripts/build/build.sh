#!/bin/bash
# SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
# SPDX-License-Identifier: LGPL-3.0-only

set -e

build_config=Release
output_path=/nethermind/output

echo "Building Nethermind Arbitrum"

# Restore dependencies. Pass PublishReadyToRun so the SDK fetches R2R platform
# packs (crossgen2, runtime packs) for every RID listed in Nethermind.Runner's
# <RuntimeIdentifiers>. Staying in --locked-mode preserves reproducibility —
# platform packs are determined by the pinned SDK image, not the lock file.
dotnet restore src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj --locked-mode -p:PublishReadyToRun=true

for rid in "linux-arm64" "linux-x64" "osx-arm64" "win-x64"; do
  echo "  Publishing for $rid"

  # Build Nethermind.Runner
  dotnet publish src/Nethermind/src/Nethermind/Nethermind.Runner/Nethermind.Runner.csproj \
    -c $build_config -r $rid -o $output_path/$rid --no-restore --sc \
    -p:DebugType=embedded \
    -p:IncludeAllContentForSelfExtract=true \
    -p:PublishSingleFile=true \
    -p:SourceRevisionId=$1

  # Build Arbitrum plugin (not self-contained, will use runner's runtime).
  # The upfront --locked-mode restore already populated assets for every RID
  # listed in Nethermind.Arbitrum's <RuntimeIdentifiers>; a per-RID restore
  # here would traverse the Nethermind.Runner project reference and rewrite
  # Runner's project.assets.json with only the current RID, breaking
  # subsequent loop iterations.
  dotnet publish src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj \
    -c $build_config -r $rid -o $output_path/$rid/arbitrum-tmp --no-restore --sc false \
    -p:SourceRevisionId=$1

  # Copy plugin assemblies to plugins directory
  mkdir -p $output_path/$rid/plugins
  cp $output_path/$rid/arbitrum-tmp/Nethermind.Arbitrum.* $output_path/$rid/plugins/

  # Copy Stylus native libraries from NuGet package output.
  # `dotnet publish -r <rid>` flattens the matching RID's native assets
  # (runtimes/<rid>/native/libstylus.*) into the publish root, so the files
  # are picked up there and placed back into the runtimes/<rid>/native layout
  # the .NET host adds to the native library search path at startup.
  native_dir=$output_path/$rid/runtimes/$rid/native
  mkdir -p "$native_dir"
  shopt -s nullglob
  stylus_libs=("$output_path/$rid/arbitrum-tmp"/*stylus*)
  shopt -u nullglob
  if [ ${#stylus_libs[@]} -eq 0 ]; then
    echo "ERROR: Stylus native libraries not found for $rid"
    exit 1
  fi
  cp "${stylus_libs[@]}" "$native_dir/"

  # Copy Arbitrum configs and chainspecs
  mkdir -p $output_path/$rid/configs $output_path/$rid/chainspec
  cp -r src/Nethermind.Arbitrum/Properties/configs/* $output_path/$rid/configs/ 2>/dev/null || true
  cp -r src/Nethermind.Arbitrum/Properties/chainspec/* $output_path/$rid/chainspec/ 2>/dev/null || true

  # Clean up temporary plugin build
  rm -rf $output_path/$rid/arbitrum-tmp

  mkdir -p $output_path/$rid/keystore

  # A temporary symlink for Linux to support the old executable name
  [[ "$rid" == linux-* ]] && ln -sr $output_path/$rid/nethermind $output_path/$rid/Nethermind.Runner
done

mkdir -p $output_path/ref
find src/Nethermind/artifacts/obj -type f -path "*/$build_config/refint/*.dll" -exec cp {} "$output_path/ref" \; 2>/dev/null || true

echo "Build completed"
