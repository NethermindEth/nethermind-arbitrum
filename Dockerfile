# SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
# SPDX-License-Identifier: LGPL-3.0-only

# ============================================================================
# Stage 1: Base SDK image with build tools
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS sdk-base

WORKDIR /src

# Copy only dependency files first for better layer caching
COPY src/Directory.Build.props ./
COPY src/nuget.config ./


# ============================================================================
# Stage 2: Restore Arbitrum plugin dependencies
# ============================================================================
FROM sdk-base AS arbitrum-restore

# Copy only project files needed for restore
COPY src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj \
     src/Nethermind.Arbitrum/
COPY src/Nethermind/src/Nethermind/Nethermind.Core/Nethermind.Core.csproj \
     src/Nethermind/src/Nethermind/Nethermind.Core/
COPY src/Nethermind/src/Nethermind/Nethermind.Api/Nethermind.Api.csproj \
     src/Nethermind/src/Nethermind/Nethermind.Api/
COPY src/Nethermind/src/Nethermind/Nethermind.Evm/Nethermind.Evm.csproj \
     src/Nethermind/src/Nethermind/Nethermind.Evm/

# Restore only - creates cached layer for NuGet packages
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj \
      --verbosity minimal


# ============================================================================
# Stage 3: Build Arbitrum plugin
# ============================================================================
FROM arbitrum-restore AS arbitrum-build

ARG BUILD_CONFIG=Release
ARG BUILD_TIMESTAMP
ARG COMMIT_HASH

# Copy full Nethermind source (needed for Arbitrum plugin compilation)
COPY src/Nethermind src/Nethermind
COPY src/Nethermind.Arbitrum src/Nethermind.Arbitrum

# Build with --no-restore since we already restored in previous stage
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj \
      -c $BUILD_CONFIG \
      -o /arbitrum-plugin \
      --sc false \
      --no-restore \
      -p:BuildTimestamp=$BUILD_TIMESTAMP \
      -p:Commit=$COMMIT_HASH \
      -p:DeterministicSourcePaths=false


# ============================================================================
# Stage 4: Restore Nethermind Runner dependencies
# ============================================================================
FROM sdk-base AS runner-restore

# Copy Nethermind project files for restore
COPY src/Nethermind/src/Nethermind/Nethermind.Runner/Nethermind.Runner.csproj \
     src/Nethermind/src/Nethermind/Nethermind.Runner/
COPY src/Nethermind/src/Nethermind/Nethermind.Core/Nethermind.Core.csproj \
     src/Nethermind/src/Nethermind/Nethermind.Core/
COPY src/Nethermind/src/Nethermind/Nethermind.Api/Nethermind.Api.csproj \
     src/Nethermind/src/Nethermind/Nethermind.Api/

# Restore Runner dependencies
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/Nethermind/src/Nethermind/Nethermind.Runner/Nethermind.Runner.csproj \
      --verbosity minimal


# ============================================================================
# Stage 5: Build Nethermind Runner
# ============================================================================
FROM runner-restore AS runner-build

ARG BUILD_CONFIG=Release
ARG BUILD_TIMESTAMP
ARG COMMIT_HASH

# Copy full Nethermind source
COPY src/Nethermind src/Nethermind

# Build with --no-restore
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Nethermind/src/Nethermind/Nethermind.Runner/Nethermind.Runner.csproj \
      -c $BUILD_CONFIG \
      -o /app \
      --sc false \
      --no-restore \
      -p:BuildTimestamp=$BUILD_TIMESTAMP \
      -p:Commit=$COMMIT_HASH \
      -p:DeterministicSourcePaths=false


# ============================================================================
# Stage 6: Assemble final build artifacts
# ============================================================================
FROM runner-build AS final-build

# Copy Arbitrum plugin from earlier stage
COPY --from=arbitrum-build /arbitrum-plugin /arbitrum-plugin

# Organize plugin files
RUN mkdir -p /app/plugins && \
    cp /arbitrum-plugin/Nethermind.Arbitrum.* /app/plugins/

# Copy Stylus native libraries
RUN mkdir -p /app/plugins/Arbos/Stylus && \
    cp -r /arbitrum-plugin/Arbos/Stylus/runtimes /app/plugins/Arbos/Stylus/ && \
    echo "Stylus libraries copied:" && \
    find /app/plugins/Arbos/Stylus -name "*.so" -o -name "*.dylib" -o -name "*.dll" | sort

# Copy configuration files
COPY src/Nethermind.Arbitrum/Properties/configs /app/configs
COPY src/Nethermind.Arbitrum/Properties/chainspec /app/chainspec

# Create data directory
RUN mkdir -p /app/data


# ============================================================================
# Final Stage: Runtime image
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble

WORKDIR /app

# Create non-root user
RUN groupadd -r nethermind && useradd -r -g nethermind nethermind

# Create required directories
RUN mkdir -p /data /app/logs /app/keystore && \
    chown -R nethermind:nethermind /data /app

VOLUME ["/data", "/app/logs", "/app/keystore"]

# Expose ports
EXPOSE 8545 8551 6060

# Copy only final artifacts from build stage
COPY --from=final-build --chown=nethermind:nethermind /app .

# Switch to non-root user
USER nethermind

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl --fail --silent http://localhost:8545/health || exit 1

ENTRYPOINT ["./nethermind"]