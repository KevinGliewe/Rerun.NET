using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;
using Xunit;

namespace Rerun.Net.Tests;

public class SmokeTests
{
    [Fact]
    public void LogPoints3DToRrdFile()
    {
        var rrdPath = Path.Combine(Path.GetTempPath(), $"rerun_net_smoke_{Guid.NewGuid()}.rrd");
        try
        {
            using (var rec = new RecordingStream("rerun_net_smoke_test"))
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
                    .WithRadii(
                        new Radius(0.5f));

                rec.Log("test/points", points);
            }

            // Verify file was created and has content
            Assert.True(File.Exists(rrdPath), $"RRD file not created at {rrdPath}");
            var fileSize = new FileInfo(rrdPath).Length;
            Assert.True(fileSize > 0, $"RRD file is empty ({fileSize} bytes)");
        }
        finally
        {
            if (File.Exists(rrdPath))
                File.Delete(rrdPath);
        }
    }
}
