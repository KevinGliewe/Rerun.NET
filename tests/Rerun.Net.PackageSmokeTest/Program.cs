// Package smoke test for Rerun.Net.
//
// Consumes the published Rerun.Net NuGet package and exercises the full
// managed -> native code path on whatever RID this process happens to be
// running on. The goal is to prove, per platform, that:
//
//   1. The package restores cleanly from a PackageReference.
//   2. The runtimes/{rid}/native/{libname} binary shipped inside the .nupkg
//      is discovered and loaded by the custom DllImportResolver / .NET host.
//   3. A round-trip RecordingStream -> Save -> disposed flow actually
//      produces a non-empty .rrd file.
//
// Exits 0 on success, non-zero on failure. Any exception is printed to
// stderr so the CI log makes the failure obvious.

using System.Runtime.InteropServices;
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

Console.WriteLine("=== Rerun.Net package smoke test ===");
Console.WriteLine($"RuntimeIdentifier : {RuntimeInformation.RuntimeIdentifier}");
Console.WriteLine($"OSDescription     : {RuntimeInformation.OSDescription}");
Console.WriteLine($"OSArchitecture    : {RuntimeInformation.OSArchitecture}");
Console.WriteLine($"ProcessArch       : {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"FrameworkDesc     : {RuntimeInformation.FrameworkDescription}");

string? rrdPath = null;
try
{
    // Step 1: touch the native library via the version entry point. This is
    // the cheapest call that forces rerun_c to load; if the native binary
    // for this RID is missing or the wrong architecture, we fail here with
    // a DllNotFoundException or BadImageFormatException before touching
    // anything else.
    var version = RecordingStream.VersionString();
    Console.WriteLine($"rerun_c version   : {version}");
    if (string.IsNullOrWhiteSpace(version))
        throw new InvalidOperationException("rr_version_string returned an empty string.");

    // Step 2: exercise the full RecordingStream -> Save -> Log -> Dispose
    // path to prove Arrow FFI marshalling and sink wiring work in the
    // packaged build, not just for version strings.
    rrdPath = Path.Combine(
        Path.GetTempPath(),
        $"rerun_net_pkg_smoke_{Guid.NewGuid():N}.rrd");

    using (var rec = new RecordingStream("rerun_net_package_smoke_test"))
    {
        rec.Save(rrdPath);
        rec.SetTimeSequence("frame", 0);

        var points = new Points3D(
                new Position3D(new Vec3D([0f, 0f, 0f])),
                new Position3D(new Vec3D([1f, 1f, 1f])),
                new Position3D(new Vec3D([2f, 2f, 2f])))
            .WithColors(
                new Color(new Rgba32(0xFF0000FFu)),
                new Color(new Rgba32(0x00FF00FFu)),
                new Color(new Rgba32(0x0000FFFFu)))
            .WithRadii(new Radius(0.5f));

        rec.Log("smoke/points", points);
    }

    var info = new FileInfo(rrdPath);
    if (!info.Exists)
        throw new InvalidOperationException($"RRD file was not created at {rrdPath}.");
    if (info.Length == 0)
        throw new InvalidOperationException($"RRD file at {rrdPath} is empty.");

    Console.WriteLine($"Wrote {info.Length} bytes to {rrdPath}");
    Console.WriteLine("=== SMOKE TEST OK ===");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("=== SMOKE TEST FAILED ===");
    Console.Error.WriteLine(ex);
    return 1;
}
finally
{
    if (rrdPath is not null && File.Exists(rrdPath))
    {
        try { File.Delete(rrdPath); }
        catch { /* best-effort cleanup */ }
    }
}
