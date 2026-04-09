// Log a 10x10x10 grid of colored 3D points (1000 total).
// C# port of the Python "minimal" example.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_minimal");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "minimal.rrd");

const int gridSize = 10;
var positions = new Position3D[gridSize * gridSize * gridSize];
var colors = new Color[gridSize * gridSize * gridSize];

// numpy mgrid[3 * [slice(-10, 10, 10j)]] iterates z(outer), y, x(inner) then transposes
// positions are (x[ix], y[iy], z[iz]) but iteration order is z, y, x
var steps = new float[gridSize];
var colorSteps = new byte[gridSize];
for (var i = 0; i < gridSize; i++)
{
    steps[i] = -10f + 20f / (gridSize - 1) * i;
    colorSteps[i] = (byte)(255f / (gridSize - 1) * i);
}

var idx = 0;
for (var iz = 0; iz < gridSize; iz++)
    for (var iy = 0; iy < gridSize; iy++)
        for (var ix = 0; ix < gridSize; ix++)
        {
            positions[idx] = new Position3D(new Vec3D([steps[ix], steps[iy], steps[iz]]));
            colors[idx] = new Color(new Rgba32((uint)(colorSteps[ix] << 24 | colorSteps[iy] << 16 | colorSteps[iz] << 8 | 255)));
            idx++;
        }

rec.Log("my_points", new Points3D(positions)
    .WithColors(colors)
    .WithRadii(new Radius(0.5f)));

Console.WriteLine($"Logged {positions.Length} points in a 10x10x10 grid. Use --spawn to open viewer.");
