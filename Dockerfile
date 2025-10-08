# ============================================================================
# Stage 1: Build Arbitrum plugin
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS arbitrum-build

WORKDIR /src

ARG BUILD_CONFIG=Release
ARG BUILD_TIMESTAMP
ARG COMMIT_HASH

# Copy build configuration
COPY src/Directory.Build.props ./
COPY src/nuget.config ./

# Copy source files
COPY src/Nethermind src/Nethermind
COPY src/Nethermind.Arbitrum src/Nethermind.Arbitrum

# Build with cache mount for NuGet packages
# Cache mount provides fast restore while keeping packages out of final image
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj \
      -c $BUILD_CONFIG \
      -o /arbitrum-plugin \
      --sc false \
      -p:BuildTimestamp=$BUILD_TIMESTAMP \
      -p:Commit=$COMMIT_HASH \
      -p:DeterministicSourcePaths=false


# ============================================================================
# Stage 2: Build Nethermind Runner
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS runner-build

WORKDIR /src

ARG BUILD_CONFIG=Release
ARG BUILD_TIMESTAMP
ARG COMMIT_HASH

# Copy build configuration
COPY src/Directory.Build.props ./
COPY src/nuget.config ./

# Copy Nethermind source
COPY src/Nethermind src/Nethermind

# Build with cache mount for NuGet packages
# Sharing=locked prevents cache corruption from parallel builds
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Nethermind/src/Nethermind/Nethermind.Runner/Nethermind.Runner.csproj \
      -c $BUILD_CONFIG \
      -o /app \
      --sc false \
      -p:BuildTimestamp=$BUILD_TIMESTAMP \
      -p:Commit=$COMMIT_HASH \
      -p:DeterministicSourcePaths=false


# ============================================================================
# Stage 3: Assemble final build artifacts
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