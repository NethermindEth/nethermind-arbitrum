# Nethermind Arbitrum

This project is an implementation of the Arbitrum execution client in C# as a plugin for the Nethermind Ethereum client.

## Development Commands

```bash
# Clean the project
dotnet clean src/Nethermind.Arbitrum.slnx

# Build the project
dotnet build src/Nethermind.Arbitrum/Nethermind.Arbitrum.csproj

# Run tests (use --filter to run specific tests)
dotnet test src/Nethermind.Arbitrum.Test/Nethermind.Arbitrum.Test.csproj
```

## Business Context

Arbitrum is a Layer 2 scaling solution for Ethereum that uses optimistic rollups to increase transaction throughput and reduce fees. Arbitrum already has execution and consensus client implementation in Go (Nitro). OffchainLabs is supporting development of a Nethermind-based execution client to provide more diversity and choice in the Ethereum ecosystem. Also, Nethermind's client is known for its performance and modularity, making it a strong candidate for Arbitrum's execution layer.

## Project Structure

Source code of Nethermind client is connected to the plugin repository via git submodule located at `/src/Nethermind`. The main Arbitrum plugin code is located in `/src/Nethermind.Arbitrum/` and its tests are in `/src/Nethermind.Arbitrum.Test/`.
As an AI, you can also have access to Nitro source code and set of full chain simulation scripts that allow to provision test environment. If access is not available, request it from the user.
