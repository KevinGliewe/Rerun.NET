// Log 3D line strips forming a wireframe cube.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_line_strips3d_cube");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "line_strips3d_cube.rrd");

var bottom = new LineStrip3D([
    new Vec3D([0f, 0f, 0f]), new Vec3D([1f, 0f, 0f]),
    new Vec3D([1f, 1f, 0f]), new Vec3D([0f, 1f, 0f]),
    new Vec3D([0f, 0f, 0f]),
]);
var top = new LineStrip3D([
    new Vec3D([0f, 0f, 1f]), new Vec3D([1f, 0f, 1f]),
    new Vec3D([1f, 1f, 1f]), new Vec3D([0f, 1f, 1f]),
    new Vec3D([0f, 0f, 1f]),
]);
var verticals = new[] {
    new LineStrip3D([new Vec3D([0f, 0f, 0f]), new Vec3D([0f, 0f, 1f])]),
    new LineStrip3D([new Vec3D([1f, 0f, 0f]), new Vec3D([1f, 0f, 1f])]),
    new LineStrip3D([new Vec3D([1f, 1f, 0f]), new Vec3D([1f, 1f, 1f])]),
    new LineStrip3D([new Vec3D([0f, 1f, 0f]), new Vec3D([0f, 1f, 1f])]),
};

rec.Log("cube/bottom", new LineStrips3D(bottom).WithColors(new Color(new Rgba32(0xFF0000FFu))));
rec.Log("cube/top", new LineStrips3D(top).WithColors(new Color(new Rgba32(0x00FF00FFu))));
rec.Log("cube/verticals", new LineStrips3D(verticals).WithColors(new Color(new Rgba32(0x0000FFFFu))));

Console.WriteLine("Logged a wireframe cube. Use --spawn to open viewer.");
