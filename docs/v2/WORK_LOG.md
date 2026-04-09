# LucaLights v2 Work Log

Use this file as an append-only session log.

Each entry should capture:

- date
- what changed
- decisions made
- blockers or risks
- next recommended step

## 2026-04-08

What changed:

- clarified `PLAN.md` so LucaLights v2 is explicitly game agnostic
- introduced the input-module concept as a first-class architectural concern
- created the v2 docs scaffold in `docs/v2/`

Decisions made:

- game-specific integrations will be modules that publish normalized channels
- the first module will be `ITGManiaInputModule`
- repo docs, not chat history, will be the source of truth for progress tracking

Blockers or risks:

- none yet beyond the known extraction complexity from Avalonia singletons

Next recommended step:

- implement `Phase 0.1 - New solution skeleton`

## 2026-04-08

What changed:

- created `src/LucaLights.Core` with a first `Color` type
- created `src/LucaLights.Server` with a minimal ASP.NET Core entry point
- added both projects to [Luca Lights.sln](../../Luca%20Lights.sln)
- verified the full solution builds successfully
- added git-checkpoint guidance to the v2 docs

Decisions made:

- keep the existing solution file for now and add v2 projects incrementally
- use one git commit per implementation slice
- defer solution/file renaming until the migration shape is more stable

Blockers or risks:

- `dotnet build` succeeds, but the existing Avalonia project still has pre-existing warnings
- `dotnet sln add` expanded the solution configurations to include `x64` and `x86`; acceptable for now, but worth keeping an eye on

Next recommended step:

- begin `Phase 0.2 - Core extraction` with `Segment`, `Device`, and `Settings`

## 2026-04-08

What changed:

- added VS Code launch support for the legacy desktop app
- added VS Code launch support for the new `LucaLights.Server` project
- added dedicated build tasks for desktop and v2 server targets

Decisions made:

- keep VS Code launch configs explicit per runnable target instead of one generic config
- use project-specific pre-launch build tasks so running from VS Code stays predictable

Blockers or risks:

- none found; both target projects build cleanly in isolation

Next recommended step:

- continue with `Phase 0.2 - Core extraction`

## 2026-04-08

What changed:

- extracted portable core `Segment`, `Device`, and `Settings` models into `src/LucaLights.Core`
- added future-facing core `Effect` and `NodeGraph` models so settings can reference the new graph-based effect shape
- added core transport senders for DDP and UDP Realtime
- added core `ConfigManager` for settings load/save without desktop dependencies
- verified both `LucaLights.Core` and the full solution build successfully

Decisions made:

- keep the new core models separate from the legacy Avalonia models for now rather than partially rewiring the old app mid-slice
- let v2 settings store input-module configuration generically under `inputModuleSettings`
- preserve the current ITGMania pipe path as the default config for the initial input module

Blockers or risks:

- `LightingManager` is still legacy-only, so Phase 0.2 is not complete yet
- the new v2 `Settings` shape is forward-looking and intentionally does not support legacy config migration

Next recommended step:

- extract the render loop into a core `LightingManager` that no longer depends on Avalonia or desktop singletons

## 2026-04-08

What changed:

- refactored the core transport layer so `DDPSend` and `UdpRealtimeSend` inherit from a shared transport base class
- updated core `Device` to store a serialized transport type plus a runtime transport instance
- removed the old-settings migration requirement from the v2 plan and tracker

Decisions made:

- v2 will not attempt to migrate legacy settings automatically
- device transport should be standardized behind a common protocol abstraction before more engine work lands

Blockers or risks:

- transport abstraction is now cleaner, but the extracted render loop still needs to be moved over to use the new core `Device`

Next recommended step:

- extract `LightingManager` into `LucaLights.Core.Engine` and point it at the new portable models

## 2026-04-08

What changed:

- renamed the generic core transport abstraction from `WledProtocol` to `DeviceTransport`
- renamed the matching serialized/runtime device transport types so future non-WLED outputs fit naturally

Decisions made:

- the v2 core should use device-agnostic transport naming because future support may include DMX and Art-Net devices

Blockers or risks:

- none added by this rename; the remaining Phase 0.2 work is still the render-loop extraction

Next recommended step:

- extract `LightingManager` into `LucaLights.Core.Engine`

## 2026-04-08

What changed:

- extracted a new core `LightingManager` into `src/LucaLights.Core/Engine`
- replaced legacy UI-singleton coupling with a renderer interface, runtime callbacks, and engine events
- added a sync root and dirty-settings flow so the new engine can be driven safely by future server/input layers

Decisions made:

- the extracted core engine should not know about Avalonia, `MainWindow`, or desktop view models
- effect rendering stays behind an interface for now so we can keep moving before the node engine lands

Blockers or risks:

- the new engine currently uses a generic renderer abstraction, so ITG-specific effect parity is not wired yet
- the next phase needs to replace the temporary activity callback with a real input-module manager

Next recommended step:

- implement the input-module foundation in `LucaLights.Core`

## 2026-04-08

What changed:

- added the core game-input foundation under `src/LucaLights.Core/GameInput`
- introduced `InputSnapshot`, `InputDefinition`, `InputChannelDefinition`, and `InputValueType`
- introduced `IGameInputModule` and `GameInputManager`
- connected the extracted `LightingManager` to `GameInputManager` and carried `InputSnapshot` through `LightingFrameContext`

Decisions made:

- the engine frame context should include the latest input snapshot now, before effect/node integration lands
- one active input module at a time remains the working model for v2

Blockers or risks:

- there is still no concrete `ITGManiaInputModule`, so the foundation is present but real game input is not wired yet

Next recommended step:

- implement `ITGManiaInputModule` and connect it to the current named pipe or FIFO protocol

## 2026-04-08

What changed:

- added [ARCHITECTURE_GRAPH_BINDINGS.md](./ARCHITECTURE_GRAPH_BINDINGS.md) to capture the reusable graph-input and binding-profile design
- implemented `ITGManiaInputModule` on top of the new input-module contracts
- ported the current named pipe or FIFO reading approach into the new module
- translated ITGMania state into normalized raw channels and snapshots for the core runtime

Decisions made:

- reusable graphs should eventually target graph-defined inputs plus binding profiles, not giant dynamic game-state nodes
- raw game channels remain available for game-specific graphs
- the first ITG module focuses on raw channels and parity, not semantic cross-game mapping

Blockers or risks:

- the ITG module currently exposes raw button, cabinet, and lights-mode channels only; richer timing data would need another source
- the server host still needs to wire `ConfigManager`, `GameInputManager`, and `LightingManager` together in one runtime

Next recommended step:

- add the first ASP.NET Core hosted bootstrap for config, input modules, and lighting engine lifecycle

## 2026-04-09

What changed:

- added an ASP.NET Core hosted runtime bootstrap with `EngineHostedService`
- registered config, input-module management, lighting engine lifecycle, and the temporary no-op renderer through server DI
- added diagnostic endpoints for root status, system status, input-module definitions, and latest input state
- fixed ITGMania input definition initialization so module definitions can be enumerated safely by API callers
- bounded ITGMania shutdown waits so blocked FIFO reads do not freeze server shutdown

Decisions made:

- keep the first server bootstrap intentionally thin and diagnostic-focused before adding the full browser-facing API surface
- keep the runtime owned by the ASP.NET Core host so future UI, WebSocket, and packaging work share one lifecycle

Blockers or risks:

- output rendering is still using `NoOpLightingRenderer`, so this host boots the runtime but does not yet drive real device output
- Phase 1 still needs device/effect/settings REST endpoints, static UI serving, and WebSocket/event streams

Next recommended step:

- add REST endpoints for devices, effects, and settings using the shared v2 `Settings` instance and `ConfigManager`

## 2026-04-09

What changed:

- added stable IDs to v2 devices and segments so server routes do not depend on list indices
- added REST CRUD endpoints for devices, nested device segments, and effects
- added `GET /api/settings` and `PUT /api/settings`
- routed settings mutations through the lighting manager sync root, dirty-state marking, and `ConfigManager.Save()`
- smoke-tested the API against a temporary config directory so the test writes did not touch normal LucaLights settings

Decisions made:

- REST routes should use stable model IDs from the start because the browser UI will reorder and edit lists frequently
- the first REST surface stays as thin minimal API endpoint groups instead of introducing controller ceremony this early

Blockers or risks:

- input module setting changes are persisted, but fully reloading a module's construction-time settings still belongs with a later engine restart or module hot-reload endpoint
- `Settings.Dirty` currently means runtime settings still need to be applied by the render loop, so it can remain true while input is inactive even after the config file has been saved

Next recommended step:

- add WebSocket plumbing for preview frames and runtime events

## 2026-04-09

What changed:

- added `/ws/events` for JSON runtime events, including input snapshots, module changes, settings changes, and system events
- added `/ws/preview` for sampled LED preview snapshots from the current settings/device buffers
- added static diagnostics UI assets under `src/LucaLights.Server/wwwroot`
- changed `/` to serve the diagnostics UI and kept the old root status payload available at `/api`
- added placeholder `GET /api/node-types` for the future node catalog
- added `POST /api/system/restart-engine` and `POST /api/system/shutdown`
- wired REST settings mutations into the runtime event stream

Decisions made:

- the Phase 1 UI should stay a disposable diagnostics surface, not the real SvelteKit/shadcn-svelte app
- the real browser app should consume the same `/ws/events` contract introduced here
- the node type catalog endpoint exists now but intentionally returns an empty catalog until Phase 2 defines real node types

Blockers or risks:

- preview frames currently reflect the temporary no-op renderer, so they prove the transport path but not final graph-rendered color output
- ITGMania FIFO shutdown can still wait up to the bounded timeout when the reader is blocked

Next recommended step:

- start Phase 2 by adding node type contracts, a small node catalog, graph validation, and the graph compilation skeleton

## 2026-04-09

What changed:

- added [ARCHITECTURE_NODE_ENGINE.md](./ARCHITECTURE_NODE_ENGINE.md)
- added core node-engine contracts for value types, ports, properties, node types, catalog lookup, diagnostics, and compiled graph results
- added a default node catalog with constants, graph inputs, color selection, and segment-color output
- added `NodeGraphCompiler` validation with port checks, duplicate input checks, cycle detection, and topological evaluation order
- added stable connection IDs to the graph model
- replaced the placeholder node-type API with the real catalog
- added whole-document graph endpoints for load, save, and validation

Decisions made:

- effect graphs remain document-style resources, matching SvelteFlow full-snapshot saves
- invalid graph drafts can be validated without being saved through `POST /graph/validate`
- `PUT /graph` saves the graph and returns validation diagnostics, but runtime evaluation will decide later whether invalid graphs become active

Blockers or risks:

- runtime node evaluation is not implemented yet
- there is still no active-effect selection model, so graph rendering needs one more design pass before replacing the no-op renderer

Next recommended step:

- implement runtime graph evaluation and wire `output.segment-color` into segment buffers

## 2026-04-09

What changed:

- scaffolded `web/lucalights-ui` with SvelteKit, Tailwind, and `shadcn-svelte`
- added a live runtime dashboard for system status, devices, effects, input snapshots, and preview frames
- wired the browser app to the existing REST endpoints plus `/ws/events` and `/ws/preview`
- added Vite dev proxies and fixed the VS Code server launch to `http://127.0.0.1:5050`
- added VS Code tasks for building and running the Svelte UI
- verified the new frontend with `npm run check` and `npm run build`

Decisions made:

- the first UI slice is a standalone SvelteKit app under `web/` rather than immediately replacing the server-served diagnostics assets
- the dashboard prioritizes validation of runtime plumbing before the graph editor exists
- the frontend talks to the backend through same-origin paths and a Vite dev proxy so the eventual server integration does not need a client-side API rewrite

Blockers or risks:

- active-effect selection is still read-only because settings editing is not surfaced in the browser yet
- the graph editor shell still does not exist, so Phase 3 is only partially complete
- the frontend build is not yet copied into `LucaLights.Server/wwwroot`

Next recommended step:

- add browser active-effect selection and the first SvelteFlow editor shell on top of the current graph endpoints

## 2026-04-09

What changed:

- added `GraphRuntimeEvaluator` for the bootstrap node set in `LucaLights.Core.NodeEngine`
- added `NodeGraphLightingRenderer` and replaced the temporary no-op renderer in the server runtime
- wired `output.segment-color` to fill targeted device segments during frame rendering
- added `Settings.ActiveEffectId` with a fallback to the first saved effect
- updated system status to report the active effect ID
- relaxed `Device` disposal semantics so settings round-trips and runtime restarts do not poison reusable device models
- smoke-tested the core runtime with a temporary harness that flipped a segment from red to blue through `input.bool` and `logic.select-color`

Decisions made:

- the first runtime keeps output semantics simple: buffers are cleared every frame and later output nodes win when they overlap
- effect selection is settings-driven for now instead of adding a dedicated effect-activation endpoint immediately
- viewport remains editor-only state even after runtime landed

Blockers or risks:

- preview and rendering still depend on an active input snapshot, which may need a future authoring-friendly override
- the runtime node library is still intentionally tiny and only covers the bootstrap node set

Next recommended step:

- start the SvelteKit UI scaffold and connect it to the now-live graph runtime and preview APIs

## 2026-04-09

What changed:

- reviewed the SvelteFlow API reference for the graph editor contract
- documented the SvelteFlow-native wire shape in [ARCHITECTURE_NODE_ENGINE.md](./ARCHITECTURE_NODE_ENGINE.md)
- updated the tracker and docs index so future work starts with the SvelteFlow adapter before the real editor

Decisions made:

- the browser-facing graph API should use SvelteFlow-style `nodes`, `edges`, and `viewport`
- SvelteFlow `node.type` should map to backend node type IDs
- SvelteFlow `edge.sourceHandle` and `edge.targetHandle` should map to backend port IDs
- the C# compiler can keep the normalized internal graph shape behind a server-side adapter

Blockers or risks:

- the current graph endpoints still use the internal `NodeGraph` shape, so they should be adapted before the SvelteKit editor is built

Next recommended step:

- add SvelteFlow graph DTOs and conversion helpers, then update graph endpoints to save and validate through that adapter

## 2026-04-09

What changed:

- added SvelteFlow graph DTOs for `nodes`, `edges`, and `viewport`
- added a server-side graph adapter from SvelteFlow documents to the normalized core `NodeGraph`
- updated graph load, save, and validate endpoints to speak the SvelteFlow wire shape
- added editor viewport state to the internal graph model so SvelteFlow `toObject()` data can round-trip
- smoke-tested save, load, and validation through the API with a SvelteFlow-shaped graph document

Decisions made:

- graph endpoints now return the same graph document shape the SvelteFlow editor should keep in its store
- runtime compilation remains based on the normalized core graph model
- viewport is persisted as editor state, but it is ignored by node compilation and runtime evaluation

Blockers or risks:

- extra future UI-only node or edge metadata outside `node.data.properties` is not preserved yet
- runtime node evaluation is still not implemented

Next recommended step:

- implement runtime graph evaluation and wire `output.segment-color` into segment buffers
