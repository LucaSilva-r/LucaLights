# LucaLights v2 Node Engine

This document captures the Phase 2 node-engine direction.

## Goals

- expose node metadata to the browser through `GET /api/node-types`
- store each effect graph as a single document, not as one REST resource per node
- validate and compile whole graph snapshots before runtime evaluation
- keep reusable graph inputs separate from game-specific raw channels
- keep the browser-facing graph shape close to SvelteFlow so the UI is not forced to translate every edit

## Graph API Shape

The graph editor treats the graph as a single document on `Settings.Graph`:

- `GET /api/graph` returns the saved graph, validation result, and compiled evaluation order
- `PUT /api/graph` replaces the whole saved graph and returns validation diagnostics
- `POST /api/graph/validate` validates a proposed graph without saving it

Fine-grained per-node endpoints are intentionally deferred. SvelteFlow naturally produces a full nodes-plus-edges snapshot, and whole-graph validation makes cycle detection and port compatibility checks simpler.

## SvelteFlow Wire Shape

The real editor should treat SvelteFlow's document shape as the browser-facing graph contract:

```json
{
  "nodes": [
    {
      "id": "color-1",
      "type": "constant.color",
      "position": { "x": 0, "y": 0 },
      "data": {
        "properties": {
          "value": "#ff0000"
        }
      }
    }
  ],
  "edges": [
    {
      "id": "edge-1",
      "source": "color-1",
      "sourceHandle": "value",
      "target": "output-1",
      "targetHandle": "color"
    }
  ],
  "viewport": { "x": 0, "y": 0, "zoom": 1 }
}
```

Mapping rules:

- `node.type` maps to the backend node type ID from `GET /api/node-types`
- `node.position.x` and `node.position.y` map to the internal node editor position
- `node.data.properties` maps to node runtime properties
- `edge.source` and `edge.target` map to source and target node IDs
- `edge.sourceHandle` and `edge.targetHandle` map to source and target port IDs
- `viewport` is editor state and should be saved with the graph, but it is ignored by runtime evaluation

The C# core still keeps `NodeGraph` as a normalized compiler/runtime model with `typeId`, `connections`, and explicit port fields. The server graph endpoints adapt between SvelteFlow's document shape and the normalized model by converting:

- SvelteFlow `nodes` into internal `NodeInstance`
- SvelteFlow `edges` into internal `Connection`
- internal validation diagnostics back to IDs that SvelteFlow can highlight

This keeps the compiler free to use engine-friendly names while letting the UI send and receive the same shape that SvelteFlow stores internally.

Validation should also be split by cost:

- on drag, the UI can use SvelteFlow's `isValidConnection` hook with local catalog metadata for fast type, direction, and single-input checks
- on save or explicit validation, the UI should call `POST /api/graph/validate` so the backend remains authoritative for cycles, missing outputs, and future runtime-only constraints

## Node Catalog

The first catalog is intentionally small:

- constants: `constant.color`, `constant.float`, `constant.bool`
- reusable graph inputs: `input.bool`, `input.float`, `input.color`
- logic: `logic.select-color`
- output: `output.segment-color`

Graph inputs are graph-level keys. Binding profiles will eventually decide which game channels feed those keys. This keeps reusable graphs from hardcoding ITGMania, osu!, or any other module-specific channel names.

The browser editor now layers custom node renderers on top of this catalog:

- `constant.color` exposes an inline color picker plus RGB controls
- `input.bool`, `input.float`, and `input.color` expose channel selectors from the active module definition
- `output.segment-color` exposes inline target filters plus device, segment, and group quick-picks
- the node palette groups catalog entries by category and supports click-to-add and drag-to-add

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

The compiler returns a `CompiledNodeGraph` with validation diagnostics and a topological evaluation order.

## Runtime Evaluation

The current runtime is now live for the first node set:

- `constant.color`, `constant.float`, `constant.bool`
- `input.bool`, `input.float`, `input.color`
- `logic.select-color`
- `output.segment-color`

Runtime behavior in this slice:

- there is one unified graph on `Settings.Graph` — no effect selection needed
- settings changes trigger recompilation through `LightingManager` and `NodeGraphLightingRenderer`
- segment buffers are cleared before each frame
- `output.segment-color` fills all LEDs in matching target segments with one color
- if multiple output nodes target the same LEDs, the later node in evaluation order wins
- viewport state stays persisted for the editor, but it is ignored by runtime execution

## Next Work

- expand the node runtime beyond the bootstrap set with math, timing, gradients, and animation nodes
- integrate the Svelte build output into `LucaLights.Server/wwwroot`
- decide whether editor preview should be able to render while no input module is active
