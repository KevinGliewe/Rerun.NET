using System.Runtime.InteropServices;
using System.Text;
using Rerun.Net.Native;

namespace Rerun.Net;

/// <summary>
/// A recording stream that logs data to Rerun.
/// </summary>
public sealed class RecordingStream : IDisposable
{
    private uint _handle;
    private bool _disposed;
    internal readonly INativeApi NativeApi;

    public const uint CurrentRecording = 0xFFFFFFFF;
    public const uint CurrentBlueprint = 0xFFFFFFFE;

    private static RecordingStream? _global;
    [ThreadStatic] private static RecordingStream? _threadLocal;
    private static Action<RerunException>? _errorHandler;

    /// <summary>
    /// Returns the current recording stream: thread-local first, then global.
    /// </summary>
    public static RecordingStream? Current => _threadLocal ?? _global;

    /// <summary>
    /// Sets a global error handler for non-throwing log methods (TryLog).
    /// If null, errors from TryLog are silently ignored.
    /// </summary>
    public static void SetErrorHandler(Action<RerunException>? handler) => _errorHandler = handler;

    static RecordingStream()
    {
        LibraryLoader.Initialize();
    }

    public RecordingStream(string applicationId, StoreKind storeKind = StoreKind.Recording)
        : this(applicationId, storeKind, DefaultNativeApi.Instance) { }

    /// <summary>Internal constructor for testing with a mock native API.</summary>
    internal RecordingStream(string applicationId, StoreKind storeKind, INativeApi nativeApi)
    {
        NativeApi = nativeApi;
        _handle = nativeApi.RecordingStreamNew(applicationId, storeKind, true);
    }

    public void Log(string entityPath, IAsComponents archetype, bool @static = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var batches = archetype.AsBatches();
        if (batches.Count == 0) return;

        unsafe
        {
            var entityPathBytes = Encoding.UTF8.GetBytes(entityPath);
            var nativeBatches = stackalloc RrComponentBatch[batches.Count];

            fixed (byte* pathPtr = entityPathBytes)
            {
                for (var i = 0; i < batches.Count; i++)
                {
                    var batch = batches[i];
                    var handle = ComponentTypeRegistry.GetOrRegister(
                        batch.Descriptor, batch.Array.Data.DataType);
                    nativeBatches[i].ComponentType = handle;
                    ArrowFFI.ExportArray(batch.Array, &nativeBatches[i].Array);
                }

                var dataRow = new RrDataRow
                {
                    EntityPath = new RrString { Utf8 = pathPtr, LengthInBytes = (uint)entityPathBytes.Length },
                    NumComponentBatches = (uint)batches.Count,
                    ComponentBatches = nativeBatches,
                };

                var error = new RrError();
                NativeMethods.RrRecordingStreamLog(_handle, dataRow, !@static, ref error);
                RerunException.ThrowIfError(error);
            }
        }
    }

    /// <summary>
    /// Non-throwing variant of Log. Returns the error code, or null on success.
    /// Invokes the error handler callback if one is set.
    /// </summary>
    public ErrorCode? TryLog(string entityPath, IAsComponents archetype, bool @static = false)
    {
        try
        {
            Log(entityPath, archetype, @static);
            return null;
        }
        catch (RerunException ex)
        {
            _errorHandler?.Invoke(ex);
            return ex.Code;
        }
    }

    public void ConnectGrpc(string? url = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NativeApi.RecordingStreamConnectGrpc(_handle, url);
    }

    public void Save(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NativeApi.RecordingStreamSave(_handle, path);
    }

    public void Spawn(SpawnOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unsafe
        {
            if (options == null)
            {
                var defaultOpts = default(RrSpawnOptions);
                var error = new RrError();
                NativeMethods.RrRecordingStreamSpawn(_handle, in defaultOpts, ref error);
                RerunException.ThrowIfError(error);
            }
            else
            {
                var pinned = options.Pin();
                fixed (byte* memPtr = pinned.MemoryLimitBytes)
                fixed (byte* srvPtr = pinned.ServerMemoryLimitBytes)
                fixed (byte* namePtr = pinned.ExecutableNameBytes)
                fixed (byte* pathPtr = pinned.ExecutablePathBytes)
                {
                    var spawnOpts = pinned.ToNative(memPtr, srvPtr, namePtr, pathPtr);
                    var error = new RrError();
                    NativeMethods.RrRecordingStreamSpawn(_handle, in spawnOpts, ref error);
                    RerunException.ThrowIfError(error);
                }
            }
        }
    }

    public void ServeGrpc(string? bindIp = null, ushort port = 0, string? serverMemoryLimit = null, bool newestFirst = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unsafe
        {
            var bindIpBytes = bindIp != null ? Encoding.UTF8.GetBytes(bindIp) : [];
            var memLimitBytes = serverMemoryLimit != null ? Encoding.UTF8.GetBytes(serverMemoryLimit) : [];
            fixed (byte* bindIpPtr = bindIpBytes)
            fixed (byte* memLimitPtr = memLimitBytes)
            {
                var rrBindIp = bindIp != null
                    ? new RrString { Utf8 = bindIpPtr, LengthInBytes = (uint)bindIpBytes.Length }
                    : default;
                var rrMemLimit = serverMemoryLimit != null
                    ? new RrString { Utf8 = memLimitPtr, LengthInBytes = (uint)memLimitBytes.Length }
                    : default;
                var error = new RrError();
                NativeMethods.RrRecordingStreamServeGrpc(
                    _handle, rrBindIp, port, rrMemLimit, newestFirst, ref error);
                RerunException.ThrowIfError(error);
            }
        }
    }

    public static void SpawnViewer(SpawnOptions? options = null)
    {
        LibraryLoader.Initialize();
        unsafe
        {
            if (options == null)
            {
                var defaultOpts = default(RrSpawnOptions);
                var error = new RrError();
                NativeMethods.RrSpawn(in defaultOpts, ref error);
                RerunException.ThrowIfError(error);
            }
            else
            {
                var pinned = options.Pin();
                fixed (byte* memPtr = pinned.MemoryLimitBytes)
                fixed (byte* srvPtr = pinned.ServerMemoryLimitBytes)
                fixed (byte* namePtr = pinned.ExecutableNameBytes)
                fixed (byte* pathPtr = pinned.ExecutablePathBytes)
                {
                    var spawnOpts = pinned.ToNative(memPtr, srvPtr, namePtr, pathPtr);
                    var error = new RrError();
                    NativeMethods.RrSpawn(in spawnOpts, ref error);
                    RerunException.ThrowIfError(error);
                }
            }
        }
    }

    public void SetTime(string timelineName, TimeType type, long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NativeApi.RecordingStreamSetTime(_handle, timelineName, type, value);
    }

    public void SetTimeSequence(string timelineName, long value)
        => SetTime(timelineName, TimeType.Sequence, value);

    public void SetTimeNanos(string timelineName, long nanos)
        => SetTime(timelineName, TimeType.Duration, nanos);

    public void ResetTime()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NativeApi.RecordingStreamResetTime(_handle);
    }

    public void FlushBlocking(float timeoutSec = -1.0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NativeApi.RecordingStreamFlushBlocking(_handle, timeoutSec);
    }

    public bool IsEnabled
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return NativeApi.RecordingStreamIsEnabled(_handle);
        }
    }

    public void SetGlobal(StoreKind storeKind = StoreKind.Recording)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NativeApi.RecordingStreamSetGlobal(_handle, storeKind);
        if (storeKind == StoreKind.Recording) _global = this;
    }

    public void SetThreadLocal(StoreKind storeKind = StoreKind.Recording)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NativeApi.RecordingStreamSetThreadLocal(_handle, storeKind);
        if (storeKind == StoreKind.Recording) _threadLocal = this;
    }

    public void SendColumns(
        string entityPath,
        TimeColumn[] timeColumns,
        ComponentColumn[] componentColumns)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (componentColumns.Length == 0) return;

        unsafe
        {
            var entityPathBytes = Encoding.UTF8.GetBytes(entityPath);
            var nativeTimeCols = stackalloc RrTimeColumn[timeColumns.Length];
            var nativeCompCols = stackalloc RrComponentColumn[componentColumns.Length];

            // Pre-encode all timeline name strings
            var timelineNameBytes = new byte[timeColumns.Length][];
            for (var i = 0; i < timeColumns.Length; i++)
                timelineNameBytes[i] = Encoding.UTF8.GetBytes(timeColumns[i].TimelineName);

            fixed (byte* pathPtr = entityPathBytes)
            {
                // Build time columns — export long[] values as Arrow Int64 arrays
                for (var i = 0; i < timeColumns.Length; i++)
                {
                    var tc = timeColumns[i];
                    var valuesArray = new Apache.Arrow.Int64Array.Builder()
                        .AppendRange(tc.Values)
                        .Build();

                    fixed (byte* namePtr = timelineNameBytes[i])
                    {
                        nativeTimeCols[i].Timeline = new RrTimeline
                        {
                            Name = new RrString { Utf8 = namePtr, LengthInBytes = (uint)timelineNameBytes[i].Length },
                            Type = (RrTimeType)tc.Type,
                        };
                        nativeTimeCols[i].SortingStatus = (RrSortingStatus)tc.Sorting;
                        ArrowFFI.ExportArray(valuesArray, &nativeTimeCols[i].Array);
                    }
                }

                // Build component columns
                for (var i = 0; i < componentColumns.Length; i++)
                {
                    var cc = componentColumns[i];
                    var handle = ComponentTypeRegistry.GetOrRegister(
                        cc.Descriptor, cc.Array.Data.DataType);
                    nativeCompCols[i].ComponentType = handle;
                    ArrowFFI.ExportArray(cc.Array, &nativeCompCols[i].Array);
                }

                var error = new RrError();
                NativeMethods.RrRecordingStreamSendColumns(
                    _handle,
                    new RrString { Utf8 = pathPtr, LengthInBytes = (uint)entityPathBytes.Length },
                    nativeTimeCols, (uint)timeColumns.Length,
                    nativeCompCols, (uint)componentColumns.Length,
                    ref error);
                RerunException.ThrowIfError(error);
            }
        }
    }

    public void LogFileFromPath(string path, string? entityPathPrefix = null, bool @static = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unsafe
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var prefixBytes = entityPathPrefix != null ? Encoding.UTF8.GetBytes(entityPathPrefix) : [];
            fixed (byte* pathPtr = pathBytes)
            fixed (byte* prefixPtr = prefixBytes)
            {
                var rrPath = new RrString { Utf8 = pathPtr, LengthInBytes = (uint)pathBytes.Length };
                var rrPrefix = entityPathPrefix != null
                    ? new RrString { Utf8 = prefixPtr, LengthInBytes = (uint)prefixBytes.Length }
                    : default;
                var error = new RrError();
                NativeMethods.RrRecordingStreamLogFileFromPath(
                    _handle, rrPath, rrPrefix, @static, ref error);
                RerunException.ThrowIfError(error);
            }
        }
    }

    public void LogFileFromContents(string path, byte[] contents, string? entityPathPrefix = null, bool @static = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unsafe
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var prefixBytes = entityPathPrefix != null ? Encoding.UTF8.GetBytes(entityPathPrefix) : [];
            fixed (byte* pathPtr = pathBytes)
            fixed (byte* prefixPtr = prefixBytes)
            fixed (byte* contentsPtr = contents)
            {
                var rrPath = new RrString { Utf8 = pathPtr, LengthInBytes = (uint)pathBytes.Length };
                var rrPrefix = entityPathPrefix != null
                    ? new RrString { Utf8 = prefixPtr, LengthInBytes = (uint)prefixBytes.Length }
                    : default;
                var rrContents = new RrBytes { Bytes = contentsPtr, Length = (uint)contents.Length };
                var error = new RrError();
                NativeMethods.RrRecordingStreamLogFileFromContents(
                    _handle, rrPath, rrContents, rrPrefix, @static, ref error);
                RerunException.ThrowIfError(error);
            }
        }
    }

    public void SetSinks(params LogSink[] sinks)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (sinks.Length == 0) return;
        unsafe
        {
            // Encode all sink strings up front
            var sinkBytes = new byte[sinks.Length][];
            for (var i = 0; i < sinks.Length; i++)
                sinkBytes[i] = Encoding.UTF8.GetBytes(sinks[i].Value);

            var nativeSinks = stackalloc RrLogSink[sinks.Length];
            // Pin all strings and build native structs
            // Use a GCHandle array to pin them
            var handles = new System.Runtime.InteropServices.GCHandle[sinks.Length];
            try
            {
                for (var i = 0; i < sinks.Length; i++)
                {
                    handles[i] = System.Runtime.InteropServices.GCHandle.Alloc(sinkBytes[i], GCHandleType.Pinned);
                    var ptr = (byte*)handles[i].AddrOfPinnedObject();
                    var rrStr = new RrString { Utf8 = ptr, LengthInBytes = (uint)sinkBytes[i].Length };

                    nativeSinks[i] = new RrLogSink { Kind = (RrLogSinkKind)sinks[i].Kind };
                    if (sinks[i].Kind == LogSinkKind.Grpc)
                        nativeSinks[i].Grpc = new RrGrpcSink { Url = rrStr };
                    else
                        nativeSinks[i].File = new RrFileSink { Path = rrStr };
                }

                var error = new RrError();
                NativeMethods.RrRecordingStreamSetSinks(_handle, nativeSinks, (uint)sinks.Length, ref error);
                RerunException.ThrowIfError(error);
            }
            finally
            {
                foreach (var h in handles)
                    if (h.IsAllocated) h.Free();
            }
        }
    }

    public void DisableTimeline(string timelineName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unsafe
        {
            var nameBytes = Encoding.UTF8.GetBytes(timelineName);
            fixed (byte* namePtr = nameBytes)
            {
                var rrName = new RrString { Utf8 = namePtr, LengthInBytes = (uint)nameBytes.Length };
                var error = new RrError();
                NativeMethods.RrRecordingStreamDisableTimeline(_handle, rrName, ref error);
                RerunException.ThrowIfError(error);
            }
        }
    }

    public static string VersionString()
    {
        LibraryLoader.Initialize();
        unsafe
        {
            var ptr = NativeMethods.RrVersionString();
            return Marshal.PtrToStringUTF8((IntPtr)ptr) ?? "";
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            NativeApi.RecordingStreamFree(_handle);
            _disposed = true;
        }
    }
}

public enum StoreKind : uint
{
    Recording = 1,
    Blueprint = 2,
}

public class SpawnOptions
{
    public ushort Port { get; set; }
    public string? MemoryLimit { get; set; }
    public string? ServerMemoryLimit { get; set; }
    public bool HideWelcomeScreen { get; set; }
    public bool DetachProcess { get; set; }
    public string? ExecutableName { get; set; }
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Marshals to native struct. Must be called inside a fixed block that keeps
    /// the returned PinnedSpawnOptions alive for the duration of the native call.
    /// </summary>
    internal PinnedSpawnOptions Pin() => new(this);
}

public enum LogSinkKind
{
    Grpc = 0,
    File = 1,
}

/// <summary>
/// A sink for log messages — either a gRPC URL or a file path.
/// </summary>
public readonly record struct LogSink(LogSinkKind Kind, string Value)
{
    public static LogSink Grpc(string url = "rerun+http://127.0.0.1:9876/proxy") => new(LogSinkKind.Grpc, url);
    public static LogSink File(string path) => new(LogSinkKind.File, path);
}

/// <summary>
/// Holds pinned byte arrays for SpawnOptions string fields.
/// Must be kept alive (not collected) while the native call is in progress.
/// </summary>
internal ref struct PinnedSpawnOptions
{
    private readonly byte[] _memoryLimit;
    private readonly byte[] _serverMemoryLimit;
    private readonly byte[] _executableName;
    private readonly byte[] _executablePath;

    public PinnedSpawnOptions(SpawnOptions opts)
    {
        _memoryLimit = opts.MemoryLimit != null ? Encoding.UTF8.GetBytes(opts.MemoryLimit) : [];
        _serverMemoryLimit = opts.ServerMemoryLimit != null ? Encoding.UTF8.GetBytes(opts.ServerMemoryLimit) : [];
        _executableName = opts.ExecutableName != null ? Encoding.UTF8.GetBytes(opts.ExecutableName) : [];
        _executablePath = opts.ExecutablePath != null ? Encoding.UTF8.GetBytes(opts.ExecutablePath) : [];

        Port = opts.Port;
        HideWelcomeScreen = opts.HideWelcomeScreen;
        DetachProcess = opts.DetachProcess;
    }

    public ushort Port { get; }
    public bool HideWelcomeScreen { get; }
    public bool DetachProcess { get; }

    public unsafe RrSpawnOptions ToNative(
        byte* memLimitPtr, byte* serverMemLimitPtr,
        byte* execNamePtr, byte* execPathPtr)
    {
        return new RrSpawnOptions
        {
            Port = Port,
            MemoryLimit = MakeRrString(memLimitPtr, _memoryLimit.Length),
            ServerMemoryLimit = MakeRrString(serverMemLimitPtr, _serverMemoryLimit.Length),
            HideWelcomeScreen = HideWelcomeScreen ? (byte)1 : (byte)0,
            DetachProcess = DetachProcess ? (byte)1 : (byte)0,
            ExecutableName = MakeRrString(execNamePtr, _executableName.Length),
            ExecutablePath = MakeRrString(execPathPtr, _executablePath.Length),
        };
    }

    public byte[] MemoryLimitBytes => _memoryLimit;
    public byte[] ServerMemoryLimitBytes => _serverMemoryLimit;
    public byte[] ExecutableNameBytes => _executableName;
    public byte[] ExecutablePathBytes => _executablePath;

    private static unsafe RrString MakeRrString(byte* ptr, int length)
    {
        if (length == 0) return default;
        return new RrString { Utf8 = ptr, LengthInBytes = (uint)length };
    }
}
