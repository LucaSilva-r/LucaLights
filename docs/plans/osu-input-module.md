---
name: Osu Input Module Implementation Plan
description: Full plan for OsuInputModule — tosu WebSocket integration, .osu file parsing for all modes, process lifecycle management, channel design
---
# OsuInputModule Implementation Plan

## Decisions Made

- **tosu process management**: Option A — full lifecycle. Download from GitHub releases if missing, check for updates on startup, launch as child process, kill on stop.
- **File parsing**: Parse .osu files for ALL game modes (not just mania). tosu reads osu's memory and provides the songs folder path + beatmap file path, so we always have file system access.
- **Module structure**: Single `OsuInputModule` class. Split into partial classes if one file gets too messy.
- **File location**: `src/LucaLights.Core/GameInput/Modules/OsuInputModule.cs` (already exists, empty)
- **Keyboard input channels**: Potentially listen to actual player keyboard inputs globally (mapping osu keybinds to channels). TBD — needs investigation on Wayland/Linux. Would be cool to have.

## Architecture

### Data Sources

1. **tosu WebSocket `/websocket/v2`** — full game state pushed periodically
   - HP, combo, accuracy, score, beatmap info, kiai, breaks, mods, PP, stars, BPM, mode, state
2. **tosu WebSocket `/websocket/v2/precise`** — high-frequency updates
   - Key presses (k1/k2/m1/m2 with isPressed + count), hit errors, current time
3. **.osu file parsing** — per-column/object note timing for all modes
   - tosu provides: `folders.songs` + `directPath.beatmapFile` to resolve full path
   - Parse on beatmap change (detect via `beatmap.checksum`)

### tosu Process Lifecycle

1. On module start: check configured tosu path (or default location)
2. If not found: download latest release from GitHub (`tosuapp/tosu`)
3. If found: check for updates via GitHub API, download if newer
4. Launch tosu as child process (background, no window)
5. Connect WebSocket with retry loop (2s delay between attempts, same as old code)
6. On module stop: kill tosu child process

Settings for the module (stored in `Settings.InputModuleSettings["osu"]`):
- `tosuPath` — path to tosu executable (auto-detected or user-configured)
- `tosuUrl` — WebSocket base URL (default: `ws://127.0.0.1:24050`)
- `autoDownload` — whether to auto-download tosu (default: true)
- `autoUpdate` — whether to auto-update tosu (default: true)

### Note Timing Engine (ported from old OsuPlayerEngine)

- On beatmap change (checksum differs): parse .osu file via OsuFileParser
- Run a timing loop that interpolates between tosu time updates
- For mania: map columns to `raw.osu.mania.col_N` bool channels
- For standard/taiko/catch: map hit objects to appropriate channels (TBD per mode)
- Handle pause, time jumps (song restart), break sections

### Channel Design

#### System (Bool)
| Key | Description |
|---|---|
| `raw.osu.connected` | tosu WebSocket connected |
| `raw.osu.playing` | In active gameplay |
| `raw.osu.paused` | Game paused |
| `raw.osu.failed` | Player failed |

#### Beatmap State (Bool)
| Key | Description |
|---|---|
| `raw.osu.is_kiai` | Kiai time (chorus/hype sections) |
| `raw.osu.is_break` | Break section |

#### Key Presses — from tosu precise endpoint (Bool)
| Key | Description |
|---|---|
| `raw.osu.keys.k1` | Keyboard key 1 pressed |
| `raw.osu.keys.k2` | Keyboard key 2 pressed |
| `raw.osu.keys.m1` | Mouse button 1 pressed |
| `raw.osu.keys.m2` | Mouse button 2 pressed |

#### Gameplay (Float)
| Key | Range | Description |
|---|---|---|
| `raw.osu.hp` | 0-1 | Health bar (normal) |
| `raw.osu.hp_smooth` | 0-1 | Health bar (smooth) |
| `raw.osu.combo` | 0+ | Current combo |
| `raw.osu.combo_max` | 0+ | Max combo this play |
| `raw.osu.accuracy` | 0-100 | Current accuracy |
| `raw.osu.score` | 0+ | Current score |
| `raw.osu.pp.current` | 0+ | Live PP |
| `raw.osu.pp.fc` | 0+ | FC PP |
| `raw.osu.progress` | 0-1 | Song progress (live / mp3Length) |
| `raw.osu.stars` | 0+ | Live star rating |
| `raw.osu.bpm` | 0+ | Current BPM |
| `raw.osu.unstable_rate` | 0+ | Unstable rate |

#### Mode Detection (Bool)
| Key | Description |
|---|---|
| `raw.osu.mode.standard` | osu!standard active |
| `raw.osu.mode.taiko` | osu!taiko active |
| `raw.osu.mode.catch` | osu!catch active |
| `raw.osu.mode.mania` | osu!mania active |

#### Mania Per-Column (Bool) — from .osu file timing
| Key | Description |
|---|---|
| `raw.osu.mania.col_0` ... `raw.osu.mania.col_N` | Column N has active note |

Max columns: 18 (osu mania supports up to 18K maps, though 10K is practical max).

#### Metadata (String)
| Key | Description |
|---|---|
| `raw.osu.now_playing` | "Artist - Title [Version] (NK)" |
| `raw.osu.beatmap_checksum` | Current beatmap checksum |

### File Structure (if splitting into partials)

```
src/LucaLights.Core/GameInput/Modules/
  OsuInputModule.cs              — core: IGameInputModule impl, definition, snapshot publishing
  OsuInputModule.TosuClient.cs   — partial: WebSocket connection, JSON parsing, tosu data models
  OsuInputModule.TosuProcess.cs  — partial: tosu download/update/launch/kill
  OsuInputModule.NoteEngine.cs   — partial: .osu file parsing + timing loop
```

### tosu v2 Data Model (minimal, what we deserialize)

```csharp
// Root
TosuData { game, state, beatmap, play, folders, directPath, files }

// game
TosuGame { focused: bool, paused: bool }

// state  
TosuState { number: int, name: string }
// number meanings: 0=menu, 2=playing, 5=song select, 7=results, etc.

// beatmap
TosuBeatmap { isKiai, isBreak, isConvert, time{live,firstObject,lastObject,mp3Length}, 
              checksum, mode{number,name}, artist, title, version, 
              stats{stars{live,total}, cs, bpm{realtime,common,min,max}, objects, maxCombo} }

// play
TosuPlay { failed, score, accuracy, healthBar{normal,smooth}, 
           hits{300,geki,100,katu,50,0}, combo{current,max}, 
           mods{name,array,rate}, rank{current}, pp{current,fc}, unstableRate }

// folders
TosuFolders { songs: string }

// directPath
TosuDirectPath { beatmapFile: string }
```

### tosu v2 Precise Data Model

```csharp
TosuPreciseData { currentTime: int, keys: TosuKeyOverlay, hitErrors: int[] }
TosuKeyOverlay { k1{isPressed,count}, k2{isPressed,count}, m1{isPressed,count}, m2{isPressed,count} }
```

### Implementation Order

1. tosu data models (C# records/classes for JSON deserialization)
2. TosuClient — dual WebSocket connection (v2 + v2/precise) with reconnect loop
3. TosuProcessManager — download/update/launch/kill tosu
4. OsuFileParser port — adapt from old code, generalize for all modes
5. NoteTimingEngine — interpolated timing loop from old OsuPlayerEngine
6. OsuInputModule — glue it all together as IGameInputModule
7. Registration in EngineHostedService
8. Test with actual osu + tosu

### Open Questions

- **Standard/Taiko/Catch note channels**: What channels make sense? Standard has circles/sliders/spinners. Taiko has don/kat. Catch has fruits. Need to define per-mode channel mappings from parsed hit objects.
- **Global keyboard input**: Listening to actual keystrokes (mapped via tosu's keybind settings). Needs Wayland investigation on Linux. Could use `/dev/input` or `libinput` on Linux, `SetWindowsHookEx` or raw input on Windows. Deferred for now.
- **osu!lazer vs stable**: tosu supports both. The data model is the same from our perspective. File paths may differ for lazer (different folder structure).
- **Max mania columns to pre-register**: Pre-register 10 columns (col_0 through col_9) in the definition? Or dynamically? ITGMania pattern uses static definition, so we'd need to pick a max.

### Reference: Old Code Locations

- `LucaLights/LTEK ULed/Code/OsuPlayer/TosuClient.cs` — WebSocket client (port this)
- `LucaLights/LTEK ULed/Code/OsuPlayer/OsuPlayerEngine.cs` — timing loop + tosu data handling
- `LucaLights/LTEK ULed/Code/OsuPlayer/OsuFileParser.cs` — .osu file parser (mania hit objects)
- `LucaLights/LTEK ULed/Code/OsuPlayer/ManiaHitObject.cs` — hit object data class
- `LucaLights/LTEK ULed/Code/OsuPlayer/ColumnMapping.cs` — column-to-button mapping (old system, replaced by input channels)
