// Log text entries at different severity levels.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_text_logging");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "text_logging.rrd");

var messages = new (string Level, string Message)[]
{
    ("INFO", "Application started"),
    ("DEBUG", "Initializing subsystems..."),
    ("INFO", "Loading configuration from disk"),
    ("WARN", "Config file not found, using defaults"),
    ("INFO", "Processing 1000 items"),
    ("DEBUG", "Batch 1/10 complete"),
    ("DEBUG", "Batch 5/10 complete"),
    ("ERROR", "Failed to process item 742: timeout"),
    ("WARN", "Retrying failed items..."),
    ("INFO", "Processing complete: 999/1000 succeeded"),
};

for (var i = 0; i < messages.Length; i++)
{
    rec.SetTimeSequence("log_index", i);
    rec.Log("logs", new TextLog(new Text(new Utf8(messages[i].Message)))
        .WithLevel(new TextLogLevel(new Utf8(messages[i].Level))));
}

Console.WriteLine($"Logged {messages.Length} text entries. Use --spawn to open viewer.");
