<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://github.com/nethermindeth/nethermind/assets/337518/3e3b3c06-9cf3-4364-a774-158e649588cc">
    <source media="(prefers-color-scheme: light)" srcset="https://github.com/nethermindeth/nethermind/assets/337518/d1cc365c-6045-409f-a961-18d22ddb2535">
    <img alt="Nethermind" src="https://github.com/nethermindeth/nethermind/assets/337518/d1cc365c-6045-409f-a961-18d22ddb2535" height="64">
  </picture>
</p>

# Arbitrum Plugin for Nethermind Ethereum client

Arbitrum Plugin - enabling execution for Arbitrum rollups

## JWT Authentication

Production deployments require JWT authentication between Nitro (consensus) and Nethermind (execution) for secure RPC communication on port 20551 (Engine API).

### Setup

1. **Start Nethermind** with a JWT secret path:
   ```bash
   # Using Makefile (defaults to ~/.arbitrum/jwt.hex)
   make run-sepolia

   # Or manually
   dotnet nethermind.dll -c arbitrum-sepolia-archive --JsonRpc.JwtSecretFile=~/.arbitrum/jwt.hex
   ```
   Nethermind auto-generates the JWT secret if the file doesn't exist.

2. **Configure Nitro** with the same secret:
   ```bash
   --node.execution-rpc-client.url=http://localhost:20551
   --node.execution-rpc-client.jwtsecret=$HOME/.arbitrum/jwt.hex
   ```

Both clients must use the same secret file.

### Custom JWT Path

Override the default path via Makefile or CLI:
```bash
# Makefile
make run-sepolia JWT_FILE=/custom/path/jwt.hex

# CLI
--JsonRpc.JwtSecretFile=/custom/path/jwt.hex
```

Or generate your own secret:
```bash
mkdir -p ~/.arbitrum && openssl rand -hex 32 > ~/.arbitrum/jwt.hex
```

### Development Mode

For local development, use the unsafe targets which disable authentication:
```bash
make run-local          # No JWT (arbitrum-local config)
make run-system-test    # No JWT (system test config)
make run-sepolia-unsafe # No JWT (Sepolia network)
make run-mainnet-unsafe # No JWT (Mainnet network)
```

**Do not use unsafe mode in production.**

### RPC Module Security

In production configs, the `arbitrum` and `nitroexecution` RPC namespaces are only available on the JWT-protected Engine port (20551), not on the public RPC port (20545). This ensures that sensitive consensus-related methods are only accessible to authenticated clients (Nitro).

| Port | Modules | Authentication |
|------|---------|----------------|
| 20545 (public) | eth, net, web3, debug, trace, txpool, etc. | None |
| 20551 (engine) | arbitrum, nitroexecution + above | JWT required |

For local development configs (`arbitrum-local`), both ports have all modules enabled with authentication disabled.