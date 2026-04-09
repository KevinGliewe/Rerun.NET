using System.Diagnostics;
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;
using Xunit;
using Xunit.Abstractions;

namespace Rerun.Net.IntegrationTests;

/// <summary>
/// Cross-language .rrd comparison tests.
/// Each test logs data from C#, writes to .rrd, validates with rerun CLI.
/// Tests skip when the rerun CLI is absent.
/// </summary>
public class RrdCompareTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tmpDir;
    private static readonly string? RerunCli = FindRerunCli();

    public RrdCompareTests(ITestOutputHelper output)
    {
        _output = output;
        _tmpDir = Path.Combine(Path.GetTempPath(), $"rerun_integ_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmpDir, true); } catch { }
    }

    // ── Tier 1: Core spatial archetypes ──────────────────────────────────

    [SkippableFact]
    public void T1_Points3DSimple()
    {
        var rrd = LogToRrd("points3d_simple", rec =>
        {
            rec.Log("points", new Points3D(
                new Position3D(new Vec3D([0f, 0f, 0f])),
                new Position3D(new Vec3D([1f, 1f, 1f]))));
        });
        AssertRrdValid(rrd);
    }

    [SkippableFact]
    public void T1_Points3DWithProperties()
    {
        var rrd = LogToRrd("points3d_props", rec =>
        {
            rec.Log("points", new Points3D(
                    new Position3D(new Vec3D([1f, 2f, 3f])),
                    new Position3D(new Vec3D([4f, 5f, 6f])),
                    new Position3D(new Vec3D([7f, 8f, 9f])))
                .WithColors(
                    new Color(new Rgba32(0xFF0000FFu)),
                    new Color(new Rgba32(0x00FF00FFu)),
                    new Color(new Rgba32(0x0000FFFFu)))
                .WithRadii(
                    new Radius(0.5f),
                    new Radius(1.0f),
                    new Radius(0.25f))
                .WithLabels(
                    new Text(new Utf8("A")),
                    new Text(new Utf8("B")),
                    new Text(new Utf8("C"))));
        });
        AssertRrdValid(rrd);
    }

    [SkippableFact]
    public void T1_Arrows3DSimple()
    {
        var rrd = LogToRrd("arrows3d_simple", rec =>
        {
            rec.Log("arrows", new Arrows3D(
                    new Components.Vector3D(new Vec3D([1f, 0f, 0f])),
                    new Components.Vector3D(new Vec3D([0f, 1f, 0f])),
                    new Components.Vector3D(new Vec3D([0f, 0f, 1f])))
                .WithOrigins(
                    new Position3D(new Vec3D([0f, 0f, 0f])),
                    new Position3D(new Vec3D([1f, 0f, 0f])),
                    new Position3D(new Vec3D([0f, 1f, 0f])))
                .WithColors(
                    new Color(new Rgba32(0xFF0000FFu)),
                    new Color(new Rgba32(0x00FF00FFu)),
                    new Color(new Rgba32(0x0000FFFFu))));
        });
        AssertRrdValid(rrd);
    }

    [SkippableFact]
    public void T1_Boxes2DSimple()
    {
        var rrd = LogToRrd("boxes2d_simple", rec =>
        {
            rec.Log("simple", new Boxes2D(
                    new HalfSize2D(new Vec2D([1f, 1f])))
                .WithCenters(
                    new Components.Position2D(new Vec2D([0f, 0f]))));
        });
        AssertRrdValid(rrd);
    }

    [SkippableFact]
    public void T1_LineStrips2DSimple()
    {
        var rrd = LogToRrd("line_strips2d_simple", rec =>
        {
            rec.Log("strip", new LineStrips2D(
                new LineStrip2D([
                    new Vec2D([0f, 0f]),
                    new Vec2D([2f, 1f]),
                    new Vec2D([4f, -1f]),
                    new Vec2D([6f, 0f]),
                ])));
        });
        AssertRrdValid(rrd);
    }

    [SkippableFact]
    public void T1_LineStrips3DSimple()
    {
        var rrd = LogToRrd("line_strips3d_simple", rec =>
        {
            rec.Log("strip", new LineStrips3D(
                new LineStrip3D([
                    new Vec3D([0f, 0f, 0f]),
                    new Vec3D([0f, 0f, 1f]),
                    new Vec3D([1f, 0f, 0f]),
                    new Vec3D([1f, 0f, 1f]),
                    new Vec3D([1f, 1f, 0f]),
                    new Vec3D([1f, 1f, 1f]),
                    new Vec3D([0f, 1f, 0f]),
                    new Vec3D([0f, 1f, 1f]),
                ])));
        });
        AssertRrdValid(rrd);
    }

    // ── Tier 2: Additional archetypes ────────────────────────────────────

    [SkippableFact]
    public void T2_GeoPointsSimple()
    {
        var rrd = LogToRrd("geo_points_simple", rec =>
        {
            rec.Log("rerun_hq", new GeoPoints(
                    new LatLon(new DVec2D([59.319221, 18.075631])))
                .WithRadii(new Radius(-10.0f))  // negative = UI points
                .WithColors(new Color(new Rgba32(0xFF0000FFu))));
        });
        AssertRrdValid(rrd);
    }

    [SkippableFact]
    public void T2_ClearSimple()
    {
        var rrd = LogToRrd("clear_simple", rec =>
        {
            // Log some data, then clear it
            rec.SetTimeSequence("step", 0);
            rec.Log("points", new Points3D(
                new Position3D(new Vec3D([1f, 2f, 3f]))));

            rec.SetTimeSequence("step", 1);
            rec.Log("points", new Clear(new ClearIsRecursive(false)));
        });
        AssertRrdValid(rrd);
    }

    [SkippableFact]
    public void T2_TextLogSimple()
    {
        var rrd = LogToRrd("text_log_simple", rec =>
        {
            rec.Log("logs", new TextLog(new Text(new Utf8("Hello from C#")))
                .WithLevel(new TextLogLevel(new Utf8("INFO"))));
        });
        AssertRrdValid(rrd);
    }

    [SkippableFact]
    public void T2_ScalarsSimple()
    {
        var rrd = LogToRrd("scalars_simple", rec =>
        {
            for (var i = 0; i < 10; i++)
            {
                rec.SetTimeSequence("step", i);
                rec.Log("plot/value", new Scalars(new Scalar(Math.Sin(i * 0.1))));
            }
        });
        AssertRrdValid(rrd);
    }

    [SkippableFact]
    public void T2_Transform3DSimple()
    {
        var rrd = LogToRrd("transform3d_simple", rec =>
        {
            rec.Log("transform", new Transform3D()
                .WithTranslation(new Components.Translation3D(new Vec3D([1f, 2f, 3f]))));
        });
        AssertRrdValid(rrd);
    }

    // ── Tier 3: Timeline and API behavior ────────────────────────────────

    [SkippableFact]
    public void T3_MultipleTimelines()
    {
        var rrd = LogToRrd("multiple_timelines", rec =>
        {
            rec.SetTimeSequence("frame", 42);
            rec.SetTime("timestamp", TimeType.Timestamp, 1_000_000_000);
            rec.Log("points", new Points3D(
                new Position3D(new Vec3D([1f, 2f, 3f]))));
        });
        AssertRrdValid(rrd);
    }

    [SkippableFact]
    public void T3_StaticLogging()
    {
        var rrd = LogToRrd("static_logging", rec =>
        {
            rec.Log("static_points", new Points3D(
                new Position3D(new Vec3D([0f, 0f, 0f]))), @static: true);
        });
        AssertRrdValid(rrd);
    }

    [SkippableFact]
    public void T3_MultipleEntities()
    {
        var rrd = LogToRrd("multiple_entities", rec =>
        {
            rec.Log("world/a", new Points3D(new Position3D(new Vec3D([0f, 0f, 0f]))));
            rec.Log("world/b", new Points3D(new Position3D(new Vec3D([1f, 1f, 1f]))));
            rec.Log("world/c", new Points3D(new Position3D(new Vec3D([2f, 2f, 2f]))));
        });
        if (RerunCli != null)
        {
            var rrdPrint = RunRerun("rrd", "print", rrd);
            Assert.Contains("world/a", rrdPrint.StdOut);
            Assert.Contains("world/b", rrdPrint.StdOut);
            Assert.Contains("world/c", rrdPrint.StdOut);
        }
    }

    [SkippableFact]
    public void T3_RowUpdates()
    {
        var rrd = LogToRrd("row_updates", rec =>
        {
            for (var i = 0; i < 5; i++)
            {
                rec.SetTimeSequence("frame", i);
                rec.Log("moving_point", new Points3D(
                    new Position3D(new Vec3D([i * 1.0f, 0f, 0f]))));
            }
        });
        AssertRrdValid(rrd);
    }

    // ── Tier 4: Cross-language comparison ─────────────────────────────────

    [SkippableFact]
    public void T4_SelfCompareIsIdentical()
    {
        Skip.If(RerunCli == null, "rerun CLI not found");
        const string appId = "rerun_integ_self_compare";
        var rrd1 = LogToRrd("self_cmp_1", rec =>
        {
            rec.Log("points", new Points3D(
                new Position3D(new Vec3D([0f, 0f, 0f])),
                new Position3D(new Vec3D([1f, 1f, 1f]))));
        }, appId);
        var rrd2 = LogToRrd("self_cmp_2", rec =>
        {
            rec.Log("points", new Points3D(
                new Position3D(new Vec3D([0f, 0f, 0f])),
                new Position3D(new Vec3D([1f, 1f, 1f]))));
        }, appId);

        var result = RunRerunCompare(rrd1, rrd2);
        _output.WriteLine($"Compare exit: {result.ExitCode}");
        if (result.ExitCode != 0) _output.WriteLine(result.StdErr);
        Assert.True(result.ExitCode == 0, $"Self-compare failed: {result.StdErr}");
    }

    [SkippableFact]
    public void T4_Points3DSimple_VsRust()
    {
        var rustRrd = GenerateRustReference("points3d_simple");
        var csharpRrd = LogToRrd("points3d_vs_rust", rec =>
        {
            rec.Log("points", new Points3D(
                new Position3D(new Vec3D([0f, 0f, 0f])),
                new Position3D(new Vec3D([1f, 1f, 1f]))));
        }, "rerun_example_points3d");

        AssertCrossLanguageMatch(csharpRrd, rustRrd, "points3d_simple");
    }

    [SkippableFact]
    public void T4_Arrows3DSimple_VsRust()
    {
        var rustRrd = GenerateRustReference("arrows3d_simple");
        var csharpRrd = LogToRrd("arrows3d_vs_rust", rec =>
        {
            rec.Log("arrows", new Arrows3D(
                    new Components.Vector3D(new Vec3D([1f, 0f, 0f])),
                    new Components.Vector3D(new Vec3D([0f, 1f, 0f])),
                    new Components.Vector3D(new Vec3D([0f, 0f, 1f])))
                .WithOrigins(
                    new Position3D(new Vec3D([0f, 0f, 0f])),
                    new Position3D(new Vec3D([0f, 0f, 0f])),
                    new Position3D(new Vec3D([0f, 0f, 0f])))
                .WithColors(
                    new Color(new Rgba32(0xFF0000FFu)),
                    new Color(new Rgba32(0x00FF00FFu)),
                    new Color(new Rgba32(0x0000FFFFu))));
        }, "rerun_example_arrow3d");

        AssertCrossLanguageMatch(csharpRrd, rustRrd, "arrows3d_simple");
    }

    [SkippableFact]
    public void T4_Boxes2DSimple_VsRust()
    {
        var rustRrd = GenerateRustReference("boxes2d_simple");
        var csharpRrd = LogToRrd("boxes2d_vs_rust", rec =>
        {
            rec.Log("simple", new Boxes2D(
                    new HalfSize2D(new Vec2D([1f, 1f])))
                .WithCenters(
                    new Components.Position2D(new Vec2D([0f, 0f]))));
        }, "rerun_example_box2d");

        AssertCrossLanguageMatch(csharpRrd, rustRrd, "boxes2d_simple");
    }

    [SkippableFact]
    public void T4_LineStrips3DSimple_VsRust()
    {
        var rustRrd = GenerateRustReference("line_strips3d_simple");
        var csharpRrd = LogToRrd("line_strips3d_vs_rust", rec =>
        {
            rec.Log("strip", new LineStrips3D(
                new LineStrip3D([
                    new Vec3D([0f, 0f, 0f]),
                    new Vec3D([0f, 0f, 1f]),
                    new Vec3D([1f, 0f, 0f]),
                    new Vec3D([1f, 0f, 1f]),
                    new Vec3D([1f, 1f, 0f]),
                    new Vec3D([1f, 1f, 1f]),
                    new Vec3D([0f, 1f, 0f]),
                    new Vec3D([0f, 1f, 1f]),
                ])));
        }, "rerun_example_line_strip3d");

        AssertCrossLanguageMatch(csharpRrd, rustRrd, "line_strips3d_simple");
    }

    [SkippableFact]
    public void T4_Points3DProps_VsRust()
    {
        var rustRrd = GenerateRustReference("points3d_props");
        var csharpRrd = LogToRrd("points3d_props_vs_rust", rec =>
        {
            rec.Log("points", new Points3D(
                    new Position3D(new Vec3D([1f, 2f, 3f])),
                    new Position3D(new Vec3D([4f, 5f, 6f])),
                    new Position3D(new Vec3D([7f, 8f, 9f])))
                .WithColors(
                    new Color(new Rgba32(0xFF0000FFu)),
                    new Color(new Rgba32(0x00FF00FFu)),
                    new Color(new Rgba32(0x0000FFFFu)))
                .WithRadii(new Radius(0.5f), new Radius(1.0f), new Radius(0.25f))
                .WithLabels(
                    new Text(new Utf8("A")),
                    new Text(new Utf8("B")),
                    new Text(new Utf8("C"))));
        }, "rerun_example_points3d_props");
        AssertCrossLanguageMatch(csharpRrd, rustRrd, "points3d_props");
    }

    [SkippableFact]
    public void T4_LineStrips2DSimple_VsRust()
    {
        var rustRrd = GenerateRustReference("line_strips2d_simple");
        var csharpRrd = LogToRrd("line_strips2d_vs_rust", rec =>
        {
            rec.Log("strip", new LineStrips2D(
                new LineStrip2D([
                    new Vec2D([0f, 0f]), new Vec2D([2f, 1f]),
                    new Vec2D([4f, -1f]), new Vec2D([6f, 0f]),
                ])));
        }, "rerun_example_line_strip2d");
        AssertCrossLanguageMatch(csharpRrd, rustRrd, "line_strips2d_simple");
    }

    [SkippableFact]
    public void T4_GeoPointsSimple_VsRust()
    {
        var rustRrd = GenerateRustReference("geo_points_simple");
        var csharpRrd = LogToRrd("geo_points_vs_rust", rec =>
        {
            rec.Log("rerun_hq", new GeoPoints(
                    new LatLon(new DVec2D([59.319221, 18.075631])))
                .WithRadii(new Radius(-10.0f))
                .WithColors(new Color(new Rgba32(0xFF0000FFu))));
        }, "rerun_example_geo_points");
        AssertCrossLanguageMatch(csharpRrd, rustRrd, "geo_points_simple");
    }

    [SkippableFact]
    public void T4_ClearSimple_VsRust()
    {
        var rustRrd = GenerateRustReference("clear_simple");
        var csharpRrd = LogToRrd("clear_vs_rust", rec =>
        {
            rec.SetTimeSequence("step", 0);
            rec.Log("points", new Points3D(new Position3D(new Vec3D([1f, 2f, 3f]))));
            rec.SetTimeSequence("step", 1);
            rec.Log("points", new Clear(new ClearIsRecursive(false)));
        }, "rerun_example_clear");
        AssertCrossLanguageMatch(csharpRrd, rustRrd, "clear_simple");
    }

    [SkippableFact]
    public void T4_TextLogSimple_VsRust()
    {
        var rustRrd = GenerateRustReference("text_log_simple");
        var csharpRrd = LogToRrd("text_log_vs_rust", rec =>
        {
            rec.Log("logs", new TextLog(new Text(new Utf8("Hello from Rust")))
                .WithLevel(new TextLogLevel(new Utf8("INFO"))));
        }, "rerun_example_text_log");
        AssertCrossLanguageMatch(csharpRrd, rustRrd, "text_log_simple");
    }

    [SkippableFact]
    public void T4_ScalarsSimple_VsRust()
    {
        var rustRrd = GenerateRustReference("scalars_simple");
        var csharpRrd = LogToRrd("scalars_vs_rust", rec =>
        {
            for (var i = 0; i < 10; i++)
            {
                rec.SetTimeSequence("step", i);
                rec.Log("plot/value", new Scalars(new Scalar(Math.Sin(i * 0.1))));
            }
        }, "rerun_example_scalars");
        AssertCrossLanguageMatch(csharpRrd, rustRrd, "scalars_simple");
    }

    [SkippableFact]
    public void T4_Transform3DSimple_VsRust()
    {
        var rustRrd = GenerateRustReference("transform3d_simple");
        var csharpRrd = LogToRrd("transform3d_vs_rust", rec =>
        {
            rec.Log("transform", new Transform3D()
                .WithTranslation(new Components.Translation3D(new Vec3D([1f, 2f, 3f]))));
        }, "rerun_example_transform3d");
        AssertCrossLanguageMatch(csharpRrd, rustRrd, "transform3d_simple");
    }

    [SkippableFact]
    public void T4_Minimal_VsRust()
    {
        var rustRrd = GenerateRustReference("minimal");
        var csharpRrd = LogToRrd("minimal_vs_rust", rec =>
        {
            const int gridSize = 10;
            var positions = new Position3D[1000];
            var colors = new Color[1000];
            var steps = new float[gridSize];
            var colorSteps = new byte[gridSize];
            for (var i = 0; i < gridSize; i++)
            {
                steps[i] = -10f + 20f / 9f * i;
                colorSteps[i] = (byte)(255f / 9f * i);
            }
            var idx = 0;
            for (var iz = 0; iz < gridSize; iz++)
                for (var iy = 0; iy < gridSize; iy++)
                    for (var ix = 0; ix < gridSize; ix++)
                    {
                        positions[idx] = new Position3D(new Vec3D([steps[ix], steps[iy], steps[iz]]));
                        colors[idx] = new Color(new Rgba32((uint)(colorSteps[ix] << 24 | colorSteps[iy] << 16 | colorSteps[iz] << 8 | 255)));
                        idx++;
                    }
            rec.Log("my_points", new Points3D(positions).WithColors(colors).WithRadii(new Radius(0.5f)));
        }, "rerun_example_minimal");
        AssertCrossLanguageMatch(csharpRrd, rustRrd, "minimal");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private string LogToRrd(string name, Action<RecordingStream> log, string? appId = null)
    {
        var rrdPath = Path.Combine(_tmpDir, $"{name}.rrd");
        using (var rec = new RecordingStream(appId ?? $"rerun_integ_{name}"))
        {
            rec.Save(rrdPath);
            log(rec);
        }
        Assert.True(File.Exists(rrdPath), $"RRD file not created: {rrdPath}");
        Assert.True(new FileInfo(rrdPath).Length > 0, "RRD file is empty");
        _output.WriteLine($"{name}.rrd: {new FileInfo(rrdPath).Length} bytes");
        return rrdPath;
    }

    private void AssertRrdValid(string rrdPath)
    {
        if (RerunCli == null) return; // CLI validation optional — file existence already checked in LogToRrd

        var result = RunRerun("rrd", "print", rrdPath);
        Assert.True(result.ExitCode == 0, $"rrd print failed (exit {result.ExitCode}): {result.StdErr}");

        var cmp = RunRerunCompare(rrdPath, rrdPath);
        Assert.True(cmp.ExitCode == 0, $"Self-compare failed: {cmp.StdErr}");
    }

    private string GenerateRustReference(string snippetName)
    {
        var rustGen = FindRustReferenceGen();
        Skip.If(rustGen == null, "Rust reference generator not built");

        var rrdPath = Path.Combine(_tmpDir, $"rust_{snippetName}.rrd");
        var psi = new ProcessStartInfo
        {
            FileName = rustGen!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(snippetName);
        psi.ArgumentList.Add(rrdPath);

        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        proc.WaitForExit(30_000);
        var stderr = stderrTask.Result;
        Assert.True(proc.ExitCode == 0, $"Rust reference gen failed for {snippetName}: {stderr}");

        // Ensure .rrd file is fully written and handle released
        Thread.Sleep(1000);

        Assert.True(File.Exists(rrdPath), $"Rust reference .rrd not created: {rrdPath}");
        _output.WriteLine($"rust_{snippetName}.rrd: {new FileInfo(rrdPath).Length} bytes");
        return rrdPath;
    }

    private void AssertCrossLanguageMatch(string csharpRrd, string rustRrd, string snippetName)
    {
        var result = RunRerunCompare(csharpRrd, rustRrd);
        _output.WriteLine($"Cross-language compare ({snippetName}): exit {result.ExitCode}");
        if (result.ExitCode != 0)
        {
            _output.WriteLine($"STDERR: {result.StdErr}");
            // Dump both for debugging
            var csPrint = RunRerun("rrd", "print", csharpRrd);
            var rsPrint = RunRerun("rrd", "print", rustRrd);
            _output.WriteLine($"=== C# rrd ===\n{csPrint.StdOut[..Math.Min(1000, csPrint.StdOut.Length)]}");
            _output.WriteLine($"=== Rust rrd ===\n{rsPrint.StdOut[..Math.Min(1000, rsPrint.StdOut.Length)]}");
        }
        Assert.True(result.ExitCode == 0,
            $"Cross-language .rrd mismatch for {snippetName}: {result.StdErr}");
    }

    private static string? FindRustReferenceGen()
    {
        // From test bin dir: bin/Debug/net8.0 -> ../../RustReference/target/release
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "RustReference", "target", "release"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "RustReference", "target", "release"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "Rerun.Net.IntegrationTests", "RustReference", "target", "release"),
        };
        var exe = OperatingSystem.IsWindows() ? "rrd-reference-gen.exe" : "rrd-reference-gen";
        foreach (var dir in candidates)
        {
            var path = Path.GetFullPath(Path.Combine(dir, exe));
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private (int ExitCode, string StdOut, string StdErr) RunRerunCompare(string file1, string file2)
        => RunRerun("rrd", "compare", "--unordered", file1, file2);

    private (int ExitCode, string StdOut, string StdErr) RunRerun(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = RerunCli!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi)!;
        // Read stdout/stderr asynchronously to avoid deadlock
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        if (!proc.WaitForExit(30_000))
        {
            proc.Kill();
            return (-1, "", "Process timed out");
        }
        return (proc.ExitCode, stdoutTask.Result, stderrTask.Result);
    }

    private static string? FindRerunCli()
    {
        var fromSubmodule = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "extern", "rerun", "target", "release",
                OperatingSystem.IsWindows() ? "rerun.exe" : "rerun"));
        if (File.Exists(fromSubmodule)) return fromSubmodule;

        try
        {
            var psi = new ProcessStartInfo("rerun", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            if (proc?.ExitCode == 0) return "rerun";
        }
        catch { }

        return null;
    }
}
