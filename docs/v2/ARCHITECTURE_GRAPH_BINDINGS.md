# Graph Inputs And Bindings

## Goal

Graphs should not usually hardcode game-specific channel keys directly.

Instead, the graph should read graph-defined inputs, and a binding profile should map one or more module channels into those inputs.

This keeps graphs reusable while still allowing game-specific graphs when needed.

## Three Layers

### 1. Module Channels

These are the raw or semantic channels exposed by an input module.

Examples:

- `raw.itgmania.cabinet.marquee_up_left`
- `raw.itgmania.button.menu_left`
- `raw.osu_mania.lane_1_pressed`
- `raw.taiko.left_don`

These are owned by the input module.

### 2. Graph Inputs

These are abstract inputs declared by the graph itself.

Examples:

- `AccentLeft`
- `PrimaryPulse`
- `Kick`
- `BackgroundEnergy`
- `RedTrigger`

The graph consumes these inputs, not the game module directly.

### 3. Binding Profiles

A binding profile maps one or more module channels to one graph input.

Example:

- graph input: `RedTrigger`
- sources:
  - `raw.itgmania.cabinet.marquee_up_left`
  - `raw.osu_mania.lane_1_pressed`
- merge mode: `or`

That means the same graph input can be driven by different games or modes without editing the graph itself.

## Why This Is Better Than A Giant Game-State Node

A giant per-game node is useful as a convenience or debug view, but it should not be the main graph contract.

Reasons:

- dynamic per-game node shapes are harder to serialize safely
- they get very large and noisy fast
- they make cross-game reuse harder
- they push too much game-specific structure into the graph runtime

Use a giant grouped node only as editor sugar if needed.

## Reuse Model

There should be two kinds of graphs:

- portable graphs
- game-specific graphs

Portable graphs:

- consume graph inputs
- rely on binding profiles
- can be reused across games or modes if equivalent bindings exist

Game-specific graphs:

- consume raw module channels directly
- are intentionally tied to one module or one mode
- are still valid and useful

Both should be supported.

## Editor Strategy

The editor should not contain one hardcoded node per possible channel in the system.

Preferred approach:

- generic input nodes such as `Input Bool`, `Input Float`, `Input Color`, `Input Pulse`
- each generic input node can target either:
  - a graph input
  - a raw module channel for module-specific graphs

Optional helper nodes can be generated per module for convenience.

Examples:

- `ITG Cabinet`
- `ITG Buttons`
- `osu!mania Lane`
- `Taiko Hit`

These should be convenience wrappers over channels, not special runtime concepts.

## Merge Modes

When multiple module channels feed one graph input, the binding profile needs a merge strategy.

Recommended defaults:

- `bool`: `or`
- `pulse`: `or`
- `float`: `max`
- `color`: `priority` or `blend`

Future versions can expand this, but the initial system should define merge explicitly rather than assuming a single source forever.

## Semantic vs Raw

The binding system should leave room for both:

- `raw.*` channels for exact module data
- `semantic.*` channels for portable higher-level meanings

Examples:

- `raw.itgmania.cabinet.marquee_up_left`
- `semantic.ui.navigate_left`
- `semantic.player.primary_1`

The first shipping version does not need to solve semantic mapping fully.

What matters now is that the graph architecture leaves space for it.

## Practical Example

Graph:

- input: `RedTrigger`
- effect: when `RedTrigger` is true, output red

Binding profile A:

- `RedTrigger <- raw.itgmania.cabinet.marquee_up_left`

Binding profile B:

- `RedTrigger <- raw.osu_mania.lane_1_pressed`

Same graph, different game bindings.

## Short-Term Plan

When the graph system is implemented:

1. Add graph-defined input declarations to the graph model.
2. Add binding profiles that map module channels to graph inputs.
3. Keep raw channel access available for game-specific graphs.
4. Treat any large module-specific state node as optional editor convenience only.
