# ADR-02: The polling service is the single dispatch path

**Status:** Accepted  
**Date:** 2026-03-26

## Context

When persistence is enabled, events could be dispatched in two ways: immediately in-memory at publish time (as the non-persistent broker does), or exclusively via the store through a polling loop. A dual-path design — where fresh events are dispatched in-memory and only crash-recovered events go through the store — introduces ambiguity about which path handled an event and doubles the code paths that must be tested and reasoned about.

## Decision

When persistence is enabled, `IEventBroker` is implemented by a persistent broker that writes to the store and returns — no in-memory dispatch occurs at publish time. A background polling loop (`EventStoragePolling`) is the sole dispatch path. It continuously fetches candidate records, claims them using optimistic concurrency, and dispatches them through the in-memory handler machinery. Ack/nack/dead-letter results are written back to the store.

To reduce latency, the persistent broker signals the polling loop after a write — waking it up immediately rather than waiting for the next poll interval. This is an optimisation; the poller's correctness does not depend on it.

## Consequences

- A single dispatch path eliminates dual-path confusion — there is no ambiguity about whether an event was handled in-memory or via the store.
- Fresh events and recovered events after a crash follow exactly the same code path, making the system easier to reason about and test.
- Other instances discover available work only on their next poll interval — during event spikes, idle instances join processing with a delay up to the polling interval.
- The polling loop and handler runner currently use `Task.Factory.StartNew` with `LongRunning` rather than `IHostedService`. Integration with the ASP.NET Core hosted service lifecycle is not currently implemented. See [Operational Considerations](../EventBrokerSlim-Persistence-Architecture.md#operational-considerations-for-adopters).
