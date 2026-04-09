// Demonstrate multithreaded logging: 10 threads each log 100 batches of 5 random Boxes2D.
// C# port of the Python "multithreading" example.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_multithreading");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "multithreading.rrd");

const int numThreads = 10;
const int batchesPerThread = 100;
const int rectsPerBatch = 5;

var threads = new Thread[numThreads];
for (var t = 0; t < numThreads; t++)
{
    var threadId = t;
    threads[t] = new Thread(() =>
    {
        var rng = new Random(42 + threadId);
        for (var batch = 0; batch < batchesPerThread; batch++)
        {
            var step = threadId * batchesPerThread + batch;
            rec.SetTimeSequence("step", step);

            var halfSizes = new HalfSize2D[rectsPerBatch];
            var centers = new Position2D[rectsPerBatch];
            var colors = new Color[rectsPerBatch];

            for (var r = 0; r < rectsPerBatch; r++)
            {
                halfSizes[r] = new HalfSize2D(new Vec2D([
                    (float)(rng.NextDouble() * 50 + 10),
                    (float)(rng.NextDouble() * 50 + 10)]));
                centers[r] = new Position2D(new Vec2D([
                    (float)(rng.NextDouble() * 800),
                    (float)(rng.NextDouble() * 600)]));
                colors[r] = new Color(new Rgba32((uint)(
                    rng.Next(256) << 24 | rng.Next(256) << 16 | rng.Next(256) << 8 | 255)));
            }

            rec.Log($"thread/{threadId}/boxes", new Boxes2D(halfSizes)
                .WithCenters(centers)
                .WithColors(colors));
        }
    });
}

// Start all threads
foreach (var t in threads) t.Start();
// Wait for all to finish
foreach (var t in threads) t.Join();

Console.WriteLine($"Logged {numThreads * batchesPerThread * rectsPerBatch} rectangles from {numThreads} threads. Use --spawn to open viewer.");
