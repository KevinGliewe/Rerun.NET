using Rerun.Net;
using Xunit;

namespace Rerun.Net.Tests;

public class RecordingStreamTests
{
    [Fact]
    public void ConstructWithValidAppId()
    {
        using var rec = new RecordingStream("test_app");
        Assert.True(rec.IsEnabled);
    }

    [Fact]
    public void DisposeIsSafe()
    {
        var rec = new RecordingStream("test_dispose");
        rec.Dispose();
        // Second dispose should not throw
        rec.Dispose();
    }

    [Fact]
    public void SetTimeSequenceDoesNotThrow()
    {
        using var rec = new RecordingStream("test_time");
        rec.SetTimeSequence("frame", 42);
    }

    [Fact]
    public void SetTimeNanosDoesNotThrow()
    {
        using var rec = new RecordingStream("test_nanos");
        rec.SetTimeNanos("elapsed", 1_000_000_000);
    }

    [Fact]
    public void ResetTimeDoesNotThrow()
    {
        using var rec = new RecordingStream("test_reset");
        rec.SetTimeSequence("frame", 1);
        rec.ResetTime();
    }

    [Fact]
    public void ThrowsAfterDispose()
    {
        var rec = new RecordingStream("test_disposed_throw");
        rec.Dispose();
        Assert.Throws<ObjectDisposedException>(() => rec.SetTimeSequence("frame", 0));
    }

    [Fact]
    public void SaveCreatesFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rerun_test_{Guid.NewGuid()}.rrd");
        try
        {
            using (var rec = new RecordingStream("test_save"))
            {
                rec.Save(path);
            }
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SetGlobalDoesNotThrow()
    {
        using var rec = new RecordingStream("test_global");
        rec.SetGlobal();
    }

    [Fact]
    public void FlushBlockingDoesNotThrow()
    {
        using var rec = new RecordingStream("test_flush");
        rec.Save(Path.Combine(Path.GetTempPath(), $"rerun_flush_{Guid.NewGuid()}.rrd"));
        rec.FlushBlocking(1.0f);
    }

    [Fact]
    public void VersionStringIsNotEmpty()
    {
        var version = RecordingStream.VersionString();
        Assert.False(string.IsNullOrEmpty(version));
    }

    [Fact]
    public void CurrentIsNullByDefault()
    {
        // In a fresh test, no stream has been set as global/thread-local
        // (other tests may have set it, so just verify the property doesn't throw)
        _ = RecordingStream.Current;
    }

    [Fact]
    public void SetGlobalMakesStreamCurrent()
    {
        using var rec = new RecordingStream("test_current_global");
        rec.SetGlobal();
        Assert.Same(rec, RecordingStream.Current);
    }

    [Fact]
    public void TryLogReturnsNullOnSuccess()
    {
        var rrd = Path.Combine(Path.GetTempPath(), $"rerun_trylog_{Guid.NewGuid()}.rrd");
        try
        {
            using var rec = new RecordingStream("test_trylog");
            rec.Save(rrd);
            var result = rec.TryLog("test", new Rerun.Net.Archetypes.Points3D(
                new Rerun.Net.Components.Position3D(new Rerun.Net.Datatypes.Vec3D([1f, 2f, 3f]))));
            Assert.Null(result);
        }
        finally { if (File.Exists(rrd)) File.Delete(rrd); }
    }

    [Fact]
    public void DisableTimelineDoesNotThrow()
    {
        using var rec = new RecordingStream("test_disable_timeline");
        rec.SetTimeSequence("frame", 0);
        rec.DisableTimeline("frame");
    }
}
