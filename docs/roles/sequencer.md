# Sequencer Role

> **Maturity: Experimental.** The sequencer role lets Nethermind produce blocks for Arbitrum chains, replacing Nitro's internal Geth-based sequencer. The role is functional end-to-end on local development chains — including Timeboost express-lane auctions and conditional transactions (EIP-7796) — but has not been exercised in production on a public chain. Operators piloting the sequencer role should expect rough edges, ongoing config churn, and the need to coordinate closely with Nitro's deployment specifics.

In this role, **users send transactions directly to Nethermind** (via `eth_sendRawTransaction`); Nethermind queues them, and on each `startSequencing` call from Nitro produces a block from the queue. Nitro then broadcasts the produced block to the feed and posts batches to L1.

For the protocol-level explanation of how Arbitrum sequencing, batching, and Timeboost work, see [Arbitrum docs on the sequencer](https://docs.arbitrum.io/how-arbitrum-works/deep-dives/sequencer) and [Timeboost](https://docs.arbitrum.io/how-arbitrum-works/timeboost/gentle-introduction).

## When to choose this role

Pick the sequencer role if you:

- **Operate an L3 / Orbit chain** and want Nethermind to be the sequencer's execution layer.
- **Are evaluating Nethermind's sequencer performance** against the in-Nitro Geth-based path.
- **Are developing on the Nitro testnode** and want to use Nethermind for the chain's sequencer.

Skip the sequencer role for any production deployment on Arbitrum One or Arbitrum Sepolia — those chains have established sequencer operators and the public chain interfaces (feeds, batch posting cadence) are not what this role targets.

## Prerequisites

Everything from [external execution prerequisites](external-execution.md#prerequisites), plus:

- **Nitro running with `--node.sequencer=false`.** Nitro must be configured to defer sequencing to the external execution client.
- **Nitro testnode** for local experimentation. Production sequencer setups require chain-specific deployment that is beyond the scope of this doc.
- **Timeboost auction contract addresses** if you enable Timeboost ([`TimeboostEnabled`](../configuration.md#timeboost-enabled)). The shipped `arbitrum-local-sequencer` config has the testnode's deployed addresses pre-populated.

## Configuration walkthrough

The shipped entry point is `arbitrum-local-sequencer`, which targets the Nitro testnode:

```jsonc
{
  "Arbitrum": {
    "SequencerEnabled": true,
    "SequencerAwaitTxResult": false,

    "TimeboostEnabled": true,
    "TimeboostAuctionContractAddress": "0x7DD3F2a3fAeF3B9F2364c335163244D3388Feb83",
    "TimeboostAuctioneerAddress": "0x46225F4cee2b4A1d506C7f894bb3dAeB21BF1596",
    "TimeboostRoundDurationSeconds": 60,
    "TimeboostAuctionClosingWindowSeconds": 15,
    "TimeboostExpressLaneAdvantageMs": 200,
    "TimeboostQueueTimeoutInBlocks": 5
  }
}
```

[`SequencerEnabled`](../configuration.md#sequencer-enabled) wires up the `ArbitrumSequencerModule` (transaction queues, sequencer engine, nonce caches). All `Sequencer*` and `Timeboost*` settings then apply.

### `SequencerAwaitTxResult`

When `false`, `eth_sendRawTransaction` returns as soon as the transaction is queued — fast for clients but the response does not confirm the tx will be included.

When `true`, the call blocks until the tx has been sequenced into a block — slower for clients but provides immediate ordering confirmation.

Default `false`. Use `true` only when downstream clients need the confirmation guarantee.

### Timeboost configuration

If you enable [`TimeboostEnabled`](../configuration.md#timeboost-enabled), [`TimeboostAuctionContractAddress`](../configuration.md#timeboost-auction-contract-address) is **required** — startup fails if it's empty. The contract address must match the deployed `ExpressLaneAuction` proxy on your chain.

Timeboost adds a non-trivial flow: an `ExpressLaneTracker` polls the auction contract every [`TimeboostAuctionContractPollIntervalMs`](../configuration.md#timeboost-auction-contract-poll-interval-ms); during a round, the controller's express-lane submissions bypass the queue delay applied to other transactions ([`TimeboostExpressLaneAdvantageMs`](../configuration.md#timeboost-express-lane-advantage-ms)).

For your own chain, replace the testnode addresses with the deployments on your chain.

### Sender whitelist

If you need to restrict who can submit transactions (e.g. permissioned chains), use [`SequencerSenderWhitelist`](../configuration.md#sequencer-sender-whitelist) as a comma-separated list of addresses. Empty (default) means everyone is allowed.

## Running the role

For the time being, there is no turnkey production deployment recipe for the sequencer role. The guide will be provided after Nitro v3.10.0 is released.

## Known issues / limitations

- **Not deployed in production.** The path is functional and tested on the Nitro testnode, but no production Arbitrum chain currently runs Nethermind as its sequencer. Treat the role as experimental.
- **Hot failover (`forwardTo`) is plumbed but not battle-tested.** The mechanism exists ([`forwardTo`](../rpc-api.md#forward-to)) and forwards `eth_sendRawTransaction` to a backup sequencer URL via HTTP. Operational scenarios beyond a planned switchover have not been exercised.
- **`SequencerMaxBlockSpeedMs` interacts with Nitro's broadcast cadence.** The default 250 ms matches Arbitrum's slot time. Lowering it without coordinating Nitro's batch-poster timing can produce odd batch-fill patterns.
- **Conditional transaction (EIP-7796) state checks are evaluated against current head state, not block-inclusion state.** This matches Nitro's behavior but means `KnownAccounts` conditions can pass at submission and fail at inclusion if the underlying state shifted between. This is by design.
- **`TimeboostAuctionContractAddress` defaults silently to empty.** If you set [`TimeboostEnabled: true`](../configuration.md#timeboost-enabled) without populating the address, startup fails — this is good — but copy-pasting the local-sequencer config without updating the addresses for your chain will quietly use the testnode's contracts.
- **Block-production semaphore never waits.** "CreateBlock mutex held" errors mean Nitro is calling `startSequencing` while a previous block is still being assembled. In the sequencer role this typically points at slow EVM execution or a stalled Stylus FFI call.
