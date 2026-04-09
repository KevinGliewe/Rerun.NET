using Rerun.Net;
using Rerun.Net.Native;
using Xunit;

namespace Rerun.Net.Tests;

/// <summary>
/// Tests that validate RecordingStream behavior using a mock INativeApi,
/// without requiring the native rerun_c binary.
/// </summary>
public class MockNativeApiTests
{
    private class MockNativeApi : INativeApi
    {
        public List<string> Calls { get; } = new();
        public uint NextHandle { get; set; } = 42;

        public uint RecordingStreamNew(string applicationId, StoreKind storeKind, bool defaultEnabled)
        {
            Calls.Add($"New({applicationId}, {storeKind})");
            return NextHandle;
        }

        public void RecordingStreamFree(uint handle) => Calls.Add($"Free({handle})");
        public bool RecordingStreamIsEnabled(uint handle) { Calls.Add($"IsEnabled({handle})"); return true; }
        public void RecordingStreamSetGlobal(uint handle, StoreKind storeKind) => Calls.Add($"SetGlobal({handle})");
        public void RecordingStreamSetThreadLocal(uint handle, StoreKind storeKind) => Calls.Add($"SetThreadLocal({handle})");
        public void RecordingStreamLog(uint handle, string entityPath, IReadOnlyList<ComponentBatch> batches, bool injectTime)
            => Calls.Add($"Log({handle}, {entityPath}, {batches.Count} batches, injectTime={injectTime})");
        public void RecordingStreamSave(uint handle, string path) => Calls.Add($"Save({handle}, {path})");
        public void RecordingStreamConnectGrpc(uint handle, string? url) => Calls.Add($"ConnectGrpc({handle}, {url})");
        public void RecordingStreamFlushBlocking(uint handle, float timeoutSec) => Calls.Add($"Flush({handle})");
        public void RecordingStreamSetTime(uint handle, string timelineName, TimeType type, long value)
            => Calls.Add($"SetTime({handle}, {timelineName}, {type}, {value})");
        public void RecordingStreamResetTime(uint handle) => Calls.Add($"ResetTime({handle})");
        public void RecordingStreamDisableTimeline(uint handle, string timelineName) => Calls.Add($"DisableTimeline({handle}, {timelineName})");
        public string VersionString() => "mock-1.0.0";
    }

    [Fact]
    public void Constructor_CallsNew()
    {
        var mock = new MockNativeApi();
        using var rec = new RecordingStream("test_app", StoreKind.Recording, mock);
        Assert.Contains("New(test_app, Recording)", mock.Calls);
    }

    [Fact]
    public void Dispose_CallsFree()
    {
        var mock = new MockNativeApi();
        var rec = new RecordingStream("test_app", StoreKind.Recording, mock);
        rec.Dispose();
        Assert.Contains("Free(42)", mock.Calls);
    }

    [Fact]
    public void Save_CallsSave()
    {
        var mock = new MockNativeApi();
        using var rec = new RecordingStream("test_app", StoreKind.Recording, mock);
        rec.Save("/tmp/test.rrd");
        Assert.Contains("Save(42, /tmp/test.rrd)", mock.Calls);
    }

    [Fact]
    public void SetTimeSequence_CallsSetTime()
    {
        var mock = new MockNativeApi();
        using var rec = new RecordingStream("test_app", StoreKind.Recording, mock);
        rec.SetTimeSequence("frame", 10);
        Assert.Contains("SetTime(42, frame, Sequence, 10)", mock.Calls);
    }

    [Fact]
    public void ConnectGrpc_CallsConnect()
    {
        var mock = new MockNativeApi();
        using var rec = new RecordingStream("test_app", StoreKind.Recording, mock);
        rec.ConnectGrpc("rerun+http://localhost:9876/proxy");
        Assert.Contains("ConnectGrpc(42, rerun+http://localhost:9876/proxy)", mock.Calls);
    }

    [Fact]
    public void DoubleDispose_OnlyCallsFreeOnce()
    {
        var mock = new MockNativeApi();
        var rec = new RecordingStream("test_app", StoreKind.Recording, mock);
        rec.Dispose();
        rec.Dispose();
        Assert.Single(mock.Calls.Where(c => c.StartsWith("Free")));
    }

    [Fact]
    public void ThrowsAfterDispose()
    {
        var mock = new MockNativeApi();
        var rec = new RecordingStream("test_app", StoreKind.Recording, mock);
        rec.Dispose();
        Assert.Throws<ObjectDisposedException>(() => rec.SetTimeSequence("frame", 0));
    }
}
