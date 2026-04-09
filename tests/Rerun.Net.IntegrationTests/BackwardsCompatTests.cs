using System.Diagnostics;
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;
using Xunit;
using Xunit.Abstractions;

namespace Rerun.Net.IntegrationTests;

/// <summary>
/// Backwards compatibility tests. Compares current C# SDK output against
/// checked-in reference .rrd files to detect regressions.
/// </summary>
public class BackwardsCompatTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tmpDir;
    private static readonly string? RerunCli = FindRerunCli();
    private static readonly string AssetsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "assets", "rrd"));

    public BackwardsCompatTests(ITestOutputHelper output)
    {
        _output = output;
        _tmpDir = Path.Combine(Path.GetTempPath(), $"rerun_compat_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmpDir, true); } catch { }
    }

    [SkippableFact]
    public void Points3DSimple_MatchesReference()
    {
        var csharp = LogToRrd("points3d_simple", "rerun_example_points3d", rec =>
            rec.Log("points", new Points3D(
                new Position3D(new Vec3D([0f, 0f, 0f])),
                new Position3D(new Vec3D([1f, 1f, 1f])))));
        AssertMatchesReference(csharp, "points3d_simple.rrd");
    }

    [SkippableFact]
    public void Arrows3DSimple_MatchesReference()
    {
        var csharp = LogToRrd("arrows3d_simple", "rerun_example_arrow3d", rec =>
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
                    new Color(new Rgba32(0x0000FFFFu)))));
        AssertMatchesReference(csharp, "arrows3d_simple.rrd");
    }

    [SkippableFact]
    public void Boxes2DSimple_MatchesReference()
    {
        var csharp = LogToRrd("boxes2d_simple", "rerun_example_box2d", rec =>
            rec.Log("simple", new Boxes2D(new HalfSize2D(new Vec2D([1f, 1f])))
                .WithCenters(new Components.Position2D(new Vec2D([0f, 0f])))));
        AssertMatchesReference(csharp, "boxes2d_simple.rrd");
    }

    [SkippableFact]
    public void LineStrips3DSimple_MatchesReference()
    {
        var csharp = LogToRrd("line_strips3d_simple", "rerun_example_line_strip3d", rec =>
            rec.Log("strip", new LineStrips3D(new LineStrip3D([
                new Vec3D([0f, 0f, 0f]), new Vec3D([0f, 0f, 1f]),
                new Vec3D([1f, 0f, 0f]), new Vec3D([1f, 0f, 1f]),
                new Vec3D([1f, 1f, 0f]), new Vec3D([1f, 1f, 1f]),
                new Vec3D([0f, 1f, 0f]), new Vec3D([0f, 1f, 1f]),
            ]))));
        AssertMatchesReference(csharp, "line_strips3d_simple.rrd");
    }

    private string LogToRrd(string name, string appId, Action<RecordingStream> log)
    {
        Skip.If(RerunCli == null, "rerun CLI not found");
        var rrdPath = Path.Combine(_tmpDir, $"{name}.rrd");
        using (var rec = new RecordingStream(appId))
        {
            rec.Save(rrdPath);
            log(rec);
        }
        return rrdPath;
    }

    private void AssertMatchesReference(string csharpRrd, string referenceFile)
    {
        var refPath = Path.Combine(AssetsDir, referenceFile);
        Skip.IfNot(File.Exists(refPath), $"Reference file not found: {refPath}");

        var psi = new ProcessStartInfo
        {
            FileName = RerunCli!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("rrd");
        psi.ArgumentList.Add("compare");
        psi.ArgumentList.Add("--unordered");
        psi.ArgumentList.Add(csharpRrd);
        psi.ArgumentList.Add(refPath);

        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        proc.WaitForExit(30_000);

        _output.WriteLine($"Backwards compat ({referenceFile}): exit {proc.ExitCode}");
        if (proc.ExitCode != 0)
            _output.WriteLine(stderrTask.Result);

        Assert.True(proc.ExitCode == 0,
            $"Backwards compat mismatch for {referenceFile}: {stderrTask.Result}");
    }

    private static string? FindRerunCli()
    {
        var path = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "extern", "rerun", "target", "release",
                OperatingSystem.IsWindows() ? "rerun.exe" : "rerun"));
        return File.Exists(path) ? path : null;
    }
}
