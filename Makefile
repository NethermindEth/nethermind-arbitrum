ROOT_DIR := $(shell pwd)
BUILD_OUTPUT_DIR := $(ROOT_DIR)/src/Nethermind/src/Nethermind/artifacts/bin/Nethermind.Runner/release

.PHONY: run clean clean-monitoring clean-all clean-restart-monitoring stop clean-run run-sepolia run-sepolia-verify clean-run-sepolia clean-run-sepolia-verify build test format coverage coverage-staged coverage-report help

all: run ## Default target

run-local: ## Start Nethermind Arbitrum node without cleaning .data
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-local --data-dir $(ROOT_DIR)/.data

nethermind-help:
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -h

clean-run-local: ## Clean .data and start Nethermind Arbitrum node
	@$(MAKE) clean
	@$(MAKE) run-local

run-system-test: ## Start Nethermind Arbitrum node without cleaning .data
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-system-test --data-dir $(ROOT_DIR)/.data --log debug

clean-run-system-test: ## Clean .data and start Nethermind Arbitrum node
	@$(MAKE) clean
	@$(MAKE) run-system-test

run-sepolia: ## Start Nethermind Arbitrum node (Sepolia) without cleaning .data
	@echo "Starting Nethermind Arbitrum node (Sepolia) with metrics..."
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-sepolia-archive --data-dir $(ROOT_DIR)/.data --log debug $(NETHERMIND_ARGS)

run-sepolia-verify: ## Start Nethermind Arbitrum node (Sepolia) with block hash verification enabled
	@echo "Starting Nethermind Arbitrum node (Sepolia) with block hash verification..."
	@$(MAKE) run-sepolia NETHERMIND_ARGS="--VerifyBlockHash.Enabled=true"
clean-run-sepolia: ## Clean .data and start Nethermind Arbitrum node (Sepolia)
	@$(MAKE) clean
	@$(MAKE) run-sepolia

clean-run-sepolia-verify: ## Clean .data and start Nethermind Arbitrum node (Sepolia) with block hash verification
	@$(MAKE) clean
	@$(MAKE) run-sepolia-verify

run-mainnet: ## Start Nethermind Arbitrum node (Mainnet) without cleaning .data
	@echo "Starting Nethermind Arbitrum node (Mainnet) with metrics..."
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-mainnet-archive \
		--data-dir $(ROOT_DIR)/.data \
  	--Snapshot.Enabled true \
  	--Snapshot.DownloadUrl "https://drive.usercontent.google.com/download?id=1Pf2jTRqgy41dZ-phpyDBvKHqrnQniKgJ&export=download&authuser=1&confirm=t&uuid=55e02503-d00b-4efa-8eb1-9cfaab8d49c8&at=AKSUxGMRmuAIU5MHSL33qOFtwc1q:1760799619104"

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
