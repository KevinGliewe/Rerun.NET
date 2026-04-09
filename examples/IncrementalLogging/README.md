# IncrementalLogging

Partial/incremental logging: colors and radii are logged once, then only positions update each frame.

## Archetypes

- `Points3D` with `Position3D`, `Color`, `Radius`

## Run

```bash
dotnet run --project examples/IncrementalLogging
dotnet run --project examples/IncrementalLogging -- --spawn   # opens Rerun viewer
```
