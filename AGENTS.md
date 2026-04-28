# AGENTS.md

This file provides architecture guidance for AI assistants working with the LucaLights codebase.

## Project Overview

LucaLights is a lighting control application for ITGMania (dance game) that uses WLED devices via the DDP protocol to create reactive lighting effects. The application receives game state data through modular input sources and sends lighting commands to networked LED devices.

**Technology Stack:**
- .NET 10.0 / C#
- Avalonia UI (cross-platform desktop framework)
- CommunityToolkit.Mvvm (MVVM pattern)
- Velopack for auto-updates
- ASP.NET Core for server API
- Svelte + SvelteFlow for web-based node graph editor

**Project Structure:**
- `src/LucaLights.Core/` - Core library with business logic, node engine, models, engines
- `src/LucaLights.Server/` - ASP.NET Core host serving API + web UI
- `src/LucaLights.Desktop/` - Tray icon desktop launcher (Avalonia)
- `web/lucalights-ui/` - Svelte frontend for node graph editor
- Solution: `Luca Lights.sln`

## Build & Run Commands

### Build the solution
```bash
dotnet build "Luca Lights.sln"
```

### Run the application
```bash
dotnet run --project "src/LucaLights.Desktop/LucaLights.Desktop.csproj"
```

### Build for release
```bash
dotnet build "Luca Lights.sln" -c Release
```

### Publish for Windows distribution
```bash
dotnet publish "src/LucaLights.Desktop/LucaLights.Desktop.csproj" -c Release -o publish -r win-x64 --self-contained true
```

### Run frontend dev server
```bash
cd web/lucalights-ui && npm run dev
```

## Architecture Overview

### Core Threading Model

The application runs three main threads:

1. **UI Thread** (Avalonia dispatcher) - Handles all UI rendering and user interaction
2. **GameInput Thread** - Reads from named pipe or other input modules via `GameInputManager`
3. **LightingManager Thread** - Runs at 60 FPS, renders effects, sends DDP packets to devices

### Data Flow

```
Input Source -> GameInputManager -> InputSnapshot -> LightingManager -> ILightingRenderer -> Devices -> Transport (DDP/UDP) -> WLED
```

### Node Graph System (Primary Effect System)

The node graph system is data-driven with no base classes for individual nodes. All node definitions are in `DefaultNodeTypeCatalog.cs`.

**Key Components:**
- **`NodeTypeDefinition`** - Schema describing a node type (TypeId, inputs, outputs, properties)
- **`NodeInstance`** - Runtime instance of a node in a graph (has Id, TypeId, Properties, X, Y)
- **`NodePortDefinition`** - Input/output port definition (Id, Label, ValueType, Direction)
- **`NodePropertyDefinition`** - Configurable parameter (Key, Label, ValueType, DefaultValue, Min/Max)
- **`NodeValueType`** - Enum: Bool, Float, Color, String, Trigger
- **`INodeTypeCatalog`** / **`DefaultNodeTypeCatalog`** - Registry of all available node types (40 nodes currently)
- **`NodeGraphCompiler`** - Validates graphs (topological sort, cycle detection, type checking)
- **`GraphRuntimeEvaluator`** - Runtime execution engine (big switch statement evaluating each node)

**How to Add a New Node:**

1. Add factory method in `DefaultNodeTypeCatalog.cs` returning `NodeTypeDefinition`
2. Add it to `_nodeTypes` list in constructor
3. Add evaluation case in `GraphRuntimeEvaluator.cs` `switch` statement

Example:
```csharp
// DefaultNodeTypeCatalog.cs
private static NodeTypeDefinition MathSin() => new(
    "math.sin", "Sine", "Math", "Computes sine of input (radians).",
    [Input("value", "Value", NodeValueType.Float, "Input in radians.")],
    [Output("value", "Value", NodeValueType.Float, "Sine result (-1 to 1).")],
    [Property("value", "Value", NodeValueType.Float, "Fallback value (radians).", 0)]);

// GraphRuntimeEvaluator.cs
case "math.sin":
    var value = GetInputFloat(...);
    outputs[BuildOutputKey(node.Id, "value")] = RuntimeValue.FromFloat(MathF.Sin(value));
    break;
```

**Categories of existing nodes (40 total):**
- **Annotations** - Comment nodes
- **Reroute** - Bool, Float, Color reroutes
- **Constants** - ConstantColor, ConstantFloat, ConstantBool
- **Graph Inputs** - GraphBooleanInput, GraphNumberInput, GraphColorInput
- **Math** - Add, Multiply, Clamp, Remap, Wrap, Ping-Pong, Modulo, Abs, Step, Smooth Step, Sine, Cosine, Tangent
- **Logic** - SelectColor, SelectNumber, Not, And, Or, Compare, MixColor
- **Time** - Elapsed, Oscillator, Pulse, Envelope
- **Color** - Brightness, HSVToColor, Gradient
- **Segment** - PixelInfo
- **Outputs** - SegmentColorOutput

### Critical Threading Rules

1. **Always lock when accessing Settings:**
   ```csharp
   lock (Settings.Lock) { /* access Settings.Instance */ }
   ```

2. **Always lock when accessing GameState/InputSnapshot:**
   ```csharp
   lock (GameState.gameState) { /* access gameState.state */ }
   ```

3. **UI updates must be dispatched:**
   ```csharp
   Dispatcher.UIThread.Post(() => /* UI update */);
   ```

4. **Mark dirty after configuration changes:**
   ```csharp
   Settings.Instance!.MarkDirty();
   ```

## Key Files by Feature

### Node Engine
- `src/LucaLights.Core/NodeEngine/DefaultNodeTypeCatalog.cs` - All node type definitions (40 nodes)
- `src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs` - Node evaluation logic (runtime execution)
- `src/LucaLights.Core/NodeEngine/NodeGraphCompiler.cs` - Graph validation and compilation
- `src/LucaLights.Core/Models/NodeGraph.cs` - Persistent graph model (nodes, connections, viewport)
- `src/LucaLights.Core/Engine/NodeGraphLightingRenderer.cs` - Integrates graph into lighting pipeline

### Models
- `src/LucaLights.Core/Models/Settings.cs` - Singleton with Devices, Effects, Graph properties
- `src/LucaLights.Core/Models/Device.cs` - WLED device representation with IP, segments
- `src/LucaLights.Core/Models/Segment.cs` - LED strip/portion with Color array per LED
- `src/LucaLights.Core/Models/InputSnapshot.cs` - Thread-safe snapshot of game state

### Engine & Transport
- `src/LucaLights.Core/Engine/LightingManager.cs` - Main 60 FPS rendering loop
- `src/LucaLights.Core/Engine/NodeGraphLightingRenderer.cs` - Node graph effect renderer
- `src/LucaLights.Core/Engine/DDPSend.cs` - DDP protocol over UDP port 4048
- `src/LucaLights.Core/Engine/DeviceTransport.cs` - Transport abstraction base class

### Game Input
- `src/LucaLights.Core/Engine/GameInputManager.cs` - Modular input system
- Input modules: ITGMania (named pipe), osu! (via tosu WebSocket)
- `InputSnapshot` - Thread-safe snapshot of game state

### Server/API
- `src/LucaLights.Server/LucaLightsServerHost.cs` - DI registration, API endpoints
- Serves the Svelte frontend at `web/lucalights-ui/`

### Frontend (Node Editor)
- `web/lucalights-ui/src/routes/editor/+page.svelte` - Main graph canvas with SvelteFlow
- `web/lucalights-ui/src/lib/components/editor/GraphNode.svelte` - Node rendering component
- `web/lucalights-ui/src/lib/lucalights.ts` - TypeScript types mirroring C# models

### Validators
- `src/LucaLights.Core/Validators/IIpAddressValidationAttribute.cs` - Validates IP format
- `src/LucaLights.Core/Validators/NameValidationAttribute.cs` - Validates device/effect names

## Settings & Configuration

- Settings singleton in `Settings.cs`
- Node graph stored in `settings.json` under `graph` property
- Input module settings per module type
- Settings file location: `%AppData%/LucaLights/settings.json`

## Device/LED Architecture

- `Device.cs` - WLED device representation with IP, segments
- `Segment.cs` - LED strip with Color array per LED
- `DDPSend.cs` - DDP protocol implementation over UDP port 4048
- `DeviceTransport` - Base class for transport abstraction
- WLED devices are configured with UDP port 21234 in WLED sync settings, DDP must be enabled
- DMX settings can interfere with DDP - check if issues arise

## Game Integration

- `GameInputManager.cs` - Modular input system supporting multiple sources
- Input modules:
  - **ITGMania** - Reads from named pipe `\\.\\pipe\\StepMania-Lights-SextetStream`
  - **osu!** - Connects via tosu WebSocket
- `InputSnapshot` - Thread-safe snapshot of game state (button presses, timing)

### Adding a New Input Module

1. Create a new class implementing the input module interface
2. Register it in `GameInputManager`
3. Ensure thread-safe state updates via `InputSnapshot`

## Frontend (Node Editor)

The node graph editor is a Svelte application using SvelteFlow for the node-based UI:

- **Main page:** `src/routes/editor/+page.svelte` - Graph canvas with SvelteFlow
- **Node component:** `src/lib/components/editor/GraphNode.svelte` - Individual node rendering
- **Type definitions:** `src/lib/lucalights.ts` - TypeScript types mirroring C# models

The frontend communicates with the server API for loading/saving graphs and reading device settings.

### Adding New Node Types to Frontend

When adding new node types, ensure the TypeScript types in `lucalights.ts` match the C# definitions in `DefaultNodeTypeCatalog.cs`.

## Release Process

Uses Velopack for automatic updates via GitHub Actions (`.github/workflows/buildAndRelease.yml`).

**Release steps:**
1. Trigger workflow dispatch with version number
2. Builds Windows x64 self-contained package
3. Velopack handles automatic updates for installed clients

## Important Constraints & Gotchas

### ITGMania Integration
- Named pipe `\\.\\pipe\\StepMania-Lights-SextetStream` must exist **BEFORE** ITGMania starts
- ITGMania only connects to pipe once at startup
- ITGMania config: `Preferences.ini` needs `SextetStreamOutputFilename` and `LightsDriver=SextetStreamToFile`

### WLED Device Configuration
- UDP port must be **21234** in WLED sync settings
- **DDP protocol must be enabled** in WLED
- DMX settings can interfere with DDP - check if issues arise

### Node Graph Runtime
- Graph is compiled on every settings change (`MarkDirty` triggers recompilation)
- Compilation includes topological sort and cycle detection
- Runtime evaluator uses a big switch statement per node type
- Stateful nodes (Pulse, Envelope) maintain state across frames via `preparedEffect.GetOrCreateXxxState()`

## Development Notes

- Node graph is stored serialized in `settings.json` under the `graph` property
- All node types must have unique TypeIds
- Input/output type matching is enforced by the compiler (type checking)
- Colors use a specific format - check `RuntimeValue` helpers for creation
- The 60 FPS loop in `LightingManager` should avoid allocations to prevent GC pressure
- Always follow critical threading rules to avoid race conditions

## Verification & Linting

### Backend (.NET)
```bash
dotnet format
```

### Frontend (Svelte)
```bash
cd web/lucalights-ui && npm run check
```
