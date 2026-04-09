# LucaLights v2 Execution Tracker

## Status Summary

- Overall status: `in progress`
- Current phase: `Phase 1 - Server Host`
- Current implementation slice: `REST API baseline complete, WebSocket/event streams next`
- Last updated: `2026-04-09`

## Milestones

| Milestone | Status | Exit Criteria |
|---|---|---|
| Docs scaffold | done | Repo contains plan, tracker, architecture note, and work log |
| Phase 0.1 - New solution skeleton | done | `src/LucaLights.Core` and `src/LucaLights.Server` exist and build |
| Phase 0.2 - Core extraction | done | Lighting/device/settings code builds without Avalonia dependencies |
| Phase 0.3 - Input module foundation | done | `IGameInputModule`, `GameInputManager`, `InputSnapshot`, and `InputDefinition` are implemented |
| Phase 0.4 - ITG parity module | done | Current ITGMania named-pipe behavior works through `ITGManiaInputModule` |
| Phase 1 - Server host | in progress | ASP.NET Core host serves APIs, static UI assets, and WebSocket endpoints |
| Phase 2 - Node engine | not started | Effects can render from compiled node graphs |
| Phase 3 - SvelteKit UI | not started | Browser UI supports device management, preview, and graph editing |
| Phase 4 - Packaging and polish | not started | Fresh v2 config works, publish flow works, and lifecycle controls are complete |

## Phase Breakdown

### Phase 0 - Foundation

Status: `done`

Scope:

- create the new project layout under `src/`
- extract engine/runtime code from Avalonia
- introduce the game-agnostic input abstraction before more features land
- keep existing ITGMania behavior working through the first module

Recommended order:

1. Create the new solution/project skeleton. Done on `2026-04-08`.
2. Move simple shared models first: `Color`, `Segment`, `Device`, `Settings`.
3. Extract `LightingManager` behind injected dependencies.
4. Introduce `IGameInputModule` and `GameInputManager`.
5. Port current `GameState` and `PipeManager` logic into `ITGManiaInputModule`.

Completed in this phase so far:

- added `src/LucaLights.Core`
- added `src/LucaLights.Server`
- added the projects to [Luca Lights.sln](../../Luca%20Lights.sln)
- verified `dotnet build`
- extracted portable core models: `Segment`, `Device`, `Settings`, `Effect`, `NodeGraph`
- extracted portable core persistence: `ConfigManager`
- extracted portable core transport senders: DDP and UDP Realtime
- extracted portable core render loop: `LightingManager`, renderer interface, frame context, options, and event hooks
- added generic game-input foundation: `InputSnapshot`, `InputDefinition`, `IGameInputModule`, and `GameInputManager`
- connected `LightingManager` to real input activity state and carried `InputSnapshot` through `LightingFrameContext`
- added `ITGManiaInputModule` with named pipe or FIFO reading and raw channel definitions
- captured graph input and binding-profile design for future graph reuse

### Phase 1 - Server Host

Status: `in progress`

Exit criteria:

- engine hosted by ASP.NET Core
- REST APIs for devices, effects, settings, and input modules
- preview and event WebSockets working locally

Completed in this phase so far:

- added `EngineHostedService` to own runtime startup and shutdown
- registered `ConfigManager`, `GameInputManager`, `LightingManager`, and the temporary no-op renderer in ASP.NET Core DI
- registered and started `ITGManiaInputModule` from v2 settings
- added diagnostic endpoints for root status, system status, input module definitions, and latest input state
- fixed ITGMania input definition initialization so server endpoints can enumerate channels safely
- added a bounded ITGMania shutdown wait so blocked FIFO reads do not hang server shutdown
- added stable v2 device and segment IDs for browser-safe REST routes
- added REST CRUD for devices, nested segments, and effects
- added `GET /api/settings` and `PUT /api/settings` backed by the shared server settings instance
- routed settings mutations through `LightingManager.SyncRoot`, dirty-state marking, and `ConfigManager.Save()`

### Phase 2 - Node Engine

Status: `not started`

Exit criteria:

- node graph schema implemented
- graph compilation with cycle detection
- runtime evaluation wired into `Effect.Render()`

### Phase 3 - Web UI

Status: `not started`

Exit criteria:

- SvelteKit app integrated into server
- device/effect management available in browser
- node editor consumes input definitions dynamically

### Phase 4 - Packaging and Polish

Status: `not started`

Exit criteria:

- fresh v2 config works without legacy migration support
- default module is `ITGManiaInputModule` for fresh v2 setups
- build and publish flow documented and repeatable

## Active Risks

- Current codebase is heavily coupled to Avalonia singletons and UI thread dispatching.
- Graph serialization can become brittle if node properties are left as unversioned `object` blobs.
- Runtime graph swaps must be thread-safe so the render loop never sees partially compiled state.
- "Game agnostic" can regress into "ITG with abstraction wrappers" unless module boundaries stay strict.

## Decisions Locked In

- The core engine remains in C#.
- Game-specific integrations are modules, not hardcoded branches in the lighting engine.
- The first module is `ITGManiaInputModule` to preserve current behavior.
- The browser UI is a consumer of backend-defined input channels, not a source of game-specific assumptions.

## Next Recommended Slice

Continue `Phase 1 - Server Host`.

Concrete target:

- add WebSocket plumbing for preview frames and runtime events
- expose config/input changes as server-side events that the future browser UI can subscribe to
- keep the first pass compatible with the temporary no-op renderer

Suggested checkpoint commit:

- `v2: add server event streams`
