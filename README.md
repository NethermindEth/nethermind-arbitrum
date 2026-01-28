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

Production deployments require JWT authentication between Nitro (consensus) and Nethermind (execution) for secure RPC communication.

### Setup

1. **Start Nethermind** - it auto-generates a JWT secret at `keystore/jwt-secret` on first run

2. **Configure Nitro** with the same secret:
   ```bash
   --node.execution-rpc-client.url=http://localhost:20551
   --node.execution-rpc-client.jwtsecret=/path/to/nethermind/keystore/jwt-secret
   ```

Both clients must use the same secret file.

### Custom JWT Path

To use a custom JWT secret path, configure Nethermind:
```bash
--JsonRpc.JwtSecretFile=/custom/path/jwt-secret
```

Or generate your own secret:
```bash
openssl rand -hex 32 > /path/to/jwt-secret
```

### Development Mode

For local development, `arbitrum-local.json` has authentication disabled via `UnsecureDevNoRpcAuthentication: true`. **Do not use this setting in production.**