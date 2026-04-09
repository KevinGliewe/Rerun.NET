using Apache.Arrow;
using Apache.Arrow.C;
using Apache.Arrow.Types;

namespace Rerun.Net.Native;

/// <summary>
/// Bridge between Apache.Arrow managed arrays and the Arrow C Data Interface structs
/// expected by rerun_c.
/// </summary>
internal static class ArrowFFI
{
    /// <summary>
    /// Exports an <see cref="IArrowArray"/> to a native <see cref="ArrowArray"/> struct
    /// via the Arrow C Data Interface.
    /// The caller must ensure the returned struct is consumed (release callback called)
    /// before the managed array is collected.
    /// </summary>
    public static unsafe void ExportArray(IArrowArray array, ArrowArray* cArray)
    {
        try
        {
            var cArrowArray = (CArrowArray*)cArray;
            CArrowArrayExporter.ExportArray(array, cArrowArray);
        }
        catch (NotSupportedException)
        {
            // CArrowArrayExporter fails on some array types (String, Binary, and sometimes
            // primitive arrays with certain buffer layouts). Fall back to manual export.
            ExportArrayManual(array, cArray);
        }
    }

    private static unsafe void ExportArrayManual(IArrowArray array, ArrowArray* cArray)
    {
        var data = array.Data;
        var nBuffers = data.Buffers.Length;
        var nChildren = data.Children?.Length ?? 0;

        var buffers = (void**)System.Runtime.InteropServices.NativeMemory.AllocZeroed(
            (nuint)nBuffers, (nuint)sizeof(void*));
        var handles = new System.Buffers.MemoryHandle[nBuffers];

        for (var i = 0; i < nBuffers; i++)
        {
            var buf = data.Buffers[i].Memory;
            if (buf.Length > 0)
            {
                handles[i] = buf.Pin();
                buffers[i] = handles[i].Pointer;
            }
        }

        // Export children recursively
        ArrowArray** children = null;
        if (nChildren > 0)
        {
            children = (ArrowArray**)System.Runtime.InteropServices.NativeMemory.AllocZeroed(
                (nuint)nChildren, (nuint)sizeof(ArrowArray*));
            for (var i = 0; i < nChildren; i++)
            {
                children[i] = (ArrowArray*)System.Runtime.InteropServices.NativeMemory.AllocZeroed(
                    1, (nuint)sizeof(ArrowArray));
                var childArray = data.Children[i].DataType.Name switch
                {
                    _ => Apache.Arrow.ArrowArrayFactory.BuildArray(data.Children[i])
                };
                ExportArray(childArray, children[i]);
            }
        }

        cArray->Length = data.Length;
        cArray->NullCount = data.NullCount;
        cArray->Offset = data.Offset;
        cArray->NBuffers = nBuffers;
        cArray->NChildren = nChildren;
        cArray->Buffers = buffers;
        cArray->Children = children;
        cArray->Dictionary = null;

        var ctx = new ManualExportContext { Array = array, Handles = handles, Buffers = buffers, Children = children, NChildren = nChildren };
        var ctxHandle = System.Runtime.InteropServices.GCHandle.Alloc(ctx);
        cArray->PrivateData = (void*)(IntPtr)ctxHandle;
        // Use a no-op release callback. The GCHandle prevents collection of the pinned data.
        // The native side will call release when done, but we don't free anything since
        // the GCHandle + MemoryHandles keep everything alive until the GCHandle is collected.
        cArray->ReleaseCallback = (IntPtr)(delegate* unmanaged<ArrowArray*, void>)&ReleaseNoOp;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static unsafe void ReleaseNoOp(ArrowArray* cArray)
    {
        // Intentional no-op. Memory is managed by GCHandle + MemoryHandle.
        // Will be cleaned up when GC collects the ManualExportContext.
        cArray->ReleaseCallback = IntPtr.Zero;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static unsafe void ReleaseManualArray(ArrowArray* cArray)
    {
        try
        {
            if (cArray->PrivateData == null) return;
            var ctxHandle = System.Runtime.InteropServices.GCHandle.FromIntPtr((IntPtr)cArray->PrivateData);
            var ctx = (ManualExportContext)ctxHandle.Target!;
            foreach (var h in ctx.Handles) h.Dispose();
            System.Runtime.InteropServices.NativeMemory.Free(ctx.Buffers);
            if (ctx.Children != null)
            {
                for (var i = 0; i < ctx.NChildren; i++)
                {
                    if (ctx.Children[i] != null && ctx.Children[i]->ReleaseCallback != IntPtr.Zero)
                    {
                        var release = (delegate* unmanaged<ArrowArray*, void>)ctx.Children[i]->ReleaseCallback;
                        release(ctx.Children[i]);
                    }
                    System.Runtime.InteropServices.NativeMemory.Free(ctx.Children[i]);
                }
                System.Runtime.InteropServices.NativeMemory.Free(ctx.Children);
            }
            ctxHandle.Free();
            cArray->PrivateData = null;
            cArray->ReleaseCallback = IntPtr.Zero;
        }
        catch
        {
            // Suppress errors during process shutdown
        }
    }

    private unsafe class ManualExportContext
    {
        public IArrowArray Array = null!; // prevent GC of the source array
        public System.Buffers.MemoryHandle[] Handles = [];
        public void** Buffers;
        public ArrowArray** Children;
        public int NChildren;
    }

    /// <summary>
    /// Exports an Arrow <see cref="IArrowType"/> to a native <see cref="ArrowSchema"/> struct.
    /// Used for component type registration.
    /// </summary>
    public static unsafe void ExportSchema(IArrowType dataType, ArrowSchema* cSchema)
    {
        var cArrowSchema = (CArrowSchema*)cSchema;
        CArrowSchemaExporter.ExportType(dataType, cArrowSchema);
    }

    /// <summary>
    /// Exports both array and schema for a component batch in one call.
    /// </summary>
    public static unsafe void ExportArrayAndSchema(IArrowArray array, ArrowArray* cArray, ArrowSchema* cSchema)
    {
        ExportArray(array, cArray);
        ExportSchema(array.Data.DataType, cSchema);
    }
}
