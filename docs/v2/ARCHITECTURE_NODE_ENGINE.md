# LucaLights v2 Node Engine

This document captures the Phase 2 node-engine direction.

## Goals

- expose node metadata to the browser through `GET /api/node-types`
- store each effect graph as a single document, not as one REST resource per node
- validate and compile whole graph snapshots before runtime evaluation
- keep reusable graph inputs separate from game-specific raw channels

## Graph API Shape

The graph editor should treat the effect graph as a document:

- `GET /api/effects/{effectId}/graph` returns the saved graph, validation result, and compiled evaluation order
- `PUT /api/effects/{effectId}/graph` replaces the whole saved graph and returns validation diagnostics
- `POST /api/effects/{effectId}/graph/validate` validates a proposed graph without saving it

Fine-grained per-node endpoints are intentionally deferred. SvelteFlow naturally produces a full nodes-plus-edges snapshot, and whole-graph validation makes cycle detection and port compatibility checks simpler.

## Node Catalog

The first catalog is intentionally small:

- constants: `constant.color`, `constant.float`, `constant.bool`
- reusable graph inputs: `input.bool`, `input.float`, `input.color`
- logic: `logic.select-color`
- output: `output.segment-color`

Graph inputs are graph-level keys. Binding profiles will eventually decide which game channels feed those keys. This keeps reusable graphs from hardcoding ITGMania, osu!, or any other module-specific channel names.

## Validation

`NodeGraphCompiler` currently validates:

- required and unique node IDs
- required and unique connection IDs
- known node type IDs
- known source and target nodes
- known source output and target input ports
- exact port value-type compatibility
- one connection per input port unless a port allows multiple connections
- self-connections and graph cycles
- missing output nodes as warnings

The compiler returns a `CompiledNodeGraph` with validation diagnostics and a topological evaluation order. Runtime evaluation is intentionally not implemented in this slice.

## Next Work

- add runtime node evaluation primitives
- define how output nodes write into device segment buffers
- add an active-effect selection model if multiple saved effects can exist
- replace the temporary `NoOpLightingRenderer` with a node-graph renderer once compilation and evaluation are stable
