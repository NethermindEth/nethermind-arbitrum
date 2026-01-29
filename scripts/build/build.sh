#!/bin/bash
# SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
# SPDX-License-Identifier: LGPL-3.0-only

set -e

build_config=release
output_path=/nethermind/output

echo "Building Nethermind Arbitrum"

# Restore dependencies
dotnet restore src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj --locked-mode

for rid in "linux-arm64" "linux-x64" "osx-arm64" "osx-x64" "win-x64"; do
  echo "  Publishing for $rid"

  # Build Nethermind.Runner
  dotnet publish src/Nethermind/src/Nethermind/Nethermind.Runner/Nethermind.Runner.csproj \
    -c $build_config -r $rid -o $output_path/$rid --no-restore --sc \
    -p:DebugType=embedded \
    -p:IncludeAllContentForSelfExtract=true \
    -p:PublishSingleFile=true \
    -p:SourceRevisionId=$1

  # Build Arbitrum plugin (not self-contained, will use runner's runtime)
  dotnet publish src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj \
    -c $build_config -r $rid -o $output_path/$rid/arbitrum-tmp --no-restore --sc false \
    -p:SourceRevisionId=$1

  # Copy plugin assemblies to plugins directory
  mkdir -p $output_path/$rid/plugins
  cp $output_path/$rid/arbitrum-tmp/Nethermind.Arbitrum.* $output_path/$rid/plugins/

  # Copy Stylus native libraries (maintaining relative structure for DllImport resolution)
  mkdir -p $output_path/$rid/plugins/Arbos/Stylus
  if [ -d "$output_path/$rid/arbitrum-tmp/Arbos/Stylus/runtimes" ]; then
    cp -r $output_path/$rid/arbitrum-tmp/Arbos/Stylus/runtimes $output_path/$rid/plugins/Arbos/Stylus/
  fi

  # Copy Arbitrum configs and chainspecs
  cp -r src/Nethermind.Arbitrum/Properties/configs/* $output_path/$rid/configs/ 2>/dev/null || true
  cp -r src/Nethermind.Arbitrum/Properties/chainspec/* $output_path/$rid/chainspec/ 2>/dev/null || true

  # Clean up temporary plugin build
  rm -rf $output_path/$rid/arbitrum-tmp

  mkdir -p $output_path/$rid/keystore

  # A temporary symlink for Linux to support the old executable name
  [[ "$rid" == linux-* ]] && ln -sr $output_path/$rid/nethermind $output_path/$rid/Nethermind.Runner
done

mkdir -p $output_path/ref
cp src/Nethermind/artifacts/obj/**/$build_config/refint/*.dll $output_path/ref 2>/dev/null || true

echo "Build completed"
