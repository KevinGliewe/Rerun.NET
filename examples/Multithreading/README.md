# Multithreading

10 threads each log 100 batches of 5 random 2D boxes, demonstrating thread-safe logging.

## Archetypes

- `Boxes2D` with `HalfSize2D`, `Position2D`, `Color`

## Run

```bash
dotnet run --project examples/Multithreading
dotnet run --project examples/Multithreading -- --spawn   # opens Rerun viewer
```
