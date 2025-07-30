ROOT_DIR := $(shell pwd)
BUILD_OUTPUT_DIR := $(ROOT_DIR)/src/Nethermind/src/Nethermind/artifacts/bin/Nethermind.Runner/debug

.PHONY: run clean clean-run build test format coverage coverage-staged coverage-report help

all: run ## Default target

run: ## Start Nethermind Arbitrum node without cleaning .data
	@echo "Starting Nethermind Arbitrum node..."
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-local --data-dir $(ROOT_DIR)/.data

clean-run: clean ## Clean .data and start Nethermind Arbitrum node
	@echo "Starting Nethermind Arbitrum node after cleaning..."
	cd $(BUILD_OUTPUT_DIR) && dotnet nethermind.dll -c arbitrum-local --data-dir $(ROOT_DIR)/.data

clean: ## Remove .data directory
	@echo "Cleaning .data directory..."
	@rm -rf $(ROOT_DIR)/.data

build: ## Build Nethermind Arbitrum project
	@echo "Building Nethermind Arbitrum..."
	dotnet build src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj

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
