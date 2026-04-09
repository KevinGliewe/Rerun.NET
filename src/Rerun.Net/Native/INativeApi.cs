namespace Rerun.Net.Native;

/// <summary>
/// Abstraction over native P/Invoke calls for testability.
/// Tests can mock this to validate managed wrapper logic without the native binary.
/// </summary>
internal interface INativeApi
{
    uint RecordingStreamNew(string applicationId, StoreKind storeKind, bool defaultEnabled);
    void RecordingStreamFree(uint handle);
    bool RecordingStreamIsEnabled(uint handle);
    void RecordingStreamSetGlobal(uint handle, StoreKind storeKind);
    void RecordingStreamSetThreadLocal(uint handle, StoreKind storeKind);
    void RecordingStreamLog(uint handle, string entityPath, IReadOnlyList<ComponentBatch> batches, bool injectTime);
    void RecordingStreamSave(uint handle, string path);
    void RecordingStreamConnectGrpc(uint handle, string? url);
    void RecordingStreamFlushBlocking(uint handle, float timeoutSec);
    void RecordingStreamSetTime(uint handle, string timelineName, TimeType type, long value);
    void RecordingStreamResetTime(uint handle);
    void RecordingStreamDisableTimeline(uint handle, string timelineName);
    string VersionString();
}

/// <summary>
/// Production implementation that delegates to P/Invoke NativeMethods.
/// </summary>
internal sealed class DefaultNativeApi : INativeApi
{
    public static readonly DefaultNativeApi Instance = new();

    public uint RecordingStreamNew(string applicationId, StoreKind storeKind, bool defaultEnabled)
    {
        unsafe
        {
            var appIdBytes = System.Text.Encoding.UTF8.GetBytes(applicationId);
            fixed (byte* appIdPtr = appIdBytes)
            {
                var storeInfo = new RrStoreInfo
                {
                    ApplicationId = new RrString { Utf8 = appIdPtr, LengthInBytes = (uint)appIdBytes.Length },
                    RecordingId = default,
                    StoreKind = (RrStoreKind)storeKind,
                };
                var error = new RrError();
                var handle = NativeMethods.RrRecordingStreamNew(in storeInfo, defaultEnabled, ref error);
                RerunException.ThrowIfError(error);
                return handle;
            }
        }
    }

    public void RecordingStreamFree(uint handle) => NativeMethods.RrRecordingStreamFree(handle);

    public bool RecordingStreamIsEnabled(uint handle)
    {
        var error = new RrError();
        var result = NativeMethods.RrRecordingStreamIsEnabled(handle, ref error);
        RerunException.ThrowIfError(error);
        return result;
    }

    public void RecordingStreamSetGlobal(uint handle, StoreKind storeKind)
        => NativeMethods.RrRecordingStreamSetGlobal(handle, (RrStoreKind)storeKind);

    public void RecordingStreamSetThreadLocal(uint handle, StoreKind storeKind)
        => NativeMethods.RrRecordingStreamSetThreadLocal(handle, (RrStoreKind)storeKind);

    public void RecordingStreamLog(uint handle, string entityPath, IReadOnlyList<ComponentBatch> batches, bool injectTime)
    {
        if (batches.Count == 0) return;
        unsafe
        {
            var entityPathBytes = System.Text.Encoding.UTF8.GetBytes(entityPath);
            var nativeBatches = stackalloc RrComponentBatch[batches.Count];
            fixed (byte* pathPtr = entityPathBytes)
            {
                for (var i = 0; i < batches.Count; i++)
                {
                    var batch = batches[i];
                    var h = ComponentTypeRegistry.GetOrRegister(batch.Descriptor, batch.Array.Data.DataType);
                    nativeBatches[i].ComponentType = h;
                    ArrowFFI.ExportArray(batch.Array, &nativeBatches[i].Array);
                }
                var dataRow = new RrDataRow
                {
                    EntityPath = new RrString { Utf8 = pathPtr, LengthInBytes = (uint)entityPathBytes.Length },
                    NumComponentBatches = (uint)batches.Count,
                    ComponentBatches = nativeBatches,
                };
                var error = new RrError();
                NativeMethods.RrRecordingStreamLog(handle, dataRow, injectTime, ref error);
                RerunException.ThrowIfError(error);
            }
        }
    }

    public void RecordingStreamSave(uint handle, string path)
    {
        unsafe
        {
            var pathBytes = System.Text.Encoding.UTF8.GetBytes(path);
            fixed (byte* pathPtr = pathBytes)
            {
                var rrPath = new RrString { Utf8 = pathPtr, LengthInBytes = (uint)pathBytes.Length };
                var error = new RrError();
                NativeMethods.RrRecordingStreamSave(handle, rrPath, ref error);
                RerunException.ThrowIfError(error);
            }
        }
    }

    public void RecordingStreamConnectGrpc(uint handle, string? url)
    {
        unsafe
        {
            var urlBytes = url != null ? System.Text.Encoding.UTF8.GetBytes(url) : [];
            fixed (byte* urlPtr = urlBytes)
            {
                var rrUrl = new RrString { Utf8 = urlPtr, LengthInBytes = (uint)urlBytes.Length };
                var error = new RrError();
                NativeMethods.RrRecordingStreamConnectGrpc(handle, rrUrl, ref error);
                RerunException.ThrowIfError(error);
            }
        }
    }

    public void RecordingStreamFlushBlocking(uint handle, float timeoutSec)
    {
        var error = new RrError();
        NativeMethods.RrRecordingStreamFlushBlocking(handle, timeoutSec, ref error);
        RerunException.ThrowIfError(error);
    }

    public void RecordingStreamSetTime(uint handle, string timelineName, TimeType type, long value)
    {
        unsafe
        {
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(timelineName);
            fixed (byte* namePtr = nameBytes)
            {
                var rrName = new RrString { Utf8 = namePtr, LengthInBytes = (uint)nameBytes.Length };
                var error = new RrError();
                NativeMethods.RrRecordingStreamSetTime(handle, rrName, (RrTimeType)type, value, ref error);
                RerunException.ThrowIfError(error);
            }
        }
    }

    public void RecordingStreamResetTime(uint handle) => NativeMethods.RrRecordingStreamResetTime(handle);

    public void RecordingStreamDisableTimeline(uint handle, string timelineName)
    {
        unsafe
        {
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(timelineName);
            fixed (byte* namePtr = nameBytes)
            {
                var rrName = new RrString { Utf8 = namePtr, LengthInBytes = (uint)nameBytes.Length };
                var error = new RrError();
                NativeMethods.RrRecordingStreamDisableTimeline(handle, rrName, ref error);
                RerunException.ThrowIfError(error);
            }
        }
    }

    public string VersionString()
    {
        unsafe
        {
            var ptr = NativeMethods.RrVersionString();
            return System.Runtime.InteropServices.Marshal.PtrToStringUTF8((IntPtr)ptr) ?? "";
        }
    }
}
