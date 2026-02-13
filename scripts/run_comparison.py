#!/usr/bin/env python3
"""
Run Nethermind comparison mode and Nitro system tests.

Supports running multiple tests sequentially with per-test log directories
and a summary report.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import signal
import socket
import subprocess
import sys
import time
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import TextIO
import contextlib
import re

try:
    from eth_keys import keys
    from eth_hash.auto import keccak
    ETH_KEYS_AVAILABLE = True
except ImportError:
    ETH_KEYS_AVAILABLE = False


# =============================================================================
# Generative Config: Compute test accounts dynamically
# =============================================================================

PRECOMPUTED_ADDRESSES = {
    "Owner": "0x26E554a8acF9003b83495c7f45F06edCB803d4e3",
    "Faucet": "0xaF24Ca6c2831f4d4F629418b50C227DF0885613A",
}

DEFAULT_TEST_BALANCE = "0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF7"


def compute_test_address(name: str) -> str:
    """Compute test account address using the same algorithm as Nitro."""
    if not ETH_KEYS_AVAILABLE:
        if name in PRECOMPUTED_ADDRESSES:
            return PRECOMPUTED_ADDRESSES[name]
        raise RuntimeError(
            f"Cannot compute address for '{name}': eth_keys not installed. "
            f"Install with: pip install eth-keys eth-hash[pycryptodome]"
        )

    key_bytes = bytearray(keccak(name.encode('utf-8')))
    key_bytes[0] = 0
    private_key = keys.PrivateKey(bytes(key_bytes))
    return private_key.public_key.to_checksum_address()


def get_test_accounts(test_name: str) -> dict[str, dict]:
    """Get the accounts required for a specific test."""
    standard_accounts = ["Owner", "Faucet"]
    test_account_overrides: dict[str, list[str]] = {}
    account_names = test_account_overrides.get(test_name, standard_accounts)

    accounts = {}
    for name in account_names:
        address = compute_test_address(name)
        address_no_prefix = address[2:] if address.startswith("0x") else address
        accounts[address_no_prefix] = {"balance": DEFAULT_TEST_BALANCE}
    return accounts


def generate_accounts_json(test_name: str, output_path: Path) -> Path:
    """Generate accounts JSON file for a specific test."""
    accounts = get_test_accounts(test_name)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w") as f:
        json.dump(accounts, f, indent=2)
    return output_path


ROOT_DIR = Path(__file__).resolve().parents[1]
DEFAULT_HOST = os.environ.get("NETHERMIND_EL_HOST", "127.0.0.1")
DEFAULT_PORT = int(os.environ.get("NETHERMIND_EL_PORT", "20551"))
DEFAULT_TEST_FILE = ROOT_DIR / "scripts" / "comparison_tests.txt"
DEFAULT_NITRO_PATH = Path.home() / "GolandProjects" / "arbitrum-nitro"


class TestStatus(Enum):
    PENDING = "pending"
    RUNNING = "running"
    PASSED = "passed"
    FAILED = "failed"
    TIMEOUT = "timeout"
    SKIPPED = "skipped"


@dataclass
class TestResult:
    name: str
    status: TestStatus = TestStatus.PENDING
    exit_code: int | None = None
    duration_s: float = 0.0
    error_msg: str = ""
    log_dir: Path | None = None


@dataclass
class RunnerState:
    """Shared state for the test runner."""
    results: list[TestResult] = field(default_factory=list)
    current_test: str = ""
    current_start: float = 0.0
    build_done: bool = False
    interrupted: bool = False


def log(msg: str, level: str = "INFO"):
    """Print a timestamped log message."""
    ts = dt.datetime.now(dt.timezone.utc).strftime("%H:%M:%S")
    print(f"[{ts}] [{level}] {msg}")


def log_test_status(test_name: str, status: TestStatus, duration: float = 0.0, error: str = ""):
    """Print test status in a consistent format."""
    status_icons = {
        TestStatus.PASSED: "✓ PASS",
        TestStatus.FAILED: "✗ FAIL",
        TestStatus.TIMEOUT: "⏱ TIMEOUT",
        TestStatus.SKIPPED: "○ SKIP",
        TestStatus.RUNNING: "▶ RUN",
        TestStatus.PENDING: "○ PEND",
    }
    icon = status_icons.get(status, "?")
    duration_str = f" ({duration:.1f}s)" if duration > 0 else ""
    error_str = f" - {error}" if error else ""
    log(f"{icon}: {test_name}{duration_str}{error_str}")


class TestRunner:
    """Orchestrates test execution with Nethermind lifecycle management."""

    def __init__(self, args: argparse.Namespace, state: RunnerState, runner_log: TextIO):
        self.args = args
        self.state = state
        self.runner_log = runner_log
        self.nethermind_proc: subprocess.Popen | None = None
        self.env = self._build_env()

    def _build_env(self) -> dict:
        env = os.environ.copy()
        env["NETHERMIND_EL_HOST"] = self.args.nethermind_host
        env["NETHERMIND_EL_PORT"] = str(self.args.nethermind_port)
        if self.args.nitro_path:
            env["NITRO_PATH"] = self.args.nitro_path
        return env

    def _log(self, msg: str):
        ts = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        self.runner_log.write(f"[{ts}] {msg}\n")
        self.runner_log.flush()

    def build_nethermind(self) -> bool:
        """Build Nethermind with Arbitrum plugin."""
        cmd = [
            "dotnet", "build",
            str(ROOT_DIR / "src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj"),
            "-c", "Debug",
        ]
        self._log(f"Building: {' '.join(cmd)}")
        log("Building Nethermind.Arbitrum (Debug)...")

        proc = subprocess.run(
            cmd, cwd=str(ROOT_DIR),
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True
        )
        self.runner_log.write(proc.stdout or "")
        self.runner_log.flush()

        if proc.returncode != 0:
            self._log(f"Build failed with exit code {proc.returncode}")
            log("Build FAILED", "ERROR")
            return False

        self._log("Build completed successfully")
        log("Build completed")
        self.state.build_done = True
        return True

    def clean_db(self):
        """Clean Nethermind database."""
        config_name = "arbitrum-system-test"
        db_path = ROOT_DIR / ".data" / "nethermind_db" / config_name
        self._log(f"Cleaning DB: {db_path}")
        if db_path.exists():
            import shutil
            shutil.rmtree(db_path, ignore_errors=True)

    def generate_config(self, test_name: str, test_dir: Path) -> bool:
        """Generate Nethermind config for a specific test."""
        accounts_path = test_dir / "accounts.json"
        generate_accounts_json(test_name, accounts_path)
        self._log(f"Generated accounts for {test_name}: {accounts_path}")

        cmd = [
            "bash",
            str(ROOT_DIR / "src/Nethermind.Arbitrum/Properties/scripts/generate-system-test-config.sh"),
            "51", str(accounts_path), "arbitrum-system-test", "0x6000",
        ]
        self._log(f"Generating config: {' '.join(cmd)}")

        result = subprocess.run(cmd, cwd=str(ROOT_DIR), capture_output=True, text=True)
        if result.returncode != 0:
            self._log(f"Config generation failed: {result.stderr}")
            return False

        self._log("Config generated successfully")
        return True

    def start_nethermind(self, log_path: Path) -> bool:
        """Start Nethermind process."""
        config_name = "arbitrum-system-test"
        build_dir = ROOT_DIR / "src/Nethermind/src/Nethermind/artifacts/bin/Nethermind.Runner/debug"
        data_dir = ROOT_DIR / ".data"
        port = self.args.nethermind_port

        cmd = [
            "dotnet", "nethermind.dll",
            "-c", config_name,
            "--data-dir", str(data_dir),
            "--JsonRpc.UnsecureDevNoRpcAuthentication=true",
            f"--JsonRpc.Port={port}",
            f"--JsonRpc.AdditionalRpcUrls=http://localhost:{port}|http|nitroexecution",
            "--log", "debug",
        ]
        self._log(f"Starting Nethermind: {' '.join(cmd)}")

        log_file = log_path.open("w", encoding="utf-8")
        self.nethermind_proc = subprocess.Popen(
            cmd, cwd=str(build_dir),
            stdout=log_file, stderr=subprocess.STDOUT,
            text=True, env=self.env,
            preexec_fn=os.setsid,
        )
        return True

    def wait_for_ready(self) -> bool:
        """Wait for Nethermind RPC to become available."""
        host = self.args.nethermind_host
        port = self.args.nethermind_port
        timeout_s = self.args.timeout

        self._log(f"Waiting for Nethermind RPC on {host}:{port} (timeout {timeout_s}s)")
        deadline = time.time() + timeout_s

        while time.time() < deadline:
            if self.state.interrupted:
                return False
            try:
                with socket.create_connection((host, port), timeout=1.0):
                    self._log("Nethermind RPC is available")
                    return True
            except OSError:
                time.sleep(1)

        self._log(f"Timed out waiting for Nethermind on {host}:{port}")
        return False

    def run_test(self, test_name: str, log_path: Path) -> tuple[int, str]:
        """Run a single test."""
        nitro_path = self.env.get("NITRO_PATH") or os.environ.get("NITRO_PATH") or str(DEFAULT_NITRO_PATH)
        if not Path(nitro_path).exists():
            return 1, f"NITRO_PATH not found: {nitro_path}"

        exact_filter = f"^{re.escape(test_name)}$"
        cmd = [
            "go", "test", "./system_tests",
            "-run", exact_filter,
            "-v", "-parallel=1", "-timeout", "5m",
            "-count=1",
        ]

        test_env = self.env.copy()
        test_env["NITRO_EXECUTION_MODE"] = "comparison"
        test_env["NITRO_SECONDARY_EL_URL"] = f"http://{self.args.nethermind_host}:{self.args.nethermind_port}"
        # Suppress duplicate library warnings (macOS ld64 only)
        if sys.platform == "darwin":
            test_env["CGO_LDFLAGS"] = "-Wl,-no_warn_duplicate_libraries"

        self._log(f"Running test {test_name}: {' '.join(cmd)}")

        with log_path.open("w", encoding="utf-8") as log_file:
            proc = subprocess.Popen(
                cmd, cwd=nitro_path,
                stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                text=True, env=test_env,
            )

            suppressed = 0
            assert proc.stdout is not None
            for line in iter(proc.stdout.readline, ""):
                if self.state.interrupted:
                    proc.terminate()
                    return -1, "interrupted"
                if "ld: warning" in line:
                    suppressed += 1
                    continue
                log_file.write(line)

            proc.stdout.close()
            exit_code = proc.wait()

            if suppressed:
                self._log(f"Suppressed {suppressed} ld warnings from output")

        return exit_code, ""

    def stop_nethermind(self, grace_s: int = 10):
        """Stop Nethermind gracefully."""
        if not self.nethermind_proc or self.nethermind_proc.poll() is not None:
            return

        self._log("Stopping Nethermind (SIGTERM)")
        try:
            os.killpg(self.nethermind_proc.pid, signal.SIGTERM)
        except ProcessLookupError:
            # Process already exited, nothing to do
            return

        deadline = time.time() + grace_s
        while time.time() < deadline:
            if self.nethermind_proc.poll() is not None:
                return
            time.sleep(0.5)

        self._log("Nethermind did not exit in time; sending SIGKILL")
        try:
            os.killpg(self.nethermind_proc.pid, signal.SIGKILL)
        except ProcessLookupError:
            # Process already exited, nothing to do
            pass


def load_tests_from_file(path: Path) -> list[str]:
    """Load test names from a file, ignoring comments and blank lines."""
    tests = []
    with path.open() as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith("#"):
                tests.append(line)
    return tests


def write_summary_json(state: RunnerState, path: Path):
    """Write machine-readable summary."""
    summary = {
        "timestamp": dt.datetime.now(dt.timezone.utc).isoformat(),
        "total": len(state.results),
        "passed": sum(1 for r in state.results if r.status == TestStatus.PASSED),
        "failed": sum(1 for r in state.results if r.status == TestStatus.FAILED),
        "timeout": sum(1 for r in state.results if r.status == TestStatus.TIMEOUT),
        "skipped": sum(1 for r in state.results if r.status == TestStatus.SKIPPED),
        "tests": [
            {
                "name": r.name,
                "status": r.status.value,
                "exit_code": r.exit_code,
                "duration_s": r.duration_s,
                "error": r.error_msg,
                "log_dir": str(r.log_dir) if r.log_dir else None,
            }
            for r in state.results
        ],
    }
    with path.open("w") as f:
        json.dump(summary, f, indent=2)


def print_summary(state: RunnerState):
    """Print test summary."""
    print("\n" + "=" * 60)
    print("TEST SUMMARY")
    print("=" * 60)

    passed = sum(1 for r in state.results if r.status == TestStatus.PASSED)
    failed = sum(1 for r in state.results if r.status == TestStatus.FAILED)
    timeout = sum(1 for r in state.results if r.status == TestStatus.TIMEOUT)
    skipped = sum(1 for r in state.results if r.status == TestStatus.SKIPPED)
    total = len(state.results)

    print(f"Total: {total} | Passed: {passed} | Failed: {failed} | Timeout: {timeout} | Skipped: {skipped}")

    failed_tests = [r for r in state.results if r.status in (TestStatus.FAILED, TestStatus.TIMEOUT)]
    if failed_tests:
        print("\nFAILED TESTS:")
        for r in failed_tests:
            reason = f"exit code {r.exit_code}" if r.exit_code else r.error_msg or r.status.value
            print(f"  - {r.name}: {reason}")

    print("=" * 60)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Run Nethermind+Nitro comparison tests.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s --test-filter TestTransfer     # Run single test
  %(prog)s                                # Run all tests from curated list
  %(prog)s --fail-fast                    # Stop on first failure
  %(prog)s --test-file custom.txt         # Use custom test list
        """,
    )
    parser.add_argument("--test-filter", default="", help="Go test -run filter (single test mode)")
    parser.add_argument("--test-file", type=Path, default=DEFAULT_TEST_FILE,
                        help=f"File with test names (default: {DEFAULT_TEST_FILE})")
    parser.add_argument("--fail-fast", action="store_true", help="Stop on first test failure")
    parser.add_argument("--nitro-path", default=os.environ.get("NITRO_PATH", ""), help="Override NITRO_PATH")
    parser.add_argument("--nethermind-host", default=DEFAULT_HOST, help="Nethermind RPC host")
    parser.add_argument("--nethermind-port", type=int, default=DEFAULT_PORT, help="Nethermind RPC port")
    parser.add_argument("--timeout", type=int, default=120, help="Nethermind startup timeout (seconds)")
    parser.add_argument("--keep-nethermind", action="store_true", help="Leave Nethermind running after tests")
    parser.add_argument("--no-logs", action="store_true", help="Disable file logging")
    args = parser.parse_args()

    # Determine test list
    if args.test_filter:
        tests = [args.test_filter]
    elif args.test_file.exists():
        tests = load_tests_from_file(args.test_file)
        if not tests:
            print(f"Error: No tests found in {args.test_file}", file=sys.stderr)
            return 1
    else:
        print(f"Error: Test file not found: {args.test_file}", file=sys.stderr)
        print("Use --test-filter for single test or create a test file.", file=sys.stderr)
        return 1

    # Setup log directory
    if args.no_logs:
        log_dir = None
    else:
        ts = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%d-%H%M%S")
        log_dir = Path("/tmp") / f"nm-nitro-compare-{ts}"
        log_dir.mkdir(parents=True, exist_ok=True)

    # Initialize state
    state = RunnerState()
    for test in tests:
        state.results.append(TestResult(name=test))

    # Setup signal handler
    def signal_handler(sig, frame):
        state.interrupted = True

    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)

    class NullWriter:
        def write(self, *args, **kwargs): pass
        def flush(self): pass

    if log_dir:
        runner_log_ctx = (log_dir / "runner.log").open("w", encoding="utf-8")
    else:
        runner_log_ctx = contextlib.nullcontext(NullWriter())

    with runner_log_ctx as runner_log:
        runner = TestRunner(args, state, runner_log)

        # Build
        if not runner.build_nethermind():
            if log_dir:
                print(f"Logs: {log_dir}")
            return 1

        log(f"Running {len(tests)} test(s)...")
        print("-" * 60)

        # Run each test
        for i, result in enumerate(state.results):
            if state.interrupted:
                result.status = TestStatus.SKIPPED
                log_test_status(result.name, result.status)
                continue

            test_name = result.name
            state.current_test = test_name
            state.current_start = time.time()
            result.status = TestStatus.RUNNING

            log_test_status(test_name, TestStatus.RUNNING)

            # Per-test directory
            if log_dir:
                test_dir = log_dir / test_name
                test_dir.mkdir(exist_ok=True)
                result.log_dir = test_dir
            else:
                test_dir = Path("/tmp")
                result.log_dir = None

            try:
                # Clean DB and generate config
                runner.clean_db()

                if not runner.generate_config(test_name, test_dir):
                    result.status = TestStatus.FAILED
                    result.error_msg = "Config generation failed"
                    result.duration_s = time.time() - state.current_start
                    log_test_status(test_name, result.status, result.duration_s, result.error_msg)
                    continue

                if log_dir:
                    nethermind_log_path = test_dir / "nethermind.log"
                else:
                    nethermind_log_path = Path("/dev/null")
                runner.start_nethermind(nethermind_log_path)

                if not runner.wait_for_ready():
                    if state.interrupted:
                        result.status = TestStatus.SKIPPED
                    else:
                        result.status = TestStatus.TIMEOUT
                        result.error_msg = "Nethermind startup timeout"
                    result.duration_s = time.time() - state.current_start
                    log_test_status(test_name, result.status, result.duration_s, result.error_msg)
                    runner.stop_nethermind()
                    continue

                # Run test
                if log_dir:
                    test_log_path = test_dir / "nitro-test.log"
                else:
                    test_log_path = Path("/dev/null")
                exit_code, error_msg = runner.run_test(test_name, test_log_path)
                result.duration_s = time.time() - state.current_start
                result.exit_code = exit_code

                if state.interrupted:
                    result.status = TestStatus.SKIPPED
                elif exit_code == 0:
                    result.status = TestStatus.PASSED
                else:
                    result.status = TestStatus.FAILED
                    result.error_msg = error_msg

                log_test_status(test_name, result.status, result.duration_s, result.error_msg)

            finally:
                if not args.keep_nethermind:
                    runner.stop_nethermind()

            state.current_test = ""

            # Fail fast check
            if args.fail_fast and result.status == TestStatus.FAILED:
                log("Stopping due to --fail-fast")
                for remaining in state.results[i + 1:]:
                    remaining.status = TestStatus.SKIPPED
                break

        # Summary
        print_summary(state)

        # Write JSON summary
        if log_dir:
            write_summary_json(state, log_dir / "summary.json")

    # Print log directory
    if log_dir:
        print(f"\nLogs: {log_dir}")

    # Return exit code
    failed = sum(1 for r in state.results if r.status in (TestStatus.FAILED, TestStatus.TIMEOUT))
    return 1 if failed > 0 else 0


if __name__ == "__main__":
    sys.exit(main())
