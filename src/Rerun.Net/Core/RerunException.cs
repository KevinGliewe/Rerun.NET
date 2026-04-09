using System.Runtime.InteropServices;
using Rerun.Net.Native;

namespace Rerun.Net;

public enum ErrorCode : uint
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

public class RerunException : Exception
{
    public ErrorCode Code { get; }

    public RerunException(ErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    internal static unsafe void ThrowIfError(in RrError error)
    {
        if (error.Code == RrErrorCode.Ok)
            return;

        fixed (byte* desc = error.Description)
        {
            var message = Marshal.PtrToStringUTF8((IntPtr)desc) ?? "Unknown error";
            throw new RerunException((ErrorCode)error.Code, message);
        }
    }
}
