ROOT_DIR := $(shell pwd)
EXTERNAL_DIR := /Volumes/S990Pro
BUILD_OUTPUT_DIR := $(ROOT_DIR)/src/Nethermind/src/Nethermind/artifacts/bin/Nethermind.Runner/debug

# JWT secret file - shared between Nethermind and Nitro
JWT_FILE ?= $(HOME)/.arbitrum/jwt.hex

# System test defaults
ARBOS_VERSION ?= 51
ACCOUNTS_FILE ?= src/Nethermind.Arbitrum/Properties/accounts/defaults.json
MAX_CODE_SIZE ?= 0x6000
CONFIG_NAME := arbitrum-system-test

# Snapshot cache configuration (shared with Nitro at ~/.cache/blockchain-snapshots/)
SNAPSHOT_CACHE_DIR := $(HOME)/.cache/blockchain-snapshots/nethermind
MAINNET_GENESIS_SNAPSHOT := snapshot.zip
MAINNET_GENESIS_SNAPSHOT_URL := https://arb-snapshot.nethermind.dev/arbitrum-snapshot/$(MAINNET_GENESIS_SNAPSHOT)

# =============================================================================
# Macros
# =============================================================================

# Run Nethermind: $(call run-nethermind,config-name)
define run-nethermind
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c $(1) \
		--data-dir $(ROOT_DIR)/.data \
		--JsonRpc.JwtSecretFile=$(JWT_FILE) $(NETHERMIND_ARGS)
endef

# Restore snapshot: $(call restore-snapshot,config-name,snapshot-subdir)
define restore-snapshot
	@if [ -d "$(ROOT_DIR)/.data/nethermind_db/$(1)" ]; then \
		exit 0; \
	elif [ -f "$(SNAPSHOT_CACHE_DIR)/$(MAINNET_GENESIS_SNAPSHOT)" ]; then \
		echo "Restoring snapshot from cache..."; \
		mkdir -p "$(ROOT_DIR)/.data/snapshot/$(2)"; \
		cp "$(SNAPSHOT_CACHE_DIR)/$(MAINNET_GENESIS_SNAPSHOT)" "$(ROOT_DIR)/.data/snapshot/$(2)/"; \
		echo "Downloaded" > "$(ROOT_DIR)/.data/snapshot/$(2)/checkpoint_$(MAINNET_GENESIS_SNAPSHOT)"; \
	else \
		echo "No cached snapshot. Run 'make download-snapshot' to cache it."; \
	fi
endef

# Clean config data: $(call clean-config,config-name,snapshot-subdir)
define clean-config
	@echo "Cleaning $(1) data..."
	@rm -rf "$(ROOT_DIR)/.data/nethermind_db/$(1)"
	@rm -rf "$(ROOT_DIR)/.data/snapshot/$(2)"
endef

# =============================================================================
# Mainnet targets
# =============================================================================

run-mainnet: ## Run Mainnet (non-archive)
	$(call restore-snapshot,arbitrum-mainnet,mainnet)
	@echo "Starting Nethermind (Mainnet)..."
	$(call run-nethermind,arbitrum-mainnet)

run-mainnet-archive: ## Run Mainnet Archive
	$(call restore-snapshot,arbitrum-mainnet-archive,mainnet-archive)
	@echo "Starting Nethermind (Mainnet Archive)..."
	$(call run-nethermind,arbitrum-mainnet-archive)

clean-mainnet: ## Clean Mainnet data
	$(call clean-config,arbitrum-mainnet,mainnet)

clean-mainnet-archive: ## Clean Mainnet Archive data
	$(call clean-config,arbitrum-mainnet-archive,mainnet-archive)

clean-run-mainnet: clean-mainnet run-mainnet ## Clean and run Mainnet

clean-run-mainnet-archive: clean-mainnet-archive run-mainnet-archive ## Clean and run Mainnet Archive


# =============================================================================
# My targets
# =============================================================================

run-my-sepolia-archive: ## Start Nethermind Arbitrum node (Sepolia) without cleaning .data
	@echo "Starting Nethermind Arbitrum node (Sepolia)..."
	$(call ensure-stylus-host-runtime)
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-sepolia-archive --data-dir $(EXTERNAL_DIR)/sepolia/nmc_data --JsonRpc.JwtSecretFile=$(JWT_FILE) --log debug $(NETHERMIND_ARGS)

run-my-sepolia: ## Start Nethermind Arbitrum node (Sepolia) without cleaning .data
	@echo "Starting Nethermind Arbitrum node (Sepolia)..."
	$(call ensure-stylus-host-runtime)
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-sepolia-with-validation --data-dir $(EXTERNAL_DIR)/sepolia/nmc_data --JsonRpc.JwtSecretFile=$(JWT_FILE) --log debug $(NETHERMIND_ARGS)

run-my-sepolia-for-docker-nitro: ## Start Nethermind Arbitrum node (Sepolia) without cleaning .data
	@echo "Starting Nethermind Arbitrum node (Sepolia)..."
	$(call ensure-stylus-host-runtime)
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-sepolia-with-validation --data-dir $(EXTERNAL_DIR)/sepolia/nmc_data --JsonRpc.JwtSecretFile=$(JWT_FILE) --JsonRpc.EngineHost=0.0.0.0 --log debug $(NETHERMIND_ARGS)

run-my-sepolia-for-cl-comparison: ## Start Nethermind Arbitrum node (Sepolia) without cleaning .data, with open RPC for Nitro CL
	@echo "Starting Nethermind Arbitrum node (Sepolia) with open RPC for Nitro CL..."
	$(call ensure-stylus-host-runtime)
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll \
		-c arbitrum-sepolia-with-validation \
		--data-dir /Volumes/S990Pro/sepolia/nethermind \
		--JsonRpc.JwtSecretFile=/Users/gugz/.arbitrum/jwt.hex \
		--JsonRpc.Port=20545 \
		--JsonRpc.EnginePort=20551 \
		--JsonRpc.Host=0.0.0.0 \
		--JsonRpc.EngineHost=0.0.0.0 \
		--log debug

run-my-mainnet: ## Start Nethermind Arbitrum node (Mainnet) without cleaning .data
	@echo "Starting Nethermind Arbitrum node (Mainnet)..."
	$(call ensure-stylus-host-runtime)
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-mainnet-with-validation --data-dir $(EXTERNAL_DIR)/mainnet/nmc_data --JsonRpc.JwtSecretFile=$(JWT_FILE) --log debug $(NETHERMIND_ARGS)

run-my-mainnet-archive-unsafe: ## Start Nethermind Arbitrum node (Mainnet) WITHOUT JWT auth
	@echo "Starting Nethermind Arbitrum node (Mainnet) without JWT auth..."
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-mainnet-archive \
		--data-dir $(EXTERNAL_DIR)/mainnet/nmc_data \
		--JsonRpc.UnsecureDevNoRpcAuthentication=true \
		--Snapshot.Enabled false \
		--Snapshot.DownloadUrl "https://arb-snapshot.nethermind.dev/arbitrum-snapshot/snapshot.zip" \
		$(NETHERMIND_ARGS)

# =============================================================================
# Sepolia targets
# =============================================================================

run-sepolia: ## Run Sepolia (non-archive)
	@echo "Starting Nethermind (Sepolia)..."
	$(call run-nethermind,arbitrum-sepolia)

run-sepolia-archive: ## Run Sepolia Archive
	@echo "Starting Nethermind (Sepolia Archive)..."
	$(call run-nethermind,arbitrum-sepolia-archive)

clean-sepolia: ## Clean Sepolia data
	$(call clean-config,arbitrum-sepolia,sepolia)

clean-sepolia-archive: ## Clean Sepolia Archive data
	$(call clean-config,arbitrum-sepolia-archive,sepolia-archive)

clean-run-sepolia: clean-sepolia run-sepolia ## Clean and run Sepolia

clean-run-sepolia-archive: clean-sepolia-archive run-sepolia-archive ## Clean and run Sepolia Archive

run-sepolia-verify: ## Run Sepolia Archive with block hash verification
	@$(MAKE) run-sepolia-archive NETHERMIND_ARGS="--VerifyBlockHash.Enabled=true"

clean-run-sepolia-verify: clean-sepolia-archive run-sepolia-verify ## Clean and run Sepolia with verification

# =============================================================================
# Snapshot cache management
# =============================================================================

download-snapshot: ## Download mainnet snapshot to cache
	@echo "Downloading mainnet snapshot to cache..."
	@mkdir -p "$(SNAPSHOT_CACHE_DIR)"
	@if command -v aria2c > /dev/null 2>&1; then \
		echo "Using aria2c (16 connections)..."; \
		aria2c -x 16 -s 16 -k 1M -c \
			-d "$(SNAPSHOT_CACHE_DIR)" -o "$(MAINNET_GENESIS_SNAPSHOT)" "$(MAINNET_GENESIS_SNAPSHOT_URL)"; \
	else \
		echo "Using curl (install aria2 for faster downloads: brew install aria2)..."; \
		curl -L -C - --http1.1 --retry 5 --retry-delay 5 --progress-bar \
			-o "$(SNAPSHOT_CACHE_DIR)/$(MAINNET_GENESIS_SNAPSHOT)" "$(MAINNET_GENESIS_SNAPSHOT_URL)"; \
	fi
	@if unzip -t "$(SNAPSHOT_CACHE_DIR)/$(MAINNET_GENESIS_SNAPSHOT)" > /dev/null 2>&1; then \
		echo "Snapshot downloaded and cached successfully."; \
	else \
		echo "ERROR: Download corrupted. Try again."; \
		rm -f "$(SNAPSHOT_CACHE_DIR)/$(MAINNET_GENESIS_SNAPSHOT)"; \
		exit 1; \
	fi

cache-status: ## Show snapshot cache status
	@echo "=== Nethermind Snapshot Cache Status ==="
	@echo "Cache directory: $(SNAPSHOT_CACHE_DIR)"
	@echo ""
	@if [ -f "$(SNAPSHOT_CACHE_DIR)/$(MAINNET_GENESIS_SNAPSHOT)" ]; then \
		echo "Mainnet: CACHED"; \
		ls -lh "$(SNAPSHOT_CACHE_DIR)/$(MAINNET_GENESIS_SNAPSHOT)"; \
	else \
		echo "Mainnet: NOT CACHED (run 'make download-snapshot')"; \
	fi

clean-cache: ## Remove snapshot cache
	@rm -rf $(SNAPSHOT_CACHE_DIR)
	@echo "Snapshot cache cleared."

# =============================================================================
# Local / System test targets
# =============================================================================

run-local: ## Run local dev node (no JWT)
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-local \
		--data-dir $(ROOT_DIR)/.data --JsonRpc.UnsecureDevNoRpcAuthentication=true

clean-local: ## Clean local data
	$(call clean-config,arbitrum-local,local)

clean-run-local: clean-local run-local ## Clean and run local

generate-system-test-config:
	@./src/Nethermind.Arbitrum/Properties/scripts/generate-system-test-config.sh \
		$(ARBOS_VERSION) $(ACCOUNTS_FILE) $(CONFIG_NAME) $(MAX_CODE_SIZE)

run-system-test: generate-system-test-config ## Run system test config
	@echo "Starting Nethermind with system-test config..."
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c $(CONFIG_NAME) \
		--data-dir $(ROOT_DIR)/.data --JsonRpc.UnsecureDevNoRpcAuthentication=true --log debug

clean-system-test: ## Clean system test data
	$(call clean-config,$(CONFIG_NAME),)

clean-run-system-test: clean-system-test run-system-test ## Clean and run system test

nethermind-help: ## Show Nethermind help
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -h

# =============================================================================
# General cleanup
# =============================================================================

clean: ## Remove all .data
	@rm -rf $(ROOT_DIR)/.data
	@rm -f $(ROOT_DIR)/.generated-chainspec.json
	@echo "All data cleaned."

clean-all: clean clean-cache ## Remove all data and cache

stop: ## Stop Nethermind
	@pkill -f "dotnet.*nethermind.dll" 2>/dev/null || true
	@echo "Nethermind stopped."

# =============================================================================
# Build / Test / Format
# =============================================================================

build: ## Build Nethermind Arbitrum
	dotnet build src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj -c Release

test: ## Run tests
	dotnet test src/Nethermind.Arbitrum.Test/Nethermind.Arbitrum.Test.csproj

coverage: ## Generate test coverage
	dotnet test src/Nethermind.Arbitrum.Test/Nethermind.Arbitrum.Test.csproj \
		--collect:"XPlat Code Coverage" --results-directory $(ROOT_DIR)/test-coverage

coverage-report: coverage ## Generate HTML coverage report
	@if command -v reportgenerator >/dev/null 2>&1; then \
		reportgenerator \
			-reports:$(ROOT_DIR)/test-coverage/*/coverage.cobertura.xml \
			-targetdir:$(ROOT_DIR)/test-coverage/html \
			-reporttypes:Html \
			-sourcedirs:$(ROOT_DIR)/src; \
		open $(ROOT_DIR)/test-coverage/html/index.html 2>/dev/null || true; \
	else \
		echo "Install reportgenerator: dotnet tool install -g dotnet-reportgenerator-globaltool"; \
	fi

format: ## Format code
	dotnet format src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj
	dotnet format src/Nethermind.Arbitrum.Test/Nethermind.Arbitrum.Test.csproj

# =============================================================================
# Help
# =============================================================================

help: ## Show this help
	@echo "Available targets:"
	@awk 'BEGIN {FS = ":.*?## "} /^[a-zA-Z_-]+:.*?## / {printf "  %-25s %s\n", $$1, $$2}' $(MAKEFILE_LIST) | sort

list-system-test-accounts: ## List account configurations
	@ls -1 src/Nethermind.Arbitrum/Properties/accounts/*.json 2>/dev/null

list-system-test-configs: ## Show system test examples
	@echo "Examples:"
	@echo "  make run-system-test ARBOS_VERSION=51"
	@echo "  make run-system-test ACCOUNTS_FILE=src/Nethermind.Arbitrum/Properties/accounts/contract-tx.json"
	@echo "  make clean-run-system-test ARBOS_VERSION=30"


# Different ways of running newer version of nitro with plugin

# Solution 1 (nitro-cl & plugin-el): nitro using docker (docker compose up nitro) WITH plugin run-my-sepolia-for-docker-nitro

# Solution 2 (nitro-cl & plugin-el): nitro using ~/Documents/arbitrum/nitro (run-nitro-nethermind) WITH plugin run-my-sepolia (or run-my-sepolia-for-docker-nitro, not sure exactly)

# Solution 3 (nitro-cl & nitro-el & plugin-el): node runner (tools/node-runner): uv run node-runner comparison --network sepolia --data-dir /Volumes/S990Pro

# Solution 4 (nitro-cl & nitro-el & plugin-el): node runner manually (run 3 terminals) -- Be careful because by default, commands delete your DB (normally i commented those lines out)
# - terminal 1 (nitro-el): init-el-sepolia-with-validation and then run-el-sepolia-with-validation
# - terminal 2 (plugin-el): run-my-sepolia-for-cl-comparison
# - terminal 3 (nitro-cl): run-cl-comparison-sepolia-with-validation