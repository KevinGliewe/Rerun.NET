# Rerun.NET

A cross-platform C# SDK for [Rerun](https://rerun.io) — multimodal data visualization for robotics, spatial AI, and computer vision.

Wraps the native `rerun_c` library via P/Invoke and the Arrow C Data Interface. All 45 archetypes, 79 components, and 61 datatypes are code-generated from the upstream FlatBuffers definitions, plus 67 blueprint types.

## Quick Start

```csharp
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("my_app");
rec.Spawn(); // or rec.Save("output.rrd");

rec.Log("points", new Points3D(
        new Position3D(new Vec3D([1f, 2f, 3f])),
        new Position3D(new Vec3D([4f, 5f, 6f])))
    .WithColors(
        new Color(new Rgba32(0xFF0000FFu)),
        new Color(new Rgba32(0x00FF00FFu)))
    .WithRadii(new Radius(0.5f)));
```

## Features

- **100% archetype coverage** — all 45 Rerun archetypes generated and working
- **Cross-language verified** — 12 cross-language comparison tests confirm C# output is semantically identical to the Rust SDK
- **Cross-platform** — targets win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64
- **Code-generated** — types stay in sync with Rerun via FlatBuffers `.fbs` codegen
- **Testable** — `INativeApi` interface allows mocking the native layer for unit tests without the native binary
- **71 tests** — 39 unit tests + 32 integration tests including backwards compatibility checks

## Examples

| Example | Description |
|---------|-------------|
| [MinimalExample](examples/MinimalExample) | Points3D with colors and radii over 2 frames |
| [Minimal](examples/Minimal) | 10x10x10 grid of 1000 colored points |
| [Clock](examples/Clock) | Animated analog clock with Arrows3D |
| [DNA](examples/DNA) | Double helix with animated beads |
| [Graphs](examples/Graphs) | Graph nodes, edges, lattice, Markov chain |
| [Plots](examples/Plots) | Gaussian, parabola, trig, classification scatter |
| [ScalarPlot](examples/ScalarPlot) | Sin/cos/product scalar plots over time |
| [RrtStar](examples/RrtStar) | RRT* pathfinding algorithm visualization |
| [Multithreading](examples/Multithreading) | Thread-safe logging from 10 concurrent threads |
| [IncrementalLogging](examples/IncrementalLogging) | Efficient partial updates across frames |
| [LiveScrollingPlot](examples/LiveScrollingPlot) | 6 plots x 5 series random walk dashboard |
| [GraphLattice](examples/GraphLattice) | 10x10 lattice graph with directed edges |
| [Arrows3DSpiral](examples/Arrows3DSpiral) | 100 arrows in a spiral with gradient colors |
| [LineStrips3DCube](examples/LineStrips3DCube) | Wireframe cube with colored line strips |
| [TextLogging](examples/TextLogging) | Log entries at different severity levels |
| [ClearDemo](examples/ClearDemo) | Log arrows then clear them one by one |

Run any example:
```bash
dotnet run --project examples/Graphs           # saves to .rrd file
dotnet run --project examples/Graphs -- --spawn # opens Rerun viewer
```

## Requirements

- .NET 8.0+
- Rust toolchain (to build `rerun_c` from source)

## Building from Source

```bash
git clone --recursive https://github.com/KevinGliewe/Rerun.NET.git
cd Rerun.NET

# Build the native library
cd extern/rerun
cargo rustc -p rerun_c --release --crate-type cdylib
cd ../..
mkdir -p runtimes/win-x64/native  # adjust RID for your platform
cp extern/rerun/target/release/rerun_c.dll runtimes/win-x64/native/

# Build and test the SDK
dotnet restore Rerun.Net.slnx
dotnet build Rerun.Net.slnx
dotnet test Rerun.Net.slnx
```

### Optional: Build the Rerun CLI (for integration tests)

```bash
cd extern/rerun
cargo build -p rerun-cli --release --no-default-features
```

### Run the code generator

```bash
dotnet run --project src/Rerun.Net.CodeGen -- \
    extern/rerun/crates/store/re_sdk_types/definitions/rerun \
    src/Rerun.Net
```

## Architecture

Three-layer design mirroring the C++ SDK:

**Layer 1 — Native/** P/Invoke bindings to `rerun_c` using .NET 8 `LibraryImport` (source-generated, AOT-compatible).

**Layer 2 — Core/** Managed wrappers. `RecordingStream` wraps the native handle with `IDisposable`, provides `Log()`, `TryLog()`, sink configuration, timeline management, and `SendColumns()`.

**Layer 3 — Archetypes/Components/Datatypes/** Code-generated from FlatBuffers `.fbs` definitions. Archetypes implement `IAsComponents`, components implement `ILoggable<T>`.

## License

MIT
