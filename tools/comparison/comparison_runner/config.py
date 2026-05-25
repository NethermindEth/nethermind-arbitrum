"""Configuration settings for the comparison test runner.

Uses Pydantic for type-safe, validated configuration with environment variable support.
All constants use typing.Final for immutability guarantees.

Note: Compatible with both Pydantic v1 and v2.
"""

from __future__ import annotations

import threading
from pathlib import Path
from typing import Final

from pydantic import BaseModel, ConfigDict, Field, field_validator

MEMORY_PER_WORKER_GB: Final[float] = 2.5
"""Memory required per Nethermind worker instance in GB.

Breakdown based on profiling:
- ~1.5 GB baseline Nethermind process
- ~0.7 GB state growth during test execution
- ~0.3 GB GC headroom buffer (~12%)

Rounded up to 2.5 GB for safety margin across test variability.
"""

SYSTEM_RESERVE_GB: Final[float] = 4.0
"""Memory reserved for system processes outside worker pool in GB.

Breakdown:
- ~1.0 GB OS kernel, services, and file cache
- ~1.5 GB Go runtime for Nitro test processes (can spike during compilation)
- ~1.0 GB pytest orchestration and subprocesses
- ~0.5 GB safety buffer for memory pressure spikes

This reserve ensures workers don't starve system processes and trigger OOM.
"""

MAX_WORKERS: Final[int] = 16
"""Maximum number of parallel workers regardless of available resources.

Rationale:
- RAM ceiling: 16 workers × 2.5 GB = 40 GB + 4 GB reserve = 44 GB total
- Port range: 16 sequential ports easily fit in unprivileged range
- Diminishing returns: beyond 16 workers, lock contention and I/O saturation
  reduce per-test throughput gains
- CI alignment: GitHub Actions runners have 7 GB RAM (~2 workers typical)

Most development machines (16-32 GB) will hit memory limits before this cap.
"""

MIN_PORT: Final[int] = 1024
"""Minimum valid port number (unprivileged ports start here)."""

MAX_PORT: Final[int] = 65535
"""Maximum valid port number."""

DEFAULT_BASE_PORT: Final[int] = 20551
"""Default base port for Nethermind instances."""

DEBUG_PORT_OFFSET: Final[int] = 8000
"""Offset from base port to debug RPC port (debug_reinitialize lives here)."""

# Note: Nitro WebSocket ports are now dynamically allocated (WSPort=0 with URL="self")
# See system_tests/common_test.go applyExecutionMode() for details.

DEFAULT_TIMEOUT_S: Final[int] = 300
"""Default test timeout in seconds (5 minutes)."""

DEFAULT_STARTUP_TIMEOUT_S: Final[int] = 60
"""Default Nethermind startup timeout in seconds."""

DEFAULT_MAX_RETRIES: Final[int] = 3
"""Default number of retries for failed tests. 0 = no retries."""


PRECOMPUTED_ADDRESSES: Final[dict[str, str]] = {
    "Owner": "0x26E554a8acF9003b83495c7f45F06edCB803d4e3",
    "Faucet": "0xaF24Ca6c2831f4d4F629418b50C227DF0885613A",
}
"""Pre-computed test account addresses matching Nitro's test setup."""

DEFAULT_TEST_BALANCE: Final[str] = (
    "0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF7"
)
"""Default balance for test accounts (near max uint256)."""


class ComparisonConfig(BaseModel):
    """Configuration for the comparison test runner.

    Supports environment variable loading via Pydantic's settings management.
    All values are validated on creation.

    Example:
        >>> config = ComparisonConfig(base_port=20551, max_workers=4)
        >>> config.base_port
        20551
    """

    base_port: int = Field(
        default=DEFAULT_BASE_PORT,
        ge=0,  # 0 = ephemeral port allocation
        le=MAX_PORT,
        description="Base port for Nethermind instances (0 = ephemeral allocation)",
    )
    # Note: Nitro WebSocket ports are dynamically allocated by Go (WSPort=0 with URL="self")
    # Python only configures Nethermind ports; Nitro handles its own port allocation.
    max_workers: int = Field(
        default=MAX_WORKERS,
        ge=1,
        le=MAX_WORKERS,
        description="Maximum number of parallel workers",
    )
    test_timeout_s: int = Field(
        default=DEFAULT_TIMEOUT_S,
        ge=10,
        le=3600,
        description="Test execution timeout in seconds",
    )
    startup_timeout_s: int = Field(
        default=DEFAULT_STARTUP_TIMEOUT_S,
        ge=10,
        le=300,
        description="Nethermind startup timeout in seconds",
    )
    nitro_path: Path | None = Field(
        default=None,
        description="Path to Nitro repository (uses NITRO_PATH env if not set)",
    )
    nethermind_path: Path | None = Field(
        default=None,
        description="Path to Nethermind repository",
    )
    verbose: bool = Field(
        default=False,
        description="Enable verbose logging output",
    )

    model_config = ConfigDict(frozen=True, extra="forbid")

    @field_validator("base_port")
    @classmethod
    def validate_port_unprivileged(cls, v: int) -> int:
        """Ensure port is in the unprivileged range or zero for ephemeral."""
        if v == 0:
            # Special case: 0 means ephemeral port allocation
            return v
        if v < MIN_PORT:
            raise ValueError(
                f"Port {v} is privileged (< {MIN_PORT}). "
                "Use a port >= 1024, or 0 for ephemeral allocation."
            )
        return v

    def get_worker_port(self, worker_id: int) -> int:
        """Calculate the Nethermind RPC port for a specific worker.

        Args:
            worker_id: Zero-based worker index

        Returns:
            Port number for this worker (0 for ephemeral allocation)

        Raises:
            ValueError: If the calculated port exceeds MAX_PORT
        """
        if self.base_port == 0:
            # Ephemeral port allocation - each worker gets port 0
            return 0
        port = self.base_port + worker_id
        if port > MAX_PORT:
            raise ValueError(f"Worker {worker_id} port {port} exceeds maximum {MAX_PORT}")
        return port

    def validate_worker_ports(self, num_workers: int) -> None:
        """Validate that all worker ports are valid.

        Args:
            num_workers: Number of workers to validate

        Raises:
            ValueError: If any port would be invalid
        """
        if self.base_port == 0:
            # Ephemeral allocation - no port range validation needed
            return
        last_port = self.base_port + num_workers - 1
        if last_port > MAX_PORT:
            raise ValueError(
                f"Configuration would use ports {self.base_port}-{last_port}, "
                f"but maximum port is {MAX_PORT}"
            )


class PortAllocator:
    """Thread-safe port allocator with sequential assignment and overflow for restarts.

    Normal operation assigns sequential ports: base, base+1, base+2, ...
    When a worker crashes and needs restart, its original port may be in TIME_WAIT.
    The overflow mechanism provides fresh ports beyond the initial range.

    Example:
        >>> allocator = PortAllocator(base_port=20551, num_workers=4)
        >>> allocator.initial_port(0)  # 20551
        >>> allocator.initial_port(1)  # 20552
        >>> allocator.overflow_port()  # 20555 (first port after initial range)
        >>> allocator.overflow_port()  # 20556 (next crash gets next port)
    """

    def __init__(self, base_port: int, num_workers: int) -> None:
        """Initialize the port allocator.

        Args:
            base_port: Starting port for worker 0
            num_workers: Number of workers (determines initial range)
        """
        self.base_port = base_port
        self.num_workers = num_workers
        self._high_water_mark = base_port + num_workers - 1
        self._lock = threading.Lock()

    def initial_port(self, worker_id: int) -> int:
        """Get the initial port for a worker (sequential assignment).

        Args:
            worker_id: Zero-based worker index

        Returns:
            Port number for this worker

        Raises:
            ValueError: If worker_id is out of range
        """
        if worker_id < 0 or worker_id >= self.num_workers:
            raise ValueError(f"worker_id must be in range [0, {self.num_workers}), got {worker_id}")
        return self.base_port + worker_id

    def overflow_port(self, max_probe: int = 50) -> int:
        """Get the next available overflow port for crash recovery.

        Thread-safe. Each call advances the high water mark and probes
        for an available port starting from that position.

        Args:
            max_probe: Maximum ports to try before raising an error

        Returns:
            An available port beyond the initial worker range

        Raises:
            RuntimeError: If no available port found within max_probe attempts
        """
        import socket

        with self._lock:
            start_port = self._high_water_mark + 1

            for offset in range(max_probe):
                port = start_port + offset
                if port > MAX_PORT:
                    break
                try:
                    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                        s.bind(("127.0.0.1", port))
                        self._high_water_mark = port
                        return port
                except OSError:
                    continue

            raise RuntimeError(
                f"No available overflow port found in range "
                f"{start_port}-{start_port + max_probe - 1}"
            )

    @property
    def high_water_mark(self) -> int:
        """Current high water mark (highest allocated port)."""
        with self._lock:
            return self._high_water_mark
