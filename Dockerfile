# SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
# SPDX-License-Identifier: LGPL-3.0-only

FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build

ARG BUILD_CONFIG=Release
ARG BUILD_TIMESTAMP
ARG CI
ARG COMMIT_HASH

WORKDIR /src

# Copy source files
COPY src/ ./
COPY src/Directory.Build.props ./Directory.Build.props
COPY src/nuget.config ./nuget.config

# Build Arbitrum plugin first (targeting x64 where Stylus library exists)
RUN dotnet publish Nethermind.Arbitrum/Nethermind.Arbitrum.csproj -c $BUILD_CONFIG -a x64 -o /arbitrum-plugin --sc false \
      -p:BuildTimestamp=$BUILD_TIMESTAMP -p:Commit=$COMMIT_HASH

# Build main Nethermind Runner  
RUN dotnet publish Nethermind/src/Nethermind/Nethermind.Runner/Nethermind.Runner.csproj -c $BUILD_CONFIG -a x64 -o /app --sc false \
      -p:BuildTimestamp=$BUILD_TIMESTAMP -p:Commit=$COMMIT_HASH

# Copy Arbitrum plugin to plugins directory
RUN mkdir -p /app/plugins && \
    cp /arbitrum-plugin/Nethermind.Arbitrum.* /app/plugins/

# Copy configuration files from build context
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