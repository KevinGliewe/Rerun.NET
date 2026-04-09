# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Rerun.NET is a cross-platform C# SDK for [Rerun](https://rerun.io), wrapping the `rerun_c` native library via P/Invoke and the Arrow C Data Interface. The full Rerun source is vendored as a git submodule in `extern/rerun/`.

## Cross-Platform

The SDK targets **win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64**. All managed code is platform-agnostic. The native `rerun_c` shared library must be built per-RID and bundled under `runtimes/{rid}/native/` following the standard NuGet convention. `LibraryLoader.cs` uses `NativeLibrary.SetDllImportResolver` to locate the correct binary at runtime. CI must build and test on all target platforms. Do not use platform-specific APIs in managed code — stick to `RuntimeInformation.RuntimeIdentifier` and `NativeLibrary` for any platform branching.

## Build Commands

```bash
dotnet restore Rerun.Net.slnx      # Restore NuGet packages
dotnet build Rerun.Net.slnx        # Build all projects
dotnet test Rerun.Net.slnx         # Run all tests
dotnet test tests/Rerun.Net.Tests   # Run unit tests only
dotnet run --project src/Rerun.Net.CodeGen -- <definitions-path> <output-path>  # Run code generator
```

## Architecture

Three-layer design mirroring the C++ SDK:

**Layer 1 — Native/** P/Invoke bindings to `rerun_c`. Uses .NET 8 `LibraryImport` (source-generated, AOT-compatible). `NativeStructs.cs` maps all C structs including `ArrowArray`/`ArrowSchema` from the Arrow C Data Interface. `LibraryLoader.cs` resolves the native library at runtime via `NativeLibrary.SetDllImportResolver`, searching `runtimes/{rid}/native/`.

**Layer 2 — Core/** Managed wrappers. `RecordingStream` is the central class — wraps a `uint32` native handle with `IDisposable`, provides `Log()`, sink configuration (`ConnectGrpc`, `Save`, `Spawn`), and thread-local timeline management (`SetTime`, `ResetTime`). All fallible native calls go through `RerunException.ThrowIfError()`.

**Layer 3 — Archetypes/, Components/, Datatypes/** Code-generated from FlatBuffers `.fbs` definitions in `extern/rerun/crates/store/re_sdk_types/definitions/rerun/`. Archetypes implement `IAsComponents`, components implement `ILoggable<T>`. Generated files use `.g.cs` suffix; manual extensions use `.ext.cs` with `partial class`.

**CodeGen** (`src/Rerun.Net.CodeGen/`) is a standalone C# console app that parses `.fbs` files and emits C# source into the SDK project.

## Key Interfaces

- `IAsComponents` — archetypes return `IReadOnlyList<ComponentBatch>` via `AsBatches()`
- `ILoggable<T>` — components serialize to Arrow arrays via static `ToArrow(ReadOnlySpan<T>)` and expose a `ComponentDescriptor`

## FFI Pattern

Data crosses the native boundary via the **Arrow C Data Interface** (zero-copy `ArrowArray`/`ArrowSchema` structs), not Arrow IPC bytes. The `Apache.Arrow` NuGet package provides `CArrowArrayExporter`/`CArrowSchemaExporter` for this. Strings are passed as pinned UTF-8 bytes via `RrString` (borrowed, non-owning views). All memory for output parameters is caller-allocated.

## Testing Strategy

- **Unit tests** (`tests/Rerun.Net.Tests/`) — test managed wrapper logic with mocked native layer
- **Integration tests** (`tests/Rerun.Net.IntegrationTests/`) — produce `.rrd` files and compare against Python/Rust reference outputs using `rerun rrd compare --unordered`

## Build Configuration

All projects target `net8.0` with nullable enabled and `AllowUnsafeBlocks` (required for pointer-based Arrow FFI). Centralized in `Directory.Build.props`.
