// 6 plots with 5 series each, 500 steps of random walk data.
// C# port of the Python "live_scrolling_plot" example (non-realtime, sequential logging).
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;

using var rec = new RecordingStream("rerun_example_live_scrolling_plot");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "live_scrolling_plot.rrd");

const int numPlots = 6;
const int numSeries = 5;
const int numSteps = 500;

var plotNames = new[] { "metrics/cpu", "metrics/memory", "metrics/disk", "sensors/temp", "sensors/pressure", "sensors/humidity" };
var seriesColors = new uint[]
{
    0xE41A1CFFu, // Red
    0x377EB8FFu, // Blue
    0x4DAF4AFFu, // Green
    0xFF7F00FFu, // Orange
    0x984EA3FFu, // Purple
};

var rng = new Random(42);

// Track current values for each series in each plot (random walk)
var values = new double[numPlots, numSeries];
for (var p = 0; p < numPlots; p++)
    for (var s = 0; s < numSeries; s++)
        values[p, s] = rng.NextDouble() * 10;

for (var step = 0; step < numSteps; step++)
{
    rec.SetTimeSequence("step", step);

    for (var p = 0; p < numPlots; p++)
    {
        for (var s = 0; s < numSeries; s++)
        {
            // Random walk: add a small random delta
            values[p, s] += (rng.NextDouble() - 0.5) * 2.0;

            rec.Log($"{plotNames[p]}/series_{s}",
                new Scalars(new Scalar(values[p, s])));
        }
    }
}

Console.WriteLine($"Logged {numSteps} steps across {numPlots} plots with {numSeries} series each ({numPlots * numSeries * numSteps} total scalars). Use --spawn to open viewer.");
