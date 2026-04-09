using System.Runtime.InteropServices;

namespace Rerun.Net.Native;

internal static partial class NativeMethods
{
    private const string LibraryName = "rerun_c";

    // --- Version ---

    [LibraryImport(LibraryName, EntryPoint = "rr_version_string")]
    internal static unsafe partial byte* RrVersionString();

    // --- Spawn ---

    [LibraryImport(LibraryName, EntryPoint = "rr_spawn")]
    internal static partial void RrSpawn(
        in RrSpawnOptions spawnOpts,
        ref RrError error);

    // --- Component type registration ---

    [LibraryImport(LibraryName, EntryPoint = "rr_register_component_type")]
    internal static partial uint RrRegisterComponentType(
        RrComponentType componentType,
        ref RrError error);

    // --- Recording stream lifecycle ---

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_new")]
    internal static partial uint RrRecordingStreamNew(
        in RrStoreInfo storeInfo,
        [MarshalAs(UnmanagedType.U1)] bool defaultEnabled,
        ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_free")]
    internal static partial void RrRecordingStreamFree(uint stream);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_set_global")]
    internal static partial void RrRecordingStreamSetGlobal(uint stream, RrStoreKind storeKind);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_set_thread_local")]
    internal static partial void RrRecordingStreamSetThreadLocal(uint stream, RrStoreKind storeKind);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_is_enabled")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool RrRecordingStreamIsEnabled(uint stream, ref RrError error);

    // --- Sinks ---

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_connect_grpc")]
    internal static partial void RrRecordingStreamConnectGrpc(
        uint stream, RrString url, ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_spawn")]
    internal static partial void RrRecordingStreamSpawn(
        uint stream, in RrSpawnOptions spawnOpts, ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_save")]
    internal static partial void RrRecordingStreamSave(
        uint stream, RrString path, ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_stdout")]
    internal static partial void RrRecordingStreamStdout(uint stream, ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_flush_blocking")]
    internal static partial void RrRecordingStreamFlushBlocking(
        uint stream, float timeoutSec, ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_serve_grpc")]
    internal static unsafe partial void RrRecordingStreamServeGrpc(
        uint stream, RrString bindIp, ushort port, RrString serverMemoryLimit,
        [MarshalAs(UnmanagedType.U1)] bool newestFirst,
        RrString* corsAllowOrigins, uint numCorsAllowOrigins,
        ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_set_sinks")]
    internal static unsafe partial void RrRecordingStreamSetSinks(
        uint stream, RrLogSink* sinks, uint numSinks, ref RrError error);

    // --- Timeline ---

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_set_time")]
    internal static partial void RrRecordingStreamSetTime(
        uint stream, RrString timelineName, RrTimeType timeType, long value, ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_disable_timeline")]
    internal static partial void RrRecordingStreamDisableTimeline(
        uint stream, RrString timelineName, ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_reset_time")]
    internal static partial void RrRecordingStreamResetTime(uint stream);

    // --- Logging ---

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_log")]
    internal static partial void RrRecordingStreamLog(
        uint stream, RrDataRow dataRow,
        [MarshalAs(UnmanagedType.U1)] bool injectTime,
        ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_log_file_from_path")]
    internal static partial void RrRecordingStreamLogFileFromPath(
        uint stream, RrString path, RrString entityPathPrefix,
        [MarshalAs(UnmanagedType.U1)] bool static_,
        ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_log_file_from_contents")]
    internal static partial void RrRecordingStreamLogFileFromContents(
        uint stream, RrString path, RrBytes contents, RrString entityPathPrefix,
        [MarshalAs(UnmanagedType.U1)] bool static_,
        ref RrError error);

    [LibraryImport(LibraryName, EntryPoint = "rr_recording_stream_send_columns")]
    internal static unsafe partial void RrRecordingStreamSendColumns(
        uint stream, RrString entityPath,
        RrTimeColumn* timeColumns, uint numTimeColumns,
        RrComponentColumn* componentColumns, uint numComponentColumns,
        ref RrError error);

    // --- Utilities ---

    [LibraryImport(LibraryName, EntryPoint = "_rr_escape_entity_path_part")]
    internal static unsafe partial byte* RrEscapeEntityPathPart(RrString part);

    [LibraryImport(LibraryName, EntryPoint = "_rr_free_string")]
    internal static unsafe partial void RrFreeString(byte* str);
}
