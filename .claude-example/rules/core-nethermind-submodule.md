---
paths: src/Nethermind/**/*.cs
---
# Nethermind Submodule

## CRITICAL WARNING
The `src/Nethermind` directory is a **git submodule** containing the upstream Nethermind client.

## Rules
- **DO NOT** add any arbitrum mentioning
- **DO NOT** commit changes to the submodule
- You can only modify the submodule to override or make some code generic to change particular behaviour in plugin

## Location Reminder
- Submodule: `/src/Nethermind/`
- Plugin code (editable): `/src/Nethermind.Arbitrum/`
- Plugin tests (editable): `/src/Nethermind.Arbitrum.Test/`
