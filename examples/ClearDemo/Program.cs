// Log arrows then clear them one by one to demonstrate the Clear archetype.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_clear_demo");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "clear_demo.rrd");

var vectors = new Vec3D[] { new([1f, 0f, 0f]), new([0f, -1f, 0f]), new([-1f, 0f, 0f]), new([0f, 1f, 0f]) };
var origins = new Vec3D[] { new([-0.5f, 0.5f, 0f]), new([0.5f, 0.5f, 0f]), new([0.5f, -0.5f, 0f]), new([-0.5f, -0.5f, 0f]) };
var colors = new uint[] { 0xCC0000FFu, 0x00CC00FFu, 0x0000CCFFu, 0xCC00CCFFu };

rec.SetTimeSequence("step", 0);
for (var i = 0; i < 4; i++)
{
    rec.Log($"arrows/{i}", new Arrows3D(new Vector3D(vectors[i]))
        .WithOrigins(new Position3D(origins[i]))
        .WithColors(new Color(new Rgba32(colors[i]))));
}

for (var i = 0; i < 4; i++)
{
    rec.SetTimeSequence("step", i + 1);
    rec.Log($"arrows/{i}", new Clear(new ClearIsRecursive(false)));
}

Console.WriteLine("Logged 4 arrows, cleared one by one. Use --spawn to open viewer.");
