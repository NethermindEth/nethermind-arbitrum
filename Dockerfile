# SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
# SPDX-License-Identifier: LGPL-3.0-only

FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build

ARG BUILD_CONFIG=Release
ARG BUILD_TIMESTAMP
ARG CI
ARG COMMIT_HASH

WORKDIR /src

# Copy source files
COPY src/Nethermind src/Nethermind
COPY src/Nethermind.Arbitrum src/Nethermind.Arbitrum
COPY src/Nethermind.Arbitrum/Directory.*.props .
COPY src/Nethermind.Arbitrum/nuget.config .

# Build Arbitrum plugin first (auto-detect architecture)
RUN dotnet publish src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj -c $BUILD_CONFIG -o /arbitrum-plugin --sc false \
      -p:BuildTimestamp=$BUILD_TIMESTAMP -p:Commit=$COMMIT_HASH -p:DeterministicSourcePaths=false

# Build main Nethermind Runner  
RUN dotnet publish src/Nethermind/src/Nethermind/Nethermind.Runner/Nethermind.Runner.csproj -c $BUILD_CONFIG -o /app --sc false \
      -p:BuildTimestamp=$BUILD_TIMESTAMP -p:Commit=$COMMIT_HASH -p:DeterministicSourcePaths=false

# Copy Arbitrum plugin to plugins directory
RUN mkdir -p /app/plugins && \
    cp /arbitrum-plugin/Nethermind.Arbitrum.* /app/plugins/

# Copy Stylus native libraries to maintain relative structure from plugin assembly
# The /arbitrum-plugin directory only exists in build stage and won't be available at runtime.
# Native libraries must be copied to /app/plugins/Arbos/Stylus/ so the StylusNative.Loader can
# find them at runtime using DllImportSearchPath.AssemblyDirectory relative path resolution.
RUN mkdir -p /app/plugins/Arbos/Stylus && \
    cp -r /arbitrum-plugin/Arbos/Stylus/runtimes /app/plugins/Arbos/Stylus/ && \
    echo "Stylus libraries copied:" && \
    find /app/plugins/Arbos/Stylus -name "*.so" -o -name "*.dylib" -o -name "*.dll" | sort

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