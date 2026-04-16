# ADR-07: Escaped pipeline exceptions always dead-letter; the retry policy is not consulted

**Status:** Accepted  
**Date:** 2026-03-26

## Context

The pipeline contract guarantees that exceptions are handled within the pipeline — via an error handling delegate or `IEventHandler<TEvent>.OnError`. An escaped exception means the handler failed to honour that contract. If the retry policy were consulted after an unhandled exception, handlers could abuse the mechanism — throwing deliberately while setting `RetryRequested` to get retry behaviour without explicit error handling.

## Decision

If an exception escapes the pipeline without being handled, the event record is immediately dead-lettered. The retry policy is not consulted in this case — `RetryRequested` and `Abandoned` are ignored.

## Consequences

- Dead-lettering on escaped exceptions enforces the pipeline contract and keeps error handling explicit.
- A handler that wants retry-on-exception must implement that decision in `OnError` or an error handling delegate — it cannot rely on throwing to trigger a retry.
- Dead-lettered records are not retried automatically; monitoring and tooling for dead-letter inspection and requeue is necessary for production use.
