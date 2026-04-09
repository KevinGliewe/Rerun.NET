// Demonstrate incremental/partial logging: colors and radii are logged once at frame 0,
// then only positions change for the remaining frames.
// C# port of the Python "incremental_logging" example.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_incremental_logging");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "incremental_logging.rrd");

const int numPoints = 10;
const int numFrames = 10;
var rng = new Random(42);

// Log static colors and radii once at frame 0
rec.SetTimeSequence("frame", 0);

var colors = new Color[numPoints];
var radii = new Radius[numPoints];
for (var i = 0; i < numPoints; i++)
{
    // Different color per point using HSV-like scheme
    var hue = (float)i / numPoints;
    var r = (byte)(255 * Math.Max(0, Math.Min(1, Math.Abs(hue * 6 - 3) - 1)));
    var g = (byte)(255 * Math.Max(0, Math.Min(1, 2 - Math.Abs(hue * 6 - 2))));
    var b = (byte)(255 * Math.Max(0, Math.Min(1, 2 - Math.Abs(hue * 6 - 4))));
    colors[i] = new Color(new Rgba32((uint)(r << 24 | g << 16 | b << 8 | 255)));
    radii[i] = new Radius(0.1f + 0.3f * i / numPoints);
}

// First frame: log full archetype with positions + colors + radii
var initialPositions = new Position3D[numPoints];
for (var i = 0; i < numPoints; i++)
{
    initialPositions[i] = new Position3D(new Vec3D([
        (float)(rng.NextDouble() * 10 - 5),
        (float)(rng.NextDouble() * 10 - 5),
        (float)(rng.NextDouble() * 10 - 5)]));
}

rec.Log("points", new Points3D(initialPositions)
    .WithColors(colors)
    .WithRadii(radii));

// Subsequent frames: only log new positions (colors and radii persist)
for (var frame = 1; frame < numFrames; frame++)
{
    rec.SetTimeSequence("frame", frame);

    var positions = new Position3D[numPoints];
    for (var i = 0; i < numPoints; i++)
    {
        // Move each point slightly from the initial position
        var angle = (float)(frame * Math.PI * 2 / numFrames + i * Math.PI * 2 / numPoints);
        positions[i] = new Position3D(new Vec3D([
            initialPositions[i].Xyz.Xyz[0] + MathF.Cos(angle) * 2f,
            initialPositions[i].Xyz.Xyz[1] + MathF.Sin(angle) * 2f,
            initialPositions[i].Xyz.Xyz[2] + MathF.Sin(angle * 0.5f)]));
    }

    // Only log positions — colors and radii carry forward from frame 0
    rec.Log("points", new Points3D(positions));
}

Console.WriteLine($"Logged {numPoints} points across {numFrames} frames. Colors and radii logged once at frame 0, only positions updated after. Use --spawn to open viewer.");
