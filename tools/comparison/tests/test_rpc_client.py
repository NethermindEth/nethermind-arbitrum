"""Tests for the RPC client module."""

from __future__ import annotations

import json
from typing import TYPE_CHECKING
from unittest.mock import MagicMock, patch

import pytest

from comparison_runner.exceptions import RPCError
from comparison_runner.rpc_client import (
    DEFAULT_ARBOS_VERSION,
    RPC_TIMEOUT_S,
    compute_test_address,
    get_test_accounts,
    reinitialize_state,
)

if TYPE_CHECKING:
    pass


class TestComputeTestAddress:
    """Tests for compute_test_address function."""

    def test_precomputed_owner_address(self) -> None:
        """Should return precomputed address for Owner."""
        address = compute_test_address("Owner")
        assert address == "0x26E554a8acF9003b83495c7f45F06edCB803d4e3"

    def test_precomputed_faucet_address(self) -> None:
        """Should return precomputed address for Faucet."""
        address = compute_test_address("Faucet")
        assert address == "0xaF24Ca6c2831f4d4F629418b50C227DF0885613A"

    def test_address_is_cached(self) -> None:
        """Same input should return cached result."""
        # Clear cache first
        compute_test_address.cache_clear()

        addr1 = compute_test_address("Owner")
        addr2 = compute_test_address("Owner")

        assert addr1 == addr2
        # Check cache info
        cache_info = compute_test_address.cache_info()
        assert cache_info.hits >= 1

    def test_different_names_different_addresses(self) -> None:
        """Different names should produce different addresses."""
        addr1 = compute_test_address("Owner")
        addr2 = compute_test_address("Faucet")
        assert addr1 != addr2


class TestGetTestAccounts:
    """Tests for get_test_accounts function."""

    def test_returns_owner_and_faucet(self) -> None:
        """Should return Owner and Faucet accounts."""
        accounts = get_test_accounts("TestTransfer")
        assert len(accounts) == 2

    def test_accounts_have_balance(self) -> None:
        """Each account should have a balance."""
        accounts = get_test_accounts("TestTransfer")
        for _addr, data in accounts.items():
            assert "balance" in data
            assert data["balance"].startswith("0x")

    def test_addresses_without_prefix(self) -> None:
        """Addresses should be without 0x prefix."""
        accounts = get_test_accounts("TestTransfer")
        for addr in accounts:
            assert not addr.startswith("0x")


class TestReinitializeState:
    """Tests for reinitialize_state function."""

    def test_success_response(self) -> None:
        """Should succeed when RPC returns result: true."""
        mock_response = json.dumps({"jsonrpc": "2.0", "result": True, "id": 1})

        with patch("urllib.request.urlopen") as mock_urlopen:
            mock_ctx = MagicMock()
            mock_ctx.__enter__ = MagicMock(return_value=mock_ctx)
            mock_ctx.__exit__ = MagicMock(return_value=False)
            mock_ctx.read.return_value = mock_response.encode()
            mock_urlopen.return_value = mock_ctx

            # Should not raise
            reinitialize_state("127.0.0.1", 20551, "TestTransfer")

    def test_error_response_raises_rpc_error(self) -> None:
        """Should raise RPCError when RPC returns error."""
        mock_response = json.dumps(
            {
                "jsonrpc": "2.0",
                "error": {"code": -32000, "message": "Internal error"},
                "id": 1,
            }
        )

        with patch("urllib.request.urlopen") as mock_urlopen:
            mock_ctx = MagicMock()
            mock_ctx.__enter__ = MagicMock(return_value=mock_ctx)
            mock_ctx.__exit__ = MagicMock(return_value=False)
            mock_ctx.read.return_value = mock_response.encode()
            mock_urlopen.return_value = mock_ctx

            with pytest.raises(RPCError) as exc_info:
                reinitialize_state("127.0.0.1", 20551, "TestTransfer")

            assert "debug_reinitialize failed" in str(exc_info.value)
            assert exc_info.value.method == "debug_reinitialize"
            assert exc_info.value.port == 20551

    def test_unexpected_result_raises_rpc_error(self) -> None:
        """Should raise RPCError when result is not True."""
        mock_response = json.dumps({"jsonrpc": "2.0", "result": False, "id": 1})

        with patch("urllib.request.urlopen") as mock_urlopen:
            mock_ctx = MagicMock()
            mock_ctx.__enter__ = MagicMock(return_value=mock_ctx)
            mock_ctx.__exit__ = MagicMock(return_value=False)
            mock_ctx.read.return_value = mock_response.encode()
            mock_urlopen.return_value = mock_ctx

            with pytest.raises(RPCError) as exc_info:
                reinitialize_state("127.0.0.1", 20551, "TestTransfer")

            assert "unexpected result" in str(exc_info.value)

    def test_url_error_raises_rpc_error(self) -> None:
        """Should raise RPCError on connection failure."""
        import urllib.error

        with patch("urllib.request.urlopen") as mock_urlopen:
            mock_urlopen.side_effect = urllib.error.URLError("Connection refused")

            with pytest.raises(RPCError) as exc_info:
                reinitialize_state("127.0.0.1", 20551, "TestTransfer")

            assert "RPC failed" in str(exc_info.value)

    def test_custom_arbos_version(self) -> None:
        """Should use custom arbos_version in request."""
        mock_response = json.dumps({"jsonrpc": "2.0", "result": True, "id": 1})

        with patch("urllib.request.urlopen") as mock_urlopen:
            mock_ctx = MagicMock()
            mock_ctx.__enter__ = MagicMock(return_value=mock_ctx)
            mock_ctx.__exit__ = MagicMock(return_value=False)
            mock_ctx.read.return_value = mock_response.encode()
            mock_urlopen.return_value = mock_ctx

            reinitialize_state("127.0.0.1", 20551, "TestTransfer", arbos_version=52)

            # Verify request was made
            assert mock_urlopen.called
            call_args = mock_urlopen.call_args
            request = call_args[0][0]
            body = json.loads(request.data.decode())
            assert body["params"][0] == 52


class TestConstants:
    """Tests for module constants."""

    def test_rpc_timeout_is_positive(self) -> None:
        """RPC timeout should be positive."""
        assert RPC_TIMEOUT_S > 0

    def test_default_arbos_version(self) -> None:
        """Default ArbOS version should be set."""
        assert DEFAULT_ARBOS_VERSION == 51
