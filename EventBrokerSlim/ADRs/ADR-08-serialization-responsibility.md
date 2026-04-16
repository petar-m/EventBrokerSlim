# ADR-08: Serialization is the responsibility of IEventStorage implementations

**Status:** Accepted  
**Date:** 2026-03-26

## Context

`EventBrokerSlim` is AOT-compatible. A shared JSON serializer based on reflection would break AOT-compatible deployments. Beyond AOT, serialization format is legitimately a backend concern — a Redis backend may prefer MessagePack, a PostgreSQL backend may prefer JSON, a custom backend may use Protobuf or stream large payloads differently.

## Decision

The core library does not provide a shared serializer. Each `IEventStorage` implementation is responsible for serializing and deserializing event payloads in whatever format suits the backend. `EventRegistry` is passed to `TryClaimAsync` so the backend can resolve CLR types from event names during deserialization.

## Consequences

- The core library remains free of serialization dependencies.
- Each backend can make the serialization choice that best fits its constraints, including AOT compatibility.
- All event types participating in persistence must be serializable — this is a new requirement that does not exist in the in-memory-only version of the library.
