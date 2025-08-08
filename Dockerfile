# SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
# SPDX-License-Identifier: LGPL-3.0-only

# syntax=docker/dockerfile:1.6
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build

ARG BUILD_CONFIG=Release
ARG BUILD_TIMESTAMP
ARG CI
ARG COMMIT_HASH
ARG TARGETARCH

WORKDIR /src

# Copy plugin sources (always present in this repo)
COPY src/Nethermind.Arbitrum src/Nethermind.Arbitrum
COPY src/Nethermind.Arbitrum/Directory.*.props .
COPY src/Nethermind.Arbitrum/nuget.config .

# Bring Nethermind core sources:
# - If build context contains submodule at src/Nethermind, use it (local builds)
# - Otherwise, clone the tracked branch (CI using reusable workflow)
RUN --mount=type=bind,source=.,target=/context \
    sh -c 'set -eu; \
      if [ -d /context/src/Nethermind ]; then \
        mkdir -p /src/src && cp -a /context/src/Nethermind /src/src/; \
      else \
        apt-get update && apt-get install -y --no-install-recommends git ca-certificates && \
        rm -rf /var/lib/apt/lists/* && \
        git clone --depth 1 --branch feature/arbitrum-setup https://github.com/NethermindEth/nethermind.git src/Nethermind; \
      fi'

# Build Arbitrum plugin first
RUN arch=$([ "$TARGETARCH" = "amd64" ] && echo "x64" || echo "$TARGETARCH") && \
    dotnet publish src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj -c $BUILD_CONFIG -a $arch -o /arbitrum-plugin --sc false \
      -p:BuildTimestamp=$BUILD_TIMESTAMP -p:Commit=$COMMIT_HASH

# Build main Nethermind Runner  
RUN arch=$([ "$TARGETARCH" = "amd64" ] && echo "x64" || echo "$TARGETARCH") && \
    dotnet publish src/Nethermind/src/Nethermind/Nethermind.Runner/Nethermind.Runner.csproj -c $BUILD_CONFIG -a $arch -o /app --sc false \
      -p:BuildTimestamp=$BUILD_TIMESTAMP -p:Commit=$COMMIT_HASH

# Copy Arbitrum plugin to plugins directory
RUN mkdir -p /app/plugins && \
    cp /arbitrum-plugin/Nethermind.Arbitrum.* /app/plugins/

# Copy configuration files
COPY src/Nethermind.Arbitrum/Properties/configs /app/configs
COPY src/Nethermind.Arbitrum/Properties/chainspec /app/chainspec

# Create data directory
RUN mkdir -p /app/data

FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble

WORKDIR /app

# Create non-root user for security
RUN groupadd -r nethermind && useradd -r -g nethermind nethermind

# Create required directories and set permissions
RUN mkdir -p /data /app/logs /app/keystore && \
    chown -R nethermind:nethermind /data /app

VOLUME ["/data", "/app/logs", "/app/keystore"]

# Expose ports for JSON-RPC, Engine API, and metrics
EXPOSE 8545 8551 6060

# Copy application from build stage
COPY --from=build --chown=nethermind:nethermind /app .

# Switch to non-root user
USER nethermind

# Health check to ensure service is running  
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl --fail --silent http://localhost:8545/health || exit 1

ENTRYPOINT ["./nethermind"] 