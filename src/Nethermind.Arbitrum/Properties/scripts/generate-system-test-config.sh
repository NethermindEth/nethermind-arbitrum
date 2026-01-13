#!/bin/bash

set -e

# Parse arguments
ARBOS_VERSION=${1:-40}
ACCOUNTS_FILE=${2:-"src/Nethermind.Arbitrum/Properties/accounts/default.json"}
CONFIG_NAME=${3:-"arbitrum-system-test-custom"}
MAX_CODE_SIZE=${4:-"0x6000"}
TEMPLATE_FILE="src/Nethermind.Arbitrum/Properties/chainspec/chainspec.template"
BUILD_DIR="src/Nethermind/src/Nethermind/artifacts/bin/Nethermind.Runner/debug"

# Output files
CHAINSPEC_FILE="${BUILD_DIR}/chainspec/${CONFIG_NAME}.json"
CONFIG_FILE="${BUILD_DIR}/configs/${CONFIG_NAME}.json"

# Validate MAX_CODE_SIZE is hex format
if [[ ! $MAX_CODE_SIZE =~ ^0x[0-9a-fA-F]+$ ]]; then
    echo "Error: MAX_CODE_SIZE must be in hex format (e.g., 0x6000)"
    exit 1
fi

# Auto-calculate mixHash from ArbOS version
VERSION_HEX=$(printf '%02X' $ARBOS_VERSION)
MIX_HASH="0x0000000000000000000000000000000000000000000000${VERSION_HEX}0000000000000000"

# Check if accounts file exists
if [ ! -f "$ACCOUNTS_FILE" ]; then
    echo "Error: Accounts file '$ACCOUNTS_FILE' not found"
    exit 1
fi

# Read accounts
ACCOUNTS=$(cat "$ACCOUNTS_FILE" | jq -c .)

# Check if template exists
if [ ! -f "$TEMPLATE_FILE" ]; then
    echo "Error: Template file not found at $TEMPLATE_FILE"
    exit 1
fi

# Create directories if needed
mkdir -p "${BUILD_DIR}/chainspec"
mkdir -p "${BUILD_DIR}/configs"

# Generate chainspec
echo "Generating configuration:"
echo "  Config Name: $CONFIG_NAME"
echo "  ArbOS Version: $ARBOS_VERSION"
echo "  MixHash: $MIX_HASH (auto-calculated)"
echo "  Max Code Size: $MAX_CODE_SIZE"
echo "  Accounts: $ACCOUNTS_FILE"
echo "  Chainspec: $CHAINSPEC_FILE"
echo "  Config: $CONFIG_FILE"

# Create chainspec file
sed "s|{{ARBOS_VERSION}}|$ARBOS_VERSION|g" "$TEMPLATE_FILE" | \
sed "s|{{MIX_HASH}}|$MIX_HASH|g" | \
sed "s|{{MAX_CODE_SIZE}}|$MAX_CODE_SIZE|g" | \
sed "s|{{ACCOUNTS}}|{}|g" | \
jq --argjson accounts "$ACCOUNTS" '.accounts = $accounts' > "$CHAINSPEC_FILE"

# Create config file that points to the chainspec
cat > "$CONFIG_FILE" << EOF
{
  "\$schema": "https://raw.githubusercontent.com/NethermindEth/core-scripts/refs/heads/main/schemas/config.json",
  "Init": {
    "ChainSpecPath": "chainspec/${CONFIG_NAME}.json",
    "BaseDbPath": "nethermind_db/arbitrum-system-test",
    "LogFileName": "arbitrum-system-test.log"
  },
  "TxPool": {
    "BlobsSupport": "Disabled"
  },
  "Sync": {
    "NetworkingEnabled": false,
    "FastSync": false,
    "SnapSync": false,
    "FastSyncCatchUpHeightDelta": "10000000000"
  },
  "Discovery": {
    "DiscoveryVersion": "V5"
  },
  "JsonRpc": {
    "Enabled": true,
    "Port": 20545,
    "EnginePort": 20551,
    "UnsecureDevNoRpcAuthentication": true,
    "AdditionalRpcUrls": [
      "http://localhost:28551|http;ws|net;eth;subscribe;web3;client;debug"
    ],
    "EnabledModules": [
      "Admin",
      "Clique",
      "Consensus",
      "Db",
      "Debug",
      "Deposit",
      "Erc20",
      "Eth",
      "Evm",
      "Net",
      "Nft",
      "Parity",
      "Personal",
      "Proof",
      "Subscribe",
      "Trace",
      "TxPool",
      "Vault",
      "Web3",
      "Arbitrum"
    ]
  },
  "Pruning": {
    "PruningBoundary": 192
  },
  "Blocks": {
    "SecondsPerSlot": 2
  },
  "Merge": {
    "Enabled": true
  }
}
EOF

echo "✓ Configuration generated successfully"
