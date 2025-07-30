ROOT_DIR := $(shell pwd)
BUILD_OUTPUT_DIR := $(ROOT_DIR)/src/Nethermind/src/Nethermind/artifacts/bin/Nethermind.Runner/debug

.PHONY: run clean clean-run build test format help

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

format: ## Format code using dotnet format
	@echo "Formatting Nethermind Arbitrum code..."
	dotnet format src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj
	dotnet format src/Nethermind.Arbitrum.Test/Nethermind.Arbitrum.Test.csproj

help: ## Show this help message
	@echo "Available targets:"
	@awk 'BEGIN {FS = ":.*?## "} /^[a-zA-Z_-]+:.*?## / {printf "  %-15s %s\n", $$1, $$2}' $(MAKEFILE_LIST) | sort
