# GraphRuntimeEvaluator zero-allocation refactor

## Context

The lighting engine runs at 60 FPS (or 60 × LED-count Hz for per-pixel graphs) on a loop inside [src/LucaLights.Core/Engine/LightingManager.cs](src/LucaLights.Core/Engine/LightingManager.cs). Idle CPU sits around 3%. Most of that is load-bearing (the frame-pacing spin in `LightingManager` is deliberate and the user wants to keep it), but the node-graph evaluator is doing far more per-frame work than it should:

- A fresh `Dictionary<string, RuntimeValue>` allocated every frame (and every pixel in per-pixel mode) at [GraphRuntimeEvaluator.cs:93](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L93) and [:104](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L104).
- Every read/write goes through `$"{nodeId}:{portId}"` string allocation at [:555-558](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L555-L558).
- Every node re-parses its `JsonObject Properties` every frame via `ReadFloat`/`ReadString`/`ReadBool`/`ParseStringSet`, even though properties only change when the graph is edited.
- Per-pixel mode re-parses `segmentIds` and builds a fresh `HashSet<Segment>` every frame ([:129-157](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L129-L157)).
- `ReadMergedBool/Float/Color` allocate via `Select().ToArray()` + LINQ aggregates ([:665-740](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L665-L740)).
- Blend/merge mode strings are `Trim().ToLowerInvariant()`'d per pixel ([:630-632](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L630-L632)).

All of this is static at render time: `NodeInstance.Properties` is replaced wholesale on graph edits, and `Prepare()` is already called under lock whenever `settings.Dirty` flips. We can move every allocation out of the hot path into `Prepare`.

Intended outcome: `EvaluateGraph` becomes a tight loop over a flat `CompiledNode[]` with integer slot indices and pre-normalized enums. Zero managed allocation per frame. CPU drop proportional to graph complexity.

## Approach

### Prerequisite: golden-frame tests

No tests currently exist for the evaluator. Before touching the hot path, add `tests/LucaLights.Core.Tests/` (xUnit) with fixtures covering each node family. Record expected byte buffers from the **current** code, then re-run after every migration step.

Fixture coverage (minimum):
1. `constant.color` → `output.segment-color` — smoke test.
2. `time.oscillator` → `color.brightness` → output — float plumbing + waveform.
3. `pixel.info` → `color.gradient` → output — per-pixel path + gradient cache.
4. Two `output.segment-color` nodes with different priorities and a non-`override` blend mode — priority sort + blend math.
5. `input.bool` with multiple keys + `mergeMode=all` + `logic.select-color` — merge-mode coverage.
6. `time.pulse` + `time.envelope` evaluated over 5 consecutive frames — cross-frame state continuity.

Harness: build `Settings` in-memory with one `Device` holding two `Segment`s; build `InputSnapshot` from a dict; call `renderer.Prepare(settings)` + `renderer.Render(settings, frameContext)`; assert `segment.Leds` byte-for-byte against `byte[][]` embedded in the test.

### Compiled-node representation

New `CompiledNode` class (one per `NodeInstance` in evaluation order), built in `Prepare` and stored on `PreparedGraph`:

```csharp
internal sealed class CompiledNode
{
    public NodeOp Op;                   // enum replacing string TypeId switch
    public int[] InputSlots;            // indexed by port-order; -1 = disconnected
    public int[] OutputSlots;           // indexed by port-order
    public RuntimeValue[] InputDefaults;// fallback when InputSlot == -1

    // Scalar properties (only the fields this Op uses):
    public float PropFloatA, PropFloatB, PropFloatC, PropFloatD;
    public bool  PropBoolA;
    public Color PropColorA, PropColorB;
    public byte  ModeA, ModeB;          // BlendOp, MergeOp, WaveformOp, EdgeOp, InterpolationOp, CompareOp

    // Op-specialized data:
    public GradientStop[]? GradientStops;   // color.gradient (replaces PreparedGraph._gradientStopsCache)
    public string[]? InputKeys;             // input.bool/float/color — pre-split, pre-lowercased
    public Segment[]? TargetSegments;       // output.segment-color — resolved against settings.Devices
    public PulseState? PulseState;          // time.pulse (moved off PreparedGraph dict)
    public EnvelopeState? EnvelopeState;    // time.envelope

    // output.segment-color only:
    public float Priority;
    public bool  IsActiveStatic;            // if `active` port unconnected, its resolved default
}
```

New enums: `NodeOp`, `BlendOp`, `MergeOp`, `WaveformOp`, `EdgeOp`, `InterpolationOp`, `CompareOp`, `MixColorOp`. Populated once in `Prepare` via the existing string switch, stored as `byte` on `CompiledNode`.

### Slot layout

Single `RuntimeValue[] _outputBuffer` on `PreparedGraph`, sized to the total output-port count across the graph. Populated by walking `EvaluationOrder` once, assigning a sequential slot per output port. Input slots are resolved via the existing `InputConnectionsByNodeId` map (which stays keyed by `OrdinalIgnoreCase` strings — `NodeInstance.Id` casing is preserved through the compile pipeline).

Runtime read helper:

```csharp
RuntimeValue v = slot >= 0 ? _outputBuffer[slot] : node.InputDefaults[portIndex];
```

Buffer is **reused** every frame (and every pixel). Safe because topological order guarantees every output slot is written before any consumer reads it — **provided every node always writes its output slots**. Today `reroute.*` skips the write when its input is disconnected ([:181-187](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L181-L187)); change it to always write using the input default. `output.segment-color` and `annotation.comment` have no output ports, no change needed.

### Per-pixel mode

`PreparedGraph` gains `Segment[] TargetSegments` populated in `Prepare`, replacing `CollectTargetSegments` at [:129-157](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L129-L157). Runtime just walks the array.

### Pending output list

`CompiledOutputNode[]` on `PreparedGraph`, **pre-sorted** by `Priority` then original evaluation index (both static). Runtime walks in sorted order and skips entries where `active` resolves to false — no per-frame `List` allocation, no per-frame `Sort`. The `active` input is still dynamic (could be wired to any bool node), so each frame reads from the slot or falls back to `IsActiveStatic`.

### Migration step order (reviewable diff trail)

0. **Golden-frame tests.** Land before touching the evaluator.
1. **Scaffolding.** Add `NodeOp`, `BlendOp`, etc. enums, `CompiledNode`, `CompiledOutputNode`. Populate them in `Prepare` **alongside** existing structures. `Render` unchanged. Build passes, tests still green.
2. **Slot buffer.** Add `_outputBuffer` + slot assignment in `Prepare`, unused at render. Assert (debug) slot count matches total output-port count.
3. **Dual-write.** `Render` writes to both the dict and `_outputBuffer`. Add a debug-only `Debug.Assert(dictValue.Equals(bufferValue))` after each node. Run the app manually through common flows.
4. **Flip reads per node family**, one commit each (tests gate each commit):
   - (a) constants + reroute (make reroute always write)
   - (b) math.*
   - (c) logic.*
   - (d) color.* incl. gradient (move gradient stops onto `CompiledNode`; drop `_gradientStopsCache`)
   - (e) time.* (move pulse/envelope state onto `CompiledNode`; drop `_pulseStates` / `_envelopeStates` dicts)
   - (f) input.* (cached `InputKeys`, enum merge mode)
   - (g) pixel.info
   - (h) output.segment-color — last; this one replaces the pending-list allocation and pre-sort.
5. **Remove the dict.** Delete the `outputs` parameter from `EvaluateGraph`, delete `BuildOutputKey`, `TryGetInputValue`, `GetInputBool/Float/Color`, and the dict allocations at [:93](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L93) and [:104](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L104).
6. **Segment cache.** Replace `CollectTargetSegments` with pre-resolved `TargetSegments`.
7. **Enum-ify blend/merge/waveform.** `BlendColors`, `BlendOutputColor`, `EvaluateWaveform` take enums. Delete `ToLowerInvariant()` at [:630-632](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L630-L632) and [:982-984](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L982-L984).
8. **De-LINQ merges.** Replace `ReadMergedBool/Float/Color` bodies with `for` loops against cached `InputKeys`. Delete `ReadStringList`, `ParseStringSet`.

### Reused code / references

- `CompiledNodeGraph.EvaluationOrder` ([src/LucaLights.Core/NodeEngine/CompiledNodeGraph.cs:1-8](src/LucaLights.Core/NodeEngine/CompiledNodeGraph.cs)) — already topologically sorted, keep as input.
- `NodeTypeDefinition.Inputs/Outputs` ([src/LucaLights.Core/NodeEngine/NodeTypeDefinition.cs](src/LucaLights.Core/NodeEngine/NodeTypeDefinition.cs)) — source of stable port ordering for slot indices.
- `InputConnectionsByNodeId` on `PreparedGraph` ([:1244-1246](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L1244-L1246)) — already built in `Prepare`, reused for slot resolution.
- `PulseState`, `EnvelopeState`, `GradientStop` ([:1283-1411](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L1283-L1411)) — kept as-is, just moved onto `CompiledNode`.
- `_syncRoot` lock in [LightingManager.cs:133-156](src/LucaLights.Core/Engine/LightingManager.cs#L133-L156) — already guards both `Prepare` and `Render`. No thread-safety work needed.

## Critical files

- [src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs) — primary target
- [src/LucaLights.Core/NodeEngine/CompiledNodeGraph.cs](src/LucaLights.Core/NodeEngine/CompiledNodeGraph.cs) — unchanged, consumed
- [src/LucaLights.Core/NodeEngine/NodeGraphCompiler.cs](src/LucaLights.Core/NodeEngine/NodeGraphCompiler.cs) — unchanged, consumed
- [src/LucaLights.Core/NodeEngine/NodeTypeDefinition.cs](src/LucaLights.Core/NodeEngine/NodeTypeDefinition.cs) — unchanged, consumed
- [src/LucaLights.Core/Engine/LightingManager.cs](src/LucaLights.Core/Engine/LightingManager.cs) — unchanged, just consumes `ILightingRenderer`
- New: `tests/LucaLights.Core.Tests/LucaLights.Core.Tests.csproj` + fixtures

## Verification

1. **Unit tests.** `dotnet test tests/LucaLights.Core.Tests` green after every commit in the migration chain. Tests assert byte-identical `Segment.Leds` output against values recorded from the current implementation.
2. **Build.** `dotnet build src/LucaLights.Desktop/LucaLights.Desktop.csproj` passes in Debug and Release.
3. **Manual smoke.** Launch the Desktop app, connect ITGMania (or osu! via tosu), open the node editor, exercise:
   - Attract mode (no active rendering, clears output)
   - Gameplay with a per-pixel gradient scene
   - A scene with two overlapping outputs at different priorities and a non-`override` blend
   - Graph edit during runtime (save graph → `settings.Dirty` → re-prepare must not crash or leak state)
4. **CPU measurement.** Run `dotnet-counters monitor -p <pid> --counters System.Runtime` before and after. Expect `gc-heap-size` allocation rate to drop dramatically and `cpu-usage` to fall proportional to graph complexity. Record numbers in the final PR description.
5. **Debug dual-write assertion.** During step 3 of the migration, run manually for 5+ minutes under mixed input; `Debug.Assert` must never fire.

## Risks

- **Property-name typos in `Prepare`.** Today a typo silently returns default forever; behavior identical after refactor (defaults frozen at Prepare-time matches current default-every-frame semantics).
- **Blend-mode input port in the future.** If a node type ever adds `blendMode` as an **input** port (currently property-only), pre-resolved `BlendOp` becomes stale. Add a debug assert in `Prepare` that node-type input-port definitions don't collide with names we've pre-resolved as properties.
- **Pulse/envelope state continuity.** Current code keys state by `nodeId` on `PreparedGraph._pulseStates`. A graph edit rebuilds `PreparedGraph` and loses state. Moving state onto `CompiledNode` preserves this exact behavior — equivalent lifetime.
- **Latent gradient-stops bug.** `_gradientStopsCache` at [:1233](src/LucaLights.Core/NodeEngine/GraphRuntimeEvaluator.cs#L1233) is keyed by `nodeId` with no invalidation on property change mid-`PreparedGraph`-lifetime. Today this is masked because graph edits always rebuild `PreparedGraph`. Moving stops to `CompiledNode` fixes the latent bug cleanly — worth noting in the PR.
- **Reroute semantics change.** Making `reroute.*` always write (instead of skipping on disconnected input) is observable if downstream code was relying on the "no output → consumer uses its own default" path. Covered by golden tests on reroute-containing fixtures.
