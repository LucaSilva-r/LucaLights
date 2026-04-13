# LucaLights v2: ASP.NET Core + SvelteKit + Node-Based Effect Editor

## Context

LucaLights is a .NET/Avalonia desktop app that controls WLED LED devices via DDP/UDP, reading game state from ITGMania via named pipes. The current effect system is limited to scrolling gradients per effect. The goal is to:

1. **Ditch Avalonia** - replace with a browser-based UI (ASP.NET Core backend + SvelteKit SPA frontend)
2. **Node-based effect editor** - replace the simple gradient system with a composable node graph (like Unity Shader Graph) using SvelteFlow, enabling LED matrices, complex patterns, and much more
3. **Keep core engine in C#** - preserve the battle-tested render loop, DDP/UDP protocols, named pipe reader, and game state parsing
4. **Become game agnostic** - move game-specific integration behind pluggable input modules that provide timing, triggers, scalar values, and metadata to the graph runtime

---

## Solution Structure

```
LucaLights/
  LucaLights.sln
  src/
    LucaLights.Core/                    # Class library, zero UI dependencies
      LucaLights.Core.csproj
      Color.cs                          # Lightweight RGB struct (replaces Avalonia.Media.Color)
      Models/
        Device.cs                       # POCO (no ObservableObject)
        Segment.cs                      # POCO (no ObservableObject)
        InputSnapshot.cs                # Normalized per-frame values exposed by game modules
        InputDefinition.cs              # Describes available channels/metadata for the editor
        NodeGraph.cs                    # Serializable graph definition
        Settings.cs                     # Clean POCO config (single Graph, no Effect list)
      Engine/
        LightingManager.cs              # 60fps render loop (preserved, stripped of Avalonia refs)
        ConfigManager.cs                # Load/Save JSON config
        OsuPlayer/                      # Osu integration (preserved)
      GameInput/
        IGameInputModule.cs             # Contract for game adapters
        GameInputManager.cs             # Active module lifecycle + snapshot cache
        ModuleContext.cs                # Logging/config/services passed to modules
        Modules/
          ITGManiaInputModule.cs        # Named pipe/FIFO adapter extracted from PipeManager/GameState
      NodeEngine/
        INode.cs                        # Node interface
        NodeContext.cs                   # Per-frame evaluation context
        CompiledGraph.cs                # Topologically sorted flat-array evaluator
        NodeTypeRegistry.cs             # Registry of available node types
        Nodes/
          InputNodes.cs                 # Time, LedPosition, InputBool, InputFloat, InputColor, Metadata
          GeneratorNodes.cs             # SolidColor, Gradient, Rainbow, Noise
          MathNodes.cs                  # Add, Multiply, Mix, Step, Smoothstep
          OutputNode.cs                 # ColorOutput (final LED color)
      Transport/
        DDPSend.cs                      # DDP protocol (preserved)
        UdpRealtimeSend.cs              # UDP Realtime protocol (preserved)

    LucaLights.Server/                  # ASP.NET Core web host
      LucaLights.Server.csproj
      Program.cs                        # App builder, DI, startup, browser launch
      Endpoints/
        DeviceEndpoints.cs              # REST CRUD for devices + segments
        GraphEndpoints.cs               # GET/PUT /api/graph, POST /api/graph/validate
        NodeTypesEndpoints.cs           # GET available node types/metadata
        SettingsEndpoints.cs            # GET/PUT global settings
        InputEndpoints.cs               # GET current input snapshot, module list
      WebSocket/
        PreviewWebSocket.cs             # Binary LED preview frames (~20fps)
        EventWebSocket.cs               # JSON input-state + module-status + config change events
      Services/
        EngineHostedService.cs          # IHostedService wrapping engine lifecycle
        PreviewBroadcaster.cs           # Taps render loop, throttles, broadcasts
      wwwroot/                          # SvelteKit build output

  web/
    lucalights-ui/                      # SvelteKit SPA
      src/
        lib/
          api/
            client.ts                   # Typed REST API client
            websocket.ts                # WebSocket client (preview + events)
          stores/
            devices.ts                  # Device/segment state
            inputState.ts               # Live input snapshot + module status
            preview.ts                  # LED preview binary data
          components/
            ui/                         # shadcn-svelte components
            preview/
              LedStrip.svelte           # Canvas-based LED visualization
              DevicePreview.svelte      # All segments for a device
            editor/
              NodeEditor.svelte         # SvelteFlow wrapper
              nodes/                    # Custom node components per type
        routes/
          +layout.svelte                # Main layout with nav + persistent preview panel
          +page.svelte                  # Dashboard
          devices/+page.svelte          # Device management
          editor/+page.svelte           # Node graph editor (single unified graph)
```

---

## Phase Breakdown

### Phase 0: Foundation - Extract Core Engine
**Goal:** New solution skeleton, port core engine out of Avalonia, and define the game-input abstraction.

- Create `LucaLights.Core` class library (net10.0, no Avalonia packages)
- Define `Color` struct: `readonly record struct Color(byte R, byte G, byte B)` with `Black`, `FromRgb()`, additive `Add()` - matches current Avalonia.Media.Color usage pattern
- Define game-agnostic input contracts:
  - `IGameInputModule` returns an `InputSnapshot` containing normalized values for the current frame
  - `InputDefinition` describes what channels a module exposes (`bool`, `float`, `color`, `string metadata`, timing fields such as song time / beat / BPM)
  - `GameInputManager` owns the active module, lifecycle, and latest snapshot cache for the render loop + API
- Port each core file, stripping Avalonia dependencies:
  - **Segment.cs**: `ObservableObject` -> plain class, `ObservableCollection<int>` -> `List<int>`
  - **Device.cs**: Same stripping, keep `Recalculate()`, `Send()`, `Dispose()`
  - **GameState.cs**: Convert from app-global singleton into ITGMania-specific parsing state owned by `ITGManiaInputModule`; remove `Dispatcher.UIThread.Post()` on line 93, expose parsed values through `InputSnapshot`
  - **LightingManager.cs**: Remove `Dispatcher.UIThread.Post()` (lines 73, 122, 188), remove `MainViewModel.Instance!.debug` / `lightOutput` / `MainWindow.Instance!` refs - replace with injected state flags + `event Action? OnFrameRendered`
  - **PipeManager.cs**: Fold into `ITGManiaInputModule` or split into reusable transport helper + module; remove `MainViewModel.Instance!.debug` ref, accept config via constructor
  - **Settings.cs**: Remove `ObservableObject`, `ObservableProperty`, `ObservableCollection` -> plain POCO with `List<T>`. Extract Load/Save to `ConfigManager`
  - **DDPSend.cs**, **UdpRealtimeSend.cs**: Swap `Avalonia.Media.Color` for new `Color` struct
  - **Effect model**: Removed — replaced by single unified `NodeGraph` on `Settings`
- Implement first module: `ITGManiaInputModule` backed by the current named pipe/FIFO protocol so behavior stays equivalent during extraction
- Create `LucaLights.Server` project, minimal `Program.cs`
- Verify: `dotnet build` succeeds

### Phase 1: ASP.NET Core Server Layer
**Goal:** Working web server with REST API, WebSocket preview, and engine lifecycle.

- **EngineHostedService** (`IHostedService`): Wires up `ConfigManager.Load()`, `GameInputManager.Start()`, `LightingManager.Start()` on startup, clean shutdown on stop
- **Game input hosting**: `GameInputManager` starts the configured module on startup, publishes snapshot updates, and supports hot-restart of the active module
- **REST API Controllers:**
  - `GET/POST/PUT/DELETE /api/devices` (+ nested `/segments`)
  - `GET/PUT /api/graph` + `POST /api/graph/validate` (single unified graph)
  - `GET /api/node-types` (metadata for the frontend editor)
  - `GET /api/input-modules` (installed/available modules + their channel definitions)
  - `GET/PUT /api/settings`
  - `GET /api/input-state` + `POST /api/input-state/debug` (simulate timing/input/metadata)
  - `POST /api/system/shutdown` + `POST /api/system/restart-engine` (lifecycle controls)
- **WebSocket endpoints** (raw WebSocket, not SignalR - lower overhead for binary):
  - `/ws/preview` - binary LED frames at ~20fps
  - `/ws/events` - JSON input-state changes, module status, config change notifications
- **PreviewBroadcaster**: Subscribes to `LightingManager.OnFrameRendered`, samples every 3rd frame, broadcasts binary to connected WebSocket clients
- **Static file serving**: `app.UseStaticFiles()` + `app.MapFallbackToFile("index.html")` for SPA routing
- **Browser auto-launch**: `Process.Start()` to open `http://localhost:5050` on startup
- **Console output**: Structured logging to console so the user sees engine status, connections, errors
- Verify: Can hit REST endpoints with curl, WebSocket connects

### Phase 2: Node Engine (Core Innovation)
**Goal:** High-performance node graph evaluation engine replacing gradient rendering and consuming normalized module-provided inputs.

**Data model:**
```csharp
public class NodeGraph {
    public List<NodeInstance> Nodes { get; set; }
    public List<Connection> Connections { get; set; }
}
public class NodeInstance {
    public string Id { get; set; }          // Unique within graph
    public string TypeId { get; set; }      // e.g. "generator.rainbow"
    public Dictionary<string, object> Properties { get; set; }
    public float X { get; set; }            // Editor position
    public float Y { get; set; }
}
public class Connection {
    public string SourceNodeId { get; set; }
    public string SourcePortId { get; set; }
    public string TargetNodeId { get; set; }
    public string TargetPortId { get; set; }
}
```

**Node interface:**
```csharp
public interface INode {
    string TypeId { get; }
    NodePort[] Inputs { get; }
    NodePort[] Outputs { get; }
    void Evaluate(NodeContext ctx, Span<float> inputs, Span<float> outputs);
}
```

All port values are `float`. Color = 3 consecutive floats (R,G,B in 0-1). Bool = 0.0 or 1.0. Uniform representation avoids boxing/type-switching.

**Phase 1 node types:**
- **Input:** `input.time`, `input.song_time`, `input.beat`, `input.led_position`, `input.bool`, `input.float`, `input.color`, `input.metadata`
- **Generators:** `generator.solid_color`, `generator.gradient`, `generator.rainbow`, `generator.noise`
- **Math:** `math.add`, `math.multiply`, `math.mix`, `math.step`, `math.smoothstep`
- **Output:** `output.color`

Instead of hardcoding ITG concepts into nodes, each input node references a channel key exposed by the active module definition, e.g. `buttons.menu_left`, `cabinet.marquee_left`, `song.bpm`, `timing.measureProgress`.

**Compiled graph evaluation (performance-critical):**

On graph save (NOT per-frame):
1. Topological sort nodes (detect cycles, reject invalid graphs)
2. Allocate flat `float[]` buffer sized to hold all port values
3. Build `EvalStep[]` array (node + input/output offsets into buffer)
4. Resolve connections as copy instructions (source offset -> target offset)

Per-LED per-frame evaluation:
```csharp
// Set context once per frame (timing/input/metadata snapshot)
// Per LED: set time + position, execute steps linearly, read Color from known offset
// Zero allocation, linear memory access, cache-friendly
```

For a 10-node graph, 300 LEDs, 60fps = ~180K evaluations/sec of simple float math. Well within budget.

**Integration:** `NodeGraphLightingRenderer` compiles and evaluates `Settings.Graph` each frame. Multiple output nodes in the same graph handle per-device routing.

### Phase 3: SvelteKit Frontend (overlaps with Phase 2)
**Goal:** Complete web UI with node editor, device management, live preview.

- **SvelteKit setup**: Skeleton template, `adapter-static` for SPA mode, Vite dev proxy to ASP.NET backend
- **Dependencies**: `@xyflow/svelte` (SvelteFlow), `shadcn-svelte`, `bits-ui`, `tailwindcss`
- **API client**: Typed fetch wrapper + two WebSocket connections (preview binary, events JSON)
- **LED preview**: Canvas-based `LedStrip.svelte` component, renders RGB data from preview WebSocket. Visible on every page.
- **Node editor** (SvelteFlow):
  - Custom node components per type (color picker on SolidColor, gradient editor on Gradient, input-channel selector on InputBool/InputFloat, etc.)
  - Input nodes populate their channel dropdowns from `/api/input-modules` definitions so the same editor works for ITGMania and future games
  - Graph held in local Svelte store during editing
  - **Explicit save** (not auto-sync on every connection change) - avoids frame stutters from recompilation and invalid intermediate states
  - "Preview" button for testing without persisting
- **Device management**: CRUD forms with shadcn-svelte components
- **Dashboard**: Live preview, input/module status indicator, system status
- **System page**: Engine restart, module reconnect, shutdown app, connection status indicators

### Phase 4: Packaging & Polish
**Goal:** Finish app lifecycle, packaging, and production hardening for v2.

- **Fresh-config assumption**: Do not support automatic migration from the legacy settings format. v2 starts from a new config model because the system is fundamentally different.
- **Default module setup**: Ship with `ITGManiaInputModule` as the default initial module configuration for fresh installs
- **Console app with lifecycle controls**: App runs as a console window showing logs. Browser UI includes a system status page with Restart Engine / Shut Down buttons. Console supports Ctrl+C for graceful shutdown.
- **Cross-platform packaging**: `dotnet publish --self-contained`, SvelteKit build copied to wwwroot
- **Build script**: `npm run build` in web/ -> copy to wwwroot -> `dotnet publish`

---

## Key Architectural Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Color type | Custom `Color` struct | `System.Drawing.Color` has unnecessary baggage. Simple R/G/B bytes match the entire codebase's usage. |
| Project structure | New solution, not in-place refactor | Every file has Avalonia imports. Clean extraction is faster than stripping. Old code serves as reference. |
| Real-time transport | Raw WebSocket, not SignalR | SignalR adds negotiation overhead. For binary LED data at 20fps on localhost, raw WS is simpler and lower-latency. |
| Node evaluation | Flat float buffer + topological sort | Avoids deep call stacks and poor cache locality of recursive graph traversal. Same approach as Blender/Unreal node engines. |
| Game integration | Pluggable input modules publishing normalized channels | Keeps core lighting engine game-agnostic while letting each game expose timing, buttons, metadata, and custom values. |
| Graph save model | Explicit save, not auto-sync | Avoids recompilation stutters on every mouse-up, handles invalid intermediate states gracefully. |
| Two WS endpoints | Separate preview (binary) + events (JSON) | Avoids framing/demux overhead. Two localhost connections is negligible. |
| Frontend framework | SvelteKit + SvelteFlow + shadcn-svelte | Built-in stores avoid state management library complexity. SvelteFlow from same team as ReactFlow. |
| App lifecycle | Console + browser UI controls | No tray icon dependency. Console shows logs, browser UI has restart/shutdown buttons. Ctrl+C for graceful shutdown. |

---

## Verification Strategy

- **Unit tests** (LucaLights.Core.Tests): Node evaluation per type, CompiledGraph with known graphs, topological sort + cycle detection, input-module contract tests, config serialization round-trip
- **Integration tests**: REST API via `WebApplicationFactory`, WebSocket frame reception, module startup/shutdown, config persistence across restart
- **Manual testing**: Create device + node graph (`input.song_time` -> Rainbow -> Output), verify DDP packets on network. Test debug input via API. Test on both Windows and Linux. Open two browser tabs, verify both get preview.

---

## Critical Files (Current Codebase Reference)

- `src/LucaLights.Core/Engine/LightingManager.cs` - Render loop heart. `OnFrameRendered` now lives in core/server flow.
- Old gradient `Effect.cs` path removed from active app. Node engine replaces that rendering path entirely.
- `src/LucaLights.Core/Models/Settings.cs` - Clean POCO settings model replaces singleton + observable pattern.
- `src/LucaLights.Core/GameInput/Modules/ITGManiaInputModule.cs` - Current seed for migrated game-state and pipe logic.
- `src/LucaLights.Core/Models/Device.cs` - `Send()` + `Recalculate()` preserved through core migration.
- `src/LucaLights.Core/Transport/DDPSend.cs` - Current DDP transport path.
- `src/LucaLights.Core/Transport/UdpRealtimeSend.cs` - Current UDP Realtime transport path.
