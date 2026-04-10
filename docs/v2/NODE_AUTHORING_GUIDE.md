# LucaLights v2 Node Authoring Guide

Use this guide whenever a future chat adds a new graph node.

The short version: a new node is not done until the catalog, runtime, browser editor, and docs all agree on it.

## What A Node Consists Of

Every runtime node has four layers:

1. Catalog metadata in `src/LucaLights.Core/NodeEngine/DefaultNodeTypeCatalog.cs`
2. Runtime behavior in `src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs`
3. Optional custom editor UI in `web/lucalights-ui/src/lib/components/editor/GraphNode.svelte`
4. Documentation updates in `docs/v2/`

The browser palette comes from `GET /api/node-types`, so the backend catalog is the source of truth for node names, categories, ports, and properties.

## Naming Rules

- Keep `TypeId` stable once it ships. Saved graphs persist the type ID directly.
- Use categories for editor organization, not runtime logic.
- Current editor categories are:
  - `Constants`
  - `Graph Inputs`
  - `Math`
  - `Logic`
  - `Outputs`
- If a new category is added, update `categoryHeaderTone()` in `web/lucalights-ui/src/lib/components/editor/GraphNode.svelte`.

## Port And Property Rules

- Input and output port IDs must be unique within the node type.
- Property keys must be unique within the node type.
- If a property is a fallback value for a driven input, prefer matching the property key to the input port ID.
  Example: the `Mix Color` node uses input port `factor` and property key `factor`.
- Matching keys matter because the browser editor automatically hides a property control when an input with the same ID is currently connected.

## Implementation Checklist

### 1. Add The Catalog Entry

Edit `src/LucaLights.Core/NodeEngine/DefaultNodeTypeCatalog.cs`.

Checklist:

- add the node to `_nodeTypes`
- define a `NodeTypeDefinition`
- choose the category that should appear in the palette
- define ports with clear labels and descriptions
- define fallback properties and ranges when needed

Use existing helpers:

- `Input(...)`
- `Output(...)`
- `Property(...)`

Questions to answer while doing this:

- should the node expose a configurable property when no input is connected?
- should any input allow multiple connections?
- should the node belong under `Math`, `Logic`, or a different editor category?

### 2. Implement Runtime Behavior

Edit `src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs`.

Checklist:

- add a `case` in the main `switch (node.TypeId)`
- read connected input values with the existing helpers
- read fallback properties from `node.Properties` when appropriate
- emit outputs through `RuntimeValue.FromBool`, `RuntimeValue.FromFloat`, or `RuntimeValue.FromColor`

Prefer reusing helper methods when possible:

- `GetInputBool(...)`
- `GetInputFloat(...)`
- `GetInputColor(...)`
- `ReadBool(...)`
- `ReadFloat(...)`
- `ReadColor(...)`

If the math is reusable or non-trivial, add a small private helper near the other runtime helpers.

### 3. Decide Whether The Editor Needs Special UI

Most nodes work automatically from catalog metadata. Only touch the editor when the default property controls are not good enough.

Edit `web/lucalights-ui/src/lib/components/editor/GraphNode.svelte` when you need:

- color pickers
- channel selectors
- compact chip pickers
- special numeric stepping or slider behavior
- custom property hiding rules

The default float editor already supports:

- numeric inputs
- sliders when min and max are present
- fractional steps for normalized ranges like `0..1`

### 4. Verify The Node Is Discoverable

After implementing the node:

- check `GET /api/node-types` exposes it
- confirm the palette groups it under the intended category
- confirm the handles and property editors match the port/property definitions
- confirm property fallback UI disappears when a same-key input is connected

### 5. Verify Runtime Behavior

Minimum verification for a new node:

- `dotnet build`
- `npm run check` in `web/lucalights-ui`
- `npm run build` in `web/lucalights-ui`

If the node changes graph authoring behavior, also test it in the browser by creating a small graph that proves the output changes as expected.

## Common Patterns

### Pure Math Node

Use when a node transforms numbers or colors without side effects.

Recommended shape:

- category: `Math`
- explicit typed inputs
- one output
- optional fallback property values for unconnected inputs

Examples:

- `logic.mix-color` in the `Math` category

### Boolean Decision Node

Use when a node branches between alternatives.

Recommended shape:

- category: `Logic`
- boolean condition input
- branch inputs or trigger inputs
- deterministic output selection

Examples:

- `logic.select-color`

### Output Node

Use when a node writes into device or segment state.

Recommended shape:

- category: `Outputs`
- typed input for the rendered value
- string or numeric filter properties for targets

## Files To Update Before Finishing

For a normal node slice, update:

- `docs/v2/ARCHITECTURE_NODE_ENGINE.md`
- `docs/v2/WORK_LOG.md`

Update these too if they materially change:

- `docs/v2/EXECUTION_TRACKER.md`
- `docs/v2/README.md`

## Commit Guidance

Prefer one checkpoint commit per node slice.

Good commit examples:

- `v2: add color mix node`
- `v2: add pulse timing node`
- `v2: document node authoring workflow`

If the slice includes both engine and editor work, keep them in the same commit when they are required for the node to be usable.
