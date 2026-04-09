// Log a spiral of 3D arrows with varying color.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_arrows3d_spiral");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "arrows3d_spiral.rrd");

const int count = 100;
var vectors = new Vector3D[count];
var origins = new Position3D[count];
var colors = new Color[count];

for (var i = 0; i < count; i++)
{
    var angle = MathF.Tau * i / count;
    var length = MathF.Log2(i + 1);
    vectors[i] = new Vector3D(new Vec3D([
        length * MathF.Sin(angle), 0f, length * MathF.Cos(angle)
    ]));
    origins[i] = new Position3D(new Vec3D([0f, 0f, 0f]));
    var c = (byte)(255f * i / count);
    colors[i] = new Color(new Rgba32((uint)((255 - c) << 24 | c << 16 | 128 << 8 | 128)));
}

rec.Log("arrows", new Arrows3D(vectors)
    .WithOrigins(origins)
    .WithColors(colors));

Console.WriteLine($"Logged {count} arrows in a spiral. Use --spawn to open viewer.");
