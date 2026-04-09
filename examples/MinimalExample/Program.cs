// Log Points3D with colors and radii over 2 frames.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_net_minimal_example");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "minimal_example.rrd");

rec.SetTimeSequence("frame", 0);
rec.Log("points", new Points3D(
        new Position3D(new Vec3D([0f, 0f, 0f])),
        new Position3D(new Vec3D([1f, 1f, 1f])),
        new Position3D(new Vec3D([2f, 0f, 0f])))
    .WithColors(
        new Color(new Rgba32(0xFF0000FFu)),
        new Color(new Rgba32(0x00FF00FFu)),
        new Color(new Rgba32(0x0000FFFFu)))
    .WithRadii(new Radius(0.1f)));

rec.SetTimeSequence("frame", 1);
rec.Log("points", new Points3D(
        new Position3D(new Vec3D([0f, 1f, 0f])),
        new Position3D(new Vec3D([1f, 2f, 1f])),
        new Position3D(new Vec3D([2f, 1f, 0f])))
    .WithColors(
        new Color(new Rgba32(0x00FFFFFFu)),
        new Color(new Rgba32(0xFF00FFFFu)),
        new Color(new Rgba32(0xFFFF00FFu))));

Console.WriteLine("Logged 2 frames of Points3D. Use --spawn to open viewer.");
