using System.Runtime.InteropServices;

namespace Rerun.Net.Native;

/// <summary>Borrowed UTF-8 string view. Must be pinned during native calls.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct RrString
{
    public byte* Utf8;
    public uint LengthInBytes;
}

/// <summary>Borrowed byte buffer.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct RrBytes
{
    public byte* Bytes;
    public uint Length;
}

internal enum RrStoreKind : uint
{
    Recording = 1,
    Blueprint = 2,
}

[StructLayout(LayoutKind.Sequential)]
internal struct RrStoreInfo
{
    public RrString ApplicationId;
    public RrString RecordingId;
    public RrStoreKind StoreKind;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RrComponentDescriptor
{
    public RrString Archetype;
    public RrString Component;
    public RrString ComponentType;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RrComponentType
{
    public RrComponentDescriptor Descriptor;
    public ArrowSchema Schema;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RrComponentBatch
{
    public uint ComponentType;
    public ArrowArray Array;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct RrDataRow
{
    public RrString EntityPath;
    public uint NumComponentBatches;
    public RrComponentBatch* ComponentBatches;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RrComponentColumn
{
    public uint ComponentType;
    public ArrowArray Array;
}

internal enum RrSortingStatus : uint
{
    Unknown = 0,
    Sorted = 1,
    Unsorted = 2,
}

internal enum RrTimeType : uint
{
    Sequence = 1,
    Duration = 2,
    Timestamp = 3,
}

[StructLayout(LayoutKind.Sequential)]
internal struct RrTimeline
{
    public RrString Name;
    public RrTimeType Type;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RrTimeColumn
{
    public RrTimeline Timeline;
    public ArrowArray Array;
    public RrSortingStatus SortingStatus;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RrSpawnOptions
{
    public ushort Port;
    public RrString MemoryLimit;
    public RrString ServerMemoryLimit;
    public byte HideWelcomeScreen;
    public byte DetachProcess;
    public RrString ExecutableName;
    public RrString ExecutablePath;
}

internal enum RrLogSinkKind : byte
{
    Grpc = 0,
    File = 1,
}

[StructLayout(LayoutKind.Sequential)]
internal struct RrGrpcSink
{
    public RrString Url;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RrFileSink
{
    public RrString Path;
}

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct RrLogSink
{
    [FieldOffset(0)] public RrLogSinkKind Kind;
    // Union starts after kind byte + padding. rr_string has pointer alignment (8 bytes on x64)
    // so the union offset is likely 8 (aligned to pointer size).
    // Actually, in C the anonymous union follows the uint8_t kind with padding.
    // On x64: kind at 0, padding 1-7, union at 8.
    [FieldOffset(8)] public RrGrpcSink Grpc;
    [FieldOffset(8)] public RrFileSink File;
}

internal enum RrErrorCode : uint
{
    Ok = 0,
    OutOfMemory,
    NotImplemented,
    SdkVersionMismatch,

    UnexpectedNullArgument = 0x00000011,
    InvalidStringArgument,
    InvalidEnumValue,
    InvalidRecordingStreamHandle,
    InvalidSocketAddress,
    InvalidComponentTypeHandle,
    InvalidTimeArgument,
    InvalidTensorDimension,
    InvalidComponent,
    InvalidServerUrl = 0x0000001a,
    FileRead,
    InvalidMemoryLimit,

    RecordingStreamRuntimeFailure = 0x00000101,
    RecordingStreamCreationFailure,
    RecordingStreamSaveFailure,
    RecordingStreamStdoutFailure,
    RecordingStreamSpawnFailure,
    RecordingStreamChunkValidationFailure,
    RecordingStreamServeGrpcFailure,
    RecordingStreamFlushTimeout,
    RecordingStreamFlushFailure,

    ArrowFfiSchemaImportError = 0x00001001,
    ArrowFfiArrayImportError,

    VideoLoadError = 0x00010001,

    FileOpenFailure = 0x00100001,

    Unknown = 0x10000001,
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct RrError
{
    public RrErrorCode Code;
    public fixed byte Description[2048];
}

// Arrow C Data Interface structs
// See: https://arrow.apache.org/docs/format/CDataInterface.html

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ArrowSchema
{
    public byte* Format;
    public byte* Name;
    public byte* Metadata;
    public long Flags;
    public long NChildren;
    public ArrowSchema** Children;
    public ArrowSchema* Dictionary;
    public IntPtr ReleaseCallback;
    public void* PrivateData;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ArrowArray
{
    public long Length;
    public long NullCount;
    public long Offset;
    public long NBuffers;
    public long NChildren;
    public void** Buffers;
    public ArrowArray** Children;
    public ArrowArray* Dictionary;
    public IntPtr ReleaseCallback;
    public void* PrivateData;
}
