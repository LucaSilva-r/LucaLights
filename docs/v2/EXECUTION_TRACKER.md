# LucaLights v2 Execution Tracker

## Status Summary

- Overall status: `in progress`
- Current phase: `Phase 0 - Foundation`
- Current implementation slice: `Phase 0.2 model/config baseline extracted`
- Last updated: `2026-04-08`

## Milestones

| Milestone | Status | Exit Criteria |
|---|---|---|
| Docs scaffold | done | Repo contains plan, tracker, architecture note, and work log |
| Phase 0.1 - New solution skeleton | done | `src/LucaLights.Core` and `src/LucaLights.Server` exist and build |
| Phase 0.2 - Core extraction | in progress | Lighting/device/settings code builds without Avalonia dependencies |
| Phase 0.3 - Input module foundation | not started | `IGameInputModule`, `GameInputManager`, `InputSnapshot`, and `InputDefinition` are implemented |
| Phase 0.4 - ITG parity module | not started | Current ITGMania named-pipe behavior works through `ITGManiaInputModule` |
| Phase 1 - Server host | not started | ASP.NET Core host serves APIs, static UI assets, and WebSocket endpoints |
| Phase 2 - Node engine | not started | Effects can render from compiled node graphs |
| Phase 3 - SvelteKit UI | not started | Browser UI supports device management, preview, and graph editing |
| Phase 4 - Packaging and polish | not started | Fresh v2 config works, publish flow works, and lifecycle controls are complete |

## Phase Breakdown

### Phase 0 - Foundation

Status: `in progress`

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

### Phase 1 - Server Host

Status: `not started`

Exit criteria:

- engine hosted by ASP.NET Core
- REST APIs for devices, effects, settings, and input modules
- preview and event WebSockets working locally

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

Start `Phase 0.2 - Core extraction`.

Concrete target:

- extract `LightingManager` into `LucaLights.Core.Engine`
- remove Avalonia/MainWindow/MainViewModel dependencies from the extracted render loop
- introduce event hooks or injected flags for preview/output instead of UI singleton calls
- keep the old app building while the new core grows alongside it

Suggested checkpoint commit:

- `v2: extract core model and config baseline`
