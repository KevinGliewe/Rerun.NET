// Log scalar values over time to produce a plot.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;

using var rec = new RecordingStream("rerun_example_scalar_plot");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "scalar_plot.rrd");

for (var i = 0; i < 200; i++)
{
    rec.SetTimeSequence("step", i);
    var t = i * 0.05;
    rec.Log("plots/sin", new Scalars(new Scalar(Math.Sin(t))));
    rec.Log("plots/cos", new Scalars(new Scalar(Math.Cos(t))));
    rec.Log("plots/sincos", new Scalars(new Scalar(Math.Sin(t) * Math.Cos(t * 2))));
}

Console.WriteLine("Logged 200 steps of scalar plots. Use --spawn to open viewer.");
