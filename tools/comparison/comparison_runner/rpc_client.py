"""RPC client for Nethermind debug_reinitialize calls.

This module provides a single source of truth for RPC communication,
eliminating code duplication from the original monolithic implementation.

Features:
- Address computation with LRU caching for performance
- Proper exception handling using RPCError
- Type-safe interfaces with dataclasses
"""

from __future__ import annotations

import json
import urllib.error
import urllib.request
from functools import lru_cache
from typing import Final

from .config import DEFAULT_TEST_BALANCE, PRECOMPUTED_ADDRESSES
from .exceptions import RPCError

# Check for optional eth_keys dependency
try:
    from eth_hash.auto import keccak
    from eth_keys import keys

    ETH_KEYS_AVAILABLE = True
except ImportError:
    ETH_KEYS_AVAILABLE = False
    keccak = None  # type: ignore[assignment]
    keys = None  # type: ignore[assignment]


RPC_TIMEOUT_S: Final[int] = 30
"""Timeout for RPC calls in seconds."""

DEFAULT_ARBOS_VERSION: Final[int] = 51
"""Default ArbOS version for state reinitialization."""


@lru_cache(maxsize=128)
def compute_test_address(name: str) -> str:
    """Compute a test account address using the same algorithm as Nitro.

    Uses LRU cache to avoid recomputing addresses for the same test names.

    Args:
        name: Account name (e.g., "Owner", "Faucet")

    Returns:
        Checksummed Ethereum address (0x-prefixed)

    Raises:
        RuntimeError: If eth_keys is not installed and the address is not precomputed
    """
    if not ETH_KEYS_AVAILABLE:
        if name in PRECOMPUTED_ADDRESSES:
            return PRECOMPUTED_ADDRESSES[name]
        raise RuntimeError(
            f"Cannot compute address for '{name}': eth_keys not installed. "
            "Install with: pip install eth-keys eth-hash[pycryptodome]"
        )

    key_bytes = bytearray(keccak(name.encode("utf-8")))
    key_bytes[0] = 0
    private_key = keys.PrivateKey(bytes(key_bytes))
    return private_key.public_key.to_checksum_address()


def get_test_accounts(test_name: str) -> dict[str, dict[str, str]]:
    """Get the accounts required for a specific test.

    Args:
        test_name: Name of the test (currently unused, reserved for overrides)

    Returns:
        Dictionary mapping addresses (without 0x prefix) to account data
    """
    standard_accounts = ["Owner", "Faucet"]
    # Reserved for test-specific account overrides
    test_account_overrides: dict[str, list[str]] = {}
    account_names = test_account_overrides.get(test_name, standard_accounts)

    accounts: dict[str, dict[str, str]] = {}
    for name in account_names:
        address = compute_test_address(name)
        address_no_prefix = address[2:] if address.startswith("0x") else address
        accounts[address_no_prefix] = {"balance": DEFAULT_TEST_BALANCE}
    return accounts


def reinitialize_state(
    host: str,
    port: int,
    test_name: str,
    arbos_version: int = DEFAULT_ARBOS_VERSION,
    timeout_s: int = RPC_TIMEOUT_S,
) -> None:
    """Call debug_reinitialize RPC to reset Nethermind state for a new test.

    This is the single source of truth for state reinitialization,
    removing duplication between sequential and parallel runners.

    Args:
        host: Nethermind RPC host
        port: Nethermind RPC port
        test_name: Test name for account generation
        arbos_version: ArbOS version (default 51)
        timeout_s: RPC call timeout in seconds

    Raises:
        RPCError: If the RPC call fails or returns an error
    """
    url = f"http://{host}:{port}"

    # Build accounts JSON for this test
    accounts = get_test_accounts(test_name)
    # Convert to the format expected by RPC (with 0x prefix)
    accounts_with_prefix = {}
    for addr, data in accounts.items():
        full_addr = f"0x{addr}" if not addr.startswith("0x") else addr
        accounts_with_prefix[full_addr] = data
    accounts_json = json.dumps(accounts_with_prefix)

    # Build JSON-RPC request
    rpc_request = {
        "jsonrpc": "2.0",
        "method": "debug_reinitialize",
        "params": [arbos_version, accounts_json, None],
        "id": 1,
    }

    try:
        req = urllib.request.Request(
            url,
            data=json.dumps(rpc_request).encode("utf-8"),
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(req, timeout=timeout_s) as resp:
            result = json.loads(resp.read().decode("utf-8"))

        if "error" in result:
            raise RPCError(
                f"debug_reinitialize failed: {result['error']}",
                method="debug_reinitialize",
                port=port,
                response=str(result),
            )

        if result.get("result") is not True:
            raise RPCError(
                f"debug_reinitialize returned unexpected result: {result}",
                method="debug_reinitialize",
                port=port,
                response=str(result),
            )

    except urllib.error.HTTPError as e:
        raise RPCError(
            f"debug_reinitialize HTTP error: {e.code} {e.reason}",
            method="debug_reinitialize",
            port=port,
        ) from e
    except urllib.error.URLError as e:
        raise RPCError(
            f"debug_reinitialize RPC failed: {e}",
            method="debug_reinitialize",
            port=port,
        ) from e
    except json.JSONDecodeError as e:
        raise RPCError(
            f"Invalid JSON response from debug_reinitialize: {e}",
            method="debug_reinitialize",
            port=port,
        ) from e
