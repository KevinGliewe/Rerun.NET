// Double helix DNA visualization: two spirals of Points3D, LineStrips3D scaffolding,
// animated beads bouncing between strands, and a rotating Transform3D.
// C# port of the Python "dna" example.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_dna");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "dna.rrd");

const int numPoints = 100;
const int numFrames = 200;

// Helper: bounce interpolation between a and b
static float BounceLerp(float a, float b, float t)
{
    return a + (b - a) * ((float)(Math.Sin(t * Math.PI * 2.0 - Math.PI / 2.0) + 1.0) / 2.0f);
}

// Helper: HSV to RGBA (h in [0,1], s in [0,1], v in [0,1])
static uint HsvToRgba(float h, float s, float v)
{
    var hi = (int)(h * 6f) % 6;
    var f = h * 6f - (int)(h * 6f);
    var p = v * (1f - s);
    var q = v * (1f - f * s);
    var t2 = v * (1f - (1f - f) * s);

    float r, g, b;
    switch (hi)
    {
        case 0: r = v; g = t2; b = p; break;
        case 1: r = q; g = v; b = p; break;
        case 2: r = p; g = v; b = t2; break;
        case 3: r = p; g = q; b = v; break;
        case 4: r = t2; g = p; b = v; break;
        default: r = v; g = p; b = q; break;
    }
    return (uint)((byte)(r * 255) << 24 | (byte)(g * 255) << 16 | (byte)(b * 255) << 8 | 255);
}

// Build two color spirals (the DNA strands)
static (Position3D[] positions, Color[] colors) BuildColorSpiral(int n, float offsetAngle)
{
    var positions = new Position3D[n];
    var colors = new Color[n];
    for (var i = 0; i < n; i++)
    {
        var t = (float)i / (n - 1);
        var angle = t * MathF.PI * 4f + offsetAngle;
        var x = MathF.Cos(angle) * 2f;
        var y = MathF.Sin(angle) * 2f;
        var z = t * 10f - 5f;
        positions[i] = new Position3D(new Vec3D([x, y, z]));

        // Color based on angle (HSV hue)
        var hue = (t + offsetAngle / (MathF.PI * 2f)) % 1f;
        if (hue < 0) hue += 1f;
        colors[i] = new Color(new Rgba32(HsvToRgba(hue, 0.8f, 0.9f)));
    }
    return (positions, colors);
}

// Log the two strands statically
var (strand1Pos, strand1Col) = BuildColorSpiral(numPoints, 0f);
var (strand2Pos, strand2Col) = BuildColorSpiral(numPoints, MathF.PI);

rec.Log("dna/strand1", new Points3D(strand1Pos)
    .WithColors(strand1Col)
    .WithRadii(new Radius(0.08f)), @static: true);

rec.Log("dna/strand2", new Points3D(strand2Pos)
    .WithColors(strand2Col)
    .WithRadii(new Radius(0.08f)), @static: true);

// Log scaffolding: line strips connecting corresponding points on the two strands
const int scaffoldStep = 5; // Connect every 5th point
var scaffolds = new LineStrip3D[(numPoints + scaffoldStep - 1) / scaffoldStep];
var scaffoldColors = new Color[scaffolds.Length];
for (var i = 0; i < scaffolds.Length; i++)
{
    var pi = i * scaffoldStep;
    if (pi >= numPoints) break;
    var p1 = strand1Pos[pi];
    var p2 = strand2Pos[pi];
    scaffolds[i] = new LineStrip3D([
        new Vec3D([p1.Xyz.Xyz[0], p1.Xyz.Xyz[1], p1.Xyz.Xyz[2]]),
        new Vec3D([p2.Xyz.Xyz[0], p2.Xyz.Xyz[1], p2.Xyz.Xyz[2]])
    ]);
    scaffoldColors[i] = new Color(new Rgba32(0x888888AAu));
}

rec.Log("dna/scaffolding", new LineStrips3D(scaffolds)
    .WithColors(scaffoldColors)
    .WithRadii(new Radius(0.02f)), @static: true);

// Animate beads bouncing along the scaffolds
var numBeads = scaffolds.Length;

for (var frame = 0; frame < numFrames; frame++)
{
    rec.SetTimeSequence("frame", frame);

    var beadPositions = new Position3D[numBeads];
    var beadColors = new Color[numBeads];

    for (var b = 0; b < numBeads; b++)
    {
        var pi = b * scaffoldStep;
        if (pi >= numPoints) break;

        // Each bead bounces at a different phase
        var phase = (float)frame / numFrames * 2f + (float)b / numBeads * MathF.PI;
        var p1 = strand1Pos[pi];
        var p2 = strand2Pos[pi];

        var bx = BounceLerp(p1.Xyz.Xyz[0], p2.Xyz.Xyz[0], phase);
        var by = BounceLerp(p1.Xyz.Xyz[1], p2.Xyz.Xyz[1], phase);
        var bz = BounceLerp(p1.Xyz.Xyz[2], p2.Xyz.Xyz[2], phase);

        beadPositions[b] = new Position3D(new Vec3D([bx, by, bz]));
        beadColors[b] = new Color(new Rgba32(0xFFFF00FFu));
    }

    rec.Log("dna/beads", new Points3D(beadPositions)
        .WithColors(beadColors)
        .WithRadii(new Radius(0.12f)));

    // Slowly rotate the whole structure
    var rotAngle = (float)(frame * Math.PI * 2.0 / numFrames * 0.25);
    rec.Log("dna", new Transform3D()
        .WithRotationAxisAngle(new Rerun.Net.Components.RotationAxisAngle(
            new Rerun.Net.Datatypes.RotationAxisAngle(new Vec3D([0f, 0f, 1f]), new Rerun.Net.Datatypes.Angle(rotAngle)))));
}

Console.WriteLine($"Logged DNA double helix with {numPoints} strand points and {numFrames} animation frames. Use --spawn to open viewer.");
