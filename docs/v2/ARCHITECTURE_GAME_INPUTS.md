# Game-Agnostic Input Architecture

## Goal

LucaLights should not know about ITGMania, osu!, or any other game at the core-engine level.

Instead, game-specific integrations should act as modules that publish normalized inputs into the lighting runtime. Graphs consume those normalized inputs by channel key.

## Core Idea

The lighting engine consumes an `InputSnapshot` each frame.

That snapshot is produced by the active `IGameInputModule`.

The editor discovers the available channels from an `InputDefinition` exposed by the same module.

This gives us:

- a stable runtime contract for the engine
- dynamic, module-driven input options in the editor
- room for timing and metadata beyond simple button presses

## Responsibilities

### Core Engine

Owns:

- LED rendering
- effect evaluation
- graph compilation
- device transport
- config persistence

Must not know:

- named pipe formats
- ITGMania enum names
- osu! score APIs
- per-game protocol details

### Game Input Module

Owns:

- connecting to a specific game or data source
- parsing raw protocol/state
- mapping source-specific events to normalized channels
- exposing channel definitions to the editor and API

Must not own:

- LED rendering
- DDP/UDP sending
- graph compilation
- device topology

## Proposed Contracts

### `IGameInputModule`

Suggested responsibilities:

- start and stop its integration lifecycle
- publish latest input snapshot
- expose static channel definitions
- expose health and connection state
- optionally accept debug overrides for testing

Conceptually:

```csharp
public interface IGameInputModule
{
    string ModuleId { get; }
    string DisplayName { get; }
    InputDefinition GetDefinition();
    InputSnapshot GetLatestSnapshot();
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
```

### `InputDefinition`

Describes the channels the module makes available to the graph editor.

Each channel should include:

- stable key
- display label
- value type: `bool`, `float`, `color`, `string`
- optional range/default metadata
- category and description for editor grouping

Example channel keys:

- `timing.song_time`
- `timing.beat`
- `timing.bpm`
- `buttons.menu_left`
- `cabinet.marquee_left`
- `players.p1.combo`

### `InputSnapshot`

Represents the latest normalized values for the current frame.

This should be easy to read from the render loop, cheap to copy or swap, and safe to publish atomically.

Suggested shape:

- timestamp or frame counter
- connection/module health state
- dictionary or indexed storage for `bool` channels
- dictionary or indexed storage for `float` channels
- dictionary or indexed storage for `color` channels
- metadata payload for UI/debug use

## Runtime Flow

1. The active module starts.
2. It parses source-specific input and updates its latest `InputSnapshot`.
3. `GameInputManager` exposes the latest snapshot to the engine and server APIs.
4. The render loop reads the latest stable snapshot once per frame.
5. The compiled graph evaluates per LED using that snapshot plus LED position/time.
6. The frontend uses the module's `InputDefinition` to populate input-node selectors.

## Why This Matters

Without this boundary, the node system will still be shaped around ITGMania concepts like cabinet lights and menu buttons.

With this boundary, LucaLights becomes:

- reusable across games
- extensible without rewriting the engine
- easier to test with fake or recorded input streams

## First Module: `ITGManiaInputModule`

The first implementation should be a compatibility module built from the existing:

- [`LTEK ULed/Code/GameState.cs`](../../LTEK%20ULed/Code/GameState.cs)
- [`LTEK ULed/Code/PipeManager.cs`](../../LTEK%20ULed/Code/PipeManager.cs)

Its job is not to preserve the old API shape. Its job is to preserve behavior while translating ITGMania signals into normalized channels.

## Open Design Questions

- Should channel keys be globally flat (`timing.beat`) or grouped with explicit namespaces and categories?
- Should `InputSnapshot` use dictionaries first for simplicity, then compiled channel indices later for performance?
- How much metadata should be runtime-visible to nodes versus UI-only?
- Should multiple modules ever run at once, or should v2 start with exactly one active module?

For now, the safest assumption is:

- one active module at a time
- human-readable channel keys
- definitions discovered over API
- snapshots optimized later if profiling requires it
