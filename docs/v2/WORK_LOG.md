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
