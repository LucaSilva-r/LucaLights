# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LucaLights is a lighting control application for ITGMania (dance game) that uses WLED devices via the DDP protocol to create reactive lighting effects. The application receives game state data through a named pipe from ITGMania and sends lighting commands to networked LED devices.

**Technology Stack:**
- .NET 9.0 / C#
- Avalonia UI (cross-platform desktop framework)
- MVVM pattern with CommunityToolkit.Mvvm
- Velopack for auto-updates

**Project Structure:**
- `LTEK ULed/` - Core library project containing all business logic and UI
- `LTEK ULed.Desktop/` - Desktop launcher project (entry point)
- Solution is named "Luca Lights.sln"

## Build & Run Commands

### Build the solution
```bash
dotnet build "Luca Lights.sln"
```

### Run the application
```bash
dotnet run --project "LTEK ULed.Desktop/LucaLights.csproj"
```

### Build for release
```bash
dotnet build "Luca Lights.sln" -c Release
```

### Publish for Windows distribution
```bash
dotnet publish ".\LTEK ULed.Desktop\LucaLights.csproj" -c Release -o publish -r win-x64 --self-contained true
```

## Architecture Overview

### Core Threading Model

The application runs three main threads:

1. **UI Thread** (Avalonia dispatcher)
   - Handles all UI rendering and user interaction
   - Updates managed through `Dispatcher.UIThread.Post()`

2. **PipeManager Thread** (`PipeManager.cs`)
   - Creates named pipe server: `StepMania-Lights-SextetStream`
   - Reads game state data (33-byte sextet stream) from ITGMania
   - Parses button states and cabinet light states
   - Updates `GameState.gameState` with parsed data
   - Must be started BEFORE ITGMania launches

3. **LightingManager Thread** (`LightingManager.cs`)
   - Runs at 60 FPS target framerate
   - Renders all effects based on current game state
   - Sends DDP packets to network devices
   - Uses spin-wait for precise timing

### Data Flow

```
ITGMania -> Named Pipe -> PipeManager -> GameState -> LightingManager -> Effects -> Devices -> DDP/UDP -> WLED
```

### Key Components

**Settings (`Settings.cs`)**
- Singleton instance containing all configuration
- Manages collections of Devices and LightEffects
- Persists to `%AppData%/LucaLights/settings.json`
- All modifications must be wrapped in `lock (Settings.Lock)`
- Call `Settings.Instance!.MarkDirty()` when changes require re-rendering

**Device (`Device.cs`)**
- Represents a physical WLED device on the network
- Contains IP address and collection of Segments
- Uses `DDPSend` to transmit RGB data via UDP port 4048
- Each device aggregates LED data from all its segments before sending

**Segment (`Segment.cs`)**
- Represents a physical LED strip or portion of LEDs
- Has a length (number of LEDs) and array of Color values
- Can belong to multiple effect groups via `GroupIds`
- Multiple segments can be on the same device

**LightEffect (`Effect.cs`)**
- Maps game inputs (GameButton/CabinetLight) to visual effects
- Each effect has a unique `GroupId`
- Contains collection of Segments it controls
- Renders gradient animations with configurable scroll speed and scale
- Effects are additive - multiple effects can affect the same LEDs

**GameState (`GameState.cs`)**
- Parses 33-byte sextet stream from ITGMania
- Extracts button states (flags enum) and cabinet light states
- Thread-safe state container accessed via locks

### Critical Threading Rules

1. **Always lock when accessing Settings:**
   ```csharp
   lock (Settings.Lock)
   {
       // Access Settings.Instance.Devices or Effects
   }
   ```

2. **Always lock when accessing GameState:**
   ```csharp
   lock (GameState.gameState)
   {
       // Access gameState.state
   }
   ```

3. **UI updates must be dispatched:**
   ```csharp
   Dispatcher.UIThread.Post(() => MainWindow.Instance!.UpdateLeds());
   ```

4. **Mark dirty after configuration changes:**
   ```csharp
   Settings.Instance!.MarkDirty();  // Triggers recalculation in LightThread
   Settings.Save();  // Persist to disk
   ```

### Effect Rendering Pipeline

1. `LightingManager.LightThread.Run()` loops at 60 FPS
2. Gets current `GameButton` and `CabinetLight` states from `GameState`
3. Clears all segment LED arrays
4. For each `LightEffect`:
   - Calls `effect.Render(gameButton, cabinetLight)`
   - This populates `effect.leds[][]` with gradient colors
   - Additively blends effect colors into segment LED arrays
5. Sends aggregated data via `device.Send()` -> `DDPSend.send()`

### DDP Protocol (`DDPSend.cs`)

- Sends RGB data to WLED devices using DDP (Distributed Display Protocol)
- UDP port 4048
- 10-byte header + 3 bytes per LED (RGB)
- Max 480 LEDs per packet (fits in single ethernet frame)
- Fire-and-forget UDP transmission

## Important Constraints

### ITGMania Integration
- Named pipe `\\.\pipe\StepMania-Lights-SextetStream` must exist BEFORE ITGMania starts
- ITGMania only connects to pipe once at startup
- User must configure ITGMania's `Preferences.ini`:
  ```ini
  SextetStreamOutputFilename=\\.\pipe\StepMania-Lights-SextetStream
  LightsDriver=SextetStreamToFile
  ```

### WLED Device Configuration
- UDP port must be 21234 in WLED sync settings (note: code uses 4048 for DDP, not the WLED UI port)
- DDP protocol must be enabled
- DMX settings can interfere with DDP

### Thread Safety
- Settings modifications require `Settings.Lock`
- GameState access requires `GameState.gameState` lock
- Effect recalculations should mark settings as dirty

## File Locations

- Settings: `%AppData%\LucaLights\settings.json`
- Solution: `Luca Lights.sln`
- Main project: `LTEK ULed\LTEK ULed.csproj`
- Desktop launcher: `LTEK ULed.Desktop\LucaLights.csproj`

## Release Process

The project uses Velopack for automatic updates via GitHub Actions workflow (`.github/workflows/buildAndRelease.yml`):

1. Trigger via workflow dispatch with version number
2. Builds with .NET 9.0
3. Publishes win-x64 self-contained
4. Creates Velopack delta packages
5. Uploads to GitHub releases with tag `v{version}`

## UI Framework Notes

- Uses Avalonia with AXAML markup (similar to WPF/XAML)
- Controls in `Controls/` directory use code-behind pattern
- ViewModels use `CommunityToolkit.Mvvm` attributes like `[ObservableProperty]` and `[RelayCommand]`
- DialogHost.Avalonia for modal dialogs
- Semi.Avalonia for UI theme

## Validation

- Custom validation attributes in `Validators/`:
  - `IpAddressValidationAttribute` - validates IP format
  - `NameValidationAttribute` - validates device/effect names
- Applied via property attributes on ObservableProperty fields
