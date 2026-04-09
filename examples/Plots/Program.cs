// Various plot types: Gaussian bell curve, parabola, sin/cos, and classification scatter.
// C# port of the Python "plots" example. BarChart is not available, so Scalars are used.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_plots");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "plots.rrd");

// Log a description
rec.Log("description", new TextDocument(
        new Text(new Utf8("# Plots\nThis example demonstrates various plot types using the Rerun SDK.\n" +
                          "- **Gauss**: Gaussian bell curve (sampled as Scalars)\n" +
                          "- **Parabola**: y = x^2 curve\n" +
                          "- **Trig**: sin and cos curves\n" +
                          "- **Classification**: simulated classification scatter")))
    .WithMediaType(new MediaType(new Utf8("text/markdown"))), @static: true);

// --- Gaussian bell curve (as scalars over x-axis) ---
for (var i = 0; i < 100; i++)
{
    var x = -3.0 + 6.0 * i / 99.0;
    var y = Math.Exp(-x * x / 2.0) / Math.Sqrt(2.0 * Math.PI);
    rec.SetTimeSequence("sample", i);
    rec.Log("plots/gauss", new Scalars(new Scalar(y)));
}

// --- Parabola ---
for (var i = 0; i < 100; i++)
{
    var x = -5.0 + 10.0 * i / 99.0;
    var y = x * x;
    rec.SetTimeSequence("sample", i);
    rec.Log("plots/parabola", new Scalars(new Scalar(y)));
}

// --- Trig functions: sin and cos ---
for (var i = 0; i < 200; i++)
{
    var x = i * 0.05;
    rec.SetTimeSequence("sample", i);
    rec.Log("plots/trig/sin", new Scalars(new Scalar(Math.Sin(x))));
    rec.Log("plots/trig/cos", new Scalars(new Scalar(Math.Cos(x))));
}

// --- Classification scatter ---
// Simulate a binary classification dataset with two clusters
var rng = new Random(42);

for (var i = 0; i < 200; i++)
{
    rec.SetTimeSequence("sample", i);

    double cx, cy;
    uint color;

    if (i < 100)
    {
        // Class 0: cluster centered at (-1, -1)
        cx = -1.0 + rng.NextDouble() * 2.0 - 1.0;
        cy = -1.0 + rng.NextDouble() * 2.0 - 1.0;
        color = 0x4488FFFFu; // blue
    }
    else
    {
        // Class 1: cluster centered at (1, 1)
        cx = 1.0 + rng.NextDouble() * 2.0 - 1.0;
        cy = 1.0 + rng.NextDouble() * 2.0 - 1.0;
        color = 0xFF4444FFu; // red
    }

    // Log as two separate scalars (x and y coordinates over time)
    rec.Log("plots/classification/x", new Scalars(new Scalar(cx)));
    rec.Log("plots/classification/y", new Scalars(new Scalar(cy)));
    // Also log a 2D scatter point
    rec.Log("plots/classification/scatter", new Points2D(
            new Position2D(new Vec2D([(float)cx, (float)cy])))
        .WithColors(new Color(new Rgba32(color)))
        .WithRadii(new Radius(3f)));
}

Console.WriteLine("Logged Gaussian, parabola, trig, and classification plots. Use --spawn to open viewer.");
