ROOT_DIR := $(shell pwd)
BUILD_OUTPUT_DIR := $(ROOT_DIR)/src/Nethermind/src/Nethermind/artifacts/bin/Nethermind.Runner/debug

# JWT secret file - shared between Nethermind and Nitro
# Using ~/.arbitrum (user-private, shared location for Nitro)
JWT_FILE ?= $(HOME)/.arbitrum/jwt.hex

# Default values (can be overridden)
ARBOS_VERSION ?= 51
ACCOUNTS_FILE ?= src/Nethermind.Arbitrum/Properties/accounts/defaults.json
MAX_CODE_SIZE ?= 0x6000
CONFIG_NAME := arbitrum-system-test

# Generate config dynamically
generate-system-test-config:
	@./src/Nethermind.Arbitrum/Properties/scripts/generate-system-test-config.sh $(ARBOS_VERSION) $(ACCOUNTS_FILE) $(CONFIG_NAME) $(MAX_CODE_SIZE)

# Run with custom parameters (no JWT - for local dev)
run-system-test: generate-system-test-config
	@echo "Starting Nethermind with system-test config..."
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c $(CONFIG_NAME) --data-dir $(ROOT_DIR)/.data --JsonRpc.UnsecureDevNoRpcAuthentication=true --log debug

clean-run-system-test: clean generate-system-test-config
	@echo "Clean start with system-test config..."
	@$(MAKE) run-system-test

run-local: ## Start Nethermind Arbitrum node without cleaning .data (no JWT)
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-local --data-dir $(ROOT_DIR)/.data --JsonRpc.UnsecureDevNoRpcAuthentication=true

nethermind-help:
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -h

clean-run-local: ## Clean .data and start Nethermind Arbitrum node
	@$(MAKE) clean
	@$(MAKE) run-local


run-sepolia: ## Start Nethermind Arbitrum node (Sepolia) without cleaning .data
	@echo "Starting Nethermind Arbitrum node (Sepolia)..."
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-sepolia-archive --data-dir $(ROOT_DIR)/.data --JsonRpc.JwtSecretFile=$(JWT_FILE) --log debug $(NETHERMIND_ARGS)

run-sepolia-verify: ## Start Nethermind Arbitrum node (Sepolia) with block hash verification enabled
	@echo "Starting Nethermind Arbitrum node (Sepolia) with block hash verification..."
	@$(MAKE) run-sepolia NETHERMIND_ARGS="--VerifyBlockHash.Enabled=true"

run-sepolia-unsafe: ## Start Nethermind Arbitrum node (Sepolia) WITHOUT JWT auth
	@echo "Starting Nethermind Arbitrum node (Sepolia) without JWT auth..."
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-sepolia-archive --data-dir $(ROOT_DIR)/.data --JsonRpc.UnsecureDevNoRpcAuthentication=true --log debug $(NETHERMIND_ARGS)

clean-run-sepolia: ## Clean .data and start Nethermind Arbitrum node (Sepolia)
	@$(MAKE) clean
	@$(MAKE) run-sepolia

clean-run-sepolia-verify: ## Clean .data and start Nethermind Arbitrum node (Sepolia) with block hash verification
	@$(MAKE) clean
	@$(MAKE) run-sepolia-verify

run-mainnet: ## Start Nethermind Arbitrum node (Mainnet) without cleaning .data
	@echo "Starting Nethermind Arbitrum node (Mainnet)..."
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-mainnet-archive \
		--data-dir $(ROOT_DIR)/.data \
		--JsonRpc.JwtSecretFile=$(JWT_FILE) \
		--Snapshot.Enabled true \
		--Snapshot.DownloadUrl "https://arb-snapshot.nethermind.dev/arbitrum-snapshot/snapshot.zip"

run-mainnet-unsafe: ## Start Nethermind Arbitrum node (Mainnet) WITHOUT JWT auth
	@echo "Starting Nethermind Arbitrum node (Mainnet) without JWT auth..."
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-mainnet-archive \
		--data-dir $(ROOT_DIR)/.data \
		--JsonRpc.UnsecureDevNoRpcAuthentication=true \
		--Snapshot.Enabled true \
		--Snapshot.DownloadUrl "https://arb-snapshot.nethermind.dev/arbitrum-snapshot/snapshot.zip"

clean-run-mainnet: ## Clean .data and start Nethermind Arbitrum node (Mainnet)
	@$(MAKE) clean
	@$(MAKE) run-mainnet

run-sepolia-monitoring: ## Start monitoring stack and Nethermind Arbitrum node (Sepolia)
	@echo "Starting monitoring stack..."
	@./start-monitoring.sh
	@echo "Starting Nethermind Arbitrum node (Sepolia) with metrics..."
	@$(MAKE) run-sepolia

clean-run-sepolia-monitoring: ## Clean .data, start monitoring and Nethermind Arbitrum node (Sepolia)
	@$(MAKE) clean
	@$(MAKE) run-sepolia-monitoring

clean: ## Remove .data directory
	@echo "Cleaning .data directory..."
	@rm -rf $(ROOT_DIR)/.data
	@rm -f $(ROOT_DIR)/.generated-chainspec.json

clean-monitoring: ## Clean monitoring data (Prometheus metrics)
	@echo "Cleaning monitoring data..."
	@docker-compose -f docker-compose.monitoring.yml down 2>/dev/null || true
	@docker volume rm nethermind-arbitrum_prometheus_data 2>/dev/null || true
	@echo "Monitoring data cleaned"

clean-all: ## Clean both Nethermind data and monitoring data
	@echo "Cleaning all data (Nethermind + Monitoring)..."
	@$(MAKE) clean
	@$(MAKE) clean-monitoring
	@echo "All data cleaned"

clean-restart-monitoring: ## Clean all data and restart with fresh monitoring
	@echo "Cleaning all data and restarting with fresh monitoring..."
	@$(MAKE) clean-all
	@$(MAKE) run-sepolia-monitoring

stop: ## Stop Nethermind and monitoring stack
	@echo "Stopping Nethermind and monitoring stack..."
	@pkill -f "dotnet.*nethermind.dll" 2>/dev/null || true
	@docker-compose -f docker-compose.monitoring.yml down 2>/dev/null || true
	@echo "All services stopped"

build: ## Build Nethermind Arbitrum project
	@echo "Building Nethermind Arbitrum..."
	dotnet build src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj -c Release

test: ## Run Nethermind Arbitrum tests
	@echo "Running Nethermind Arbitrum tests..."
	dotnet test src/Nethermind.Arbitrum.Test/Nethermind.Arbitrum.Test.csproj

coverage: ## Generate and display test coverage report
	@echo "Generating test coverage report..."
	dotnet test src/Nethermind.Arbitrum.Test/Nethermind.Arbitrum.Test.csproj --collect:"XPlat Code Coverage" --results-directory $(ROOT_DIR)/test-coverage
	@echo "Coverage report generated in test-coverage directory"
	@echo "Open test-coverage/coverage.cobertura.xml in your IDE or use a coverage viewer"

coverage-report: ## Generate and open HTML coverage report in browser
	@echo "Checking for existing coverage data..."
	@if [ ! -d "$(ROOT_DIR)/test-coverage" ] || [ -z "$$(find $(ROOT_DIR)/test-coverage -name '*.xml' 2>/dev/null)" ]; then \
		echo "📊 No coverage data found. Generating coverage first..."; \
		$(MAKE) coverage; \
	else \
		echo "✅ Found existing coverage data"; \
	fi
	@echo "Generating HTML coverage report with line-by-line visualization..."
	@if command -v reportgenerator >/dev/null 2>&1; then \
		reportgenerator \
			-reports:$(ROOT_DIR)/test-coverage/*/coverage.cobertura.xml \
			-targetdir:$(ROOT_DIR)/test-coverage/html \
			-reporttypes:Html \
			-sourcedirs:$(ROOT_DIR)/src \
			-historydir:$(ROOT_DIR)/test-coverage/history \
			-verbosity:Info \
			-title:"Nethermind Arbitrum Coverage Report"; \
		echo "✅ HTML coverage report generated at $(ROOT_DIR)/test-coverage/html/index.html"; \
		echo "📊 Features: Line-by-line coverage, branch coverage, file tree, search"; \
		if command -v open >/dev/null 2>&1; then \
			echo "🌐 Opening in browser..."; \
			open $(ROOT_DIR)/test-coverage/html/index.html; \
		elif command -v xdg-open >/dev/null 2>&1; then \
			echo "🌐 Opening in browser..."; \
			xdg-open $(ROOT_DIR)/test-coverage/html/index.html; \
		else \
			echo "🌐 Please open $(ROOT_DIR)/test-coverage/html/index.html in your browser"; \
		fi; \
	else \
		echo "❌ ReportGenerator not found. Installing..."; \
		dotnet tool install -g dotnet-reportgenerator-globaltool; \
		echo "✅ ReportGenerator installed. Run 'make coverage-report' again."; \
	fi

format: ## Format code using dotnet format
	@echo "Formatting Nethermind Arbitrum code..."
	dotnet format src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj
	dotnet format src/Nethermind.Arbitrum.Test/Nethermind.Arbitrum.Test.csproj

help: ## Show this help message
	@echo "Available targets:"
	@awk 'BEGIN {FS = ":.*?## "} /^[a-zA-Z_-]+:.*?## / {printf "  %-15s %s\n", $$1, $$2}' $(MAKEFILE_LIST) | sort

list-system-test-accounts: ## List available account configurations for System Tests
	@echo "Available account configurations:"
	@ls -1 src/Nethermind.Arbitrum/Properties/accounts/*.json 2>/dev/null || echo "  No accounts files found"

list-system-test-configs: ## Example configurations for System Tests
	@echo "Example usage:"
	@echo ""
	@echo "1. Run with default settings (ArbOS 40, default accounts):"
	@echo "   make run-system-test"
	@echo ""
	@echo "2. Run with specific ArbOS version:"
	@echo "   make run-system-test ARBOS_VERSION=50"
	@echo ""
	@echo "3. Run with specific accounts:"
	@echo "   make run-system-test ACCOUNTS_FILE=src/Nethermind.Arbitrum/Properties/accounts/contract-tx.json"
	@echo ""
	@echo "4. Run with custom max code size (default is 0x6000 = 24KB):"
	@echo "   make run-system-test MAX_CODE_SIZE=0xC000"
	@echo ""
	@echo "5. Combine all parameters:"
	@echo "   make run-system-test ARBOS_VERSION=51 ACCOUNTS_FILE=src/Nethermind.Arbitrum/Properties/accounts/contract-tx.json MAX_CODE_SIZE=0xC000"
	@echo ""
	@echo "6. Clean run with custom settings:"
	@echo "   make clean-run-system-test ARBOS_VERSION=30 ACCOUNTS_FILE=src/Nethermind.Arbitrum/Properties/accounts/contract-tx.json"
	@echo ""
