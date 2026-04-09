using Apache.Arrow;
using Apache.Arrow.C;
using Apache.Arrow.Types;
using Xunit;
using Xunit.Abstractions;

namespace Rerun.Net.Tests;

public class ArrowCDataProbe
{
    private readonly ITestOutputHelper _output;

    public ArrowCDataProbe(ITestOutputHelper output) => _output = output;

    [Fact]
    public unsafe void ExportAndVerifyArrowArray()
    {
        // Build a simple Float32 array
        var builder = new FloatArray.Builder();
        builder.Append(1.0f);
        builder.Append(2.0f);
        builder.Append(3.0f);
        var array = builder.Build();

        // Export via C Data Interface
        var cArray = new CArrowArray();
        var cSchema = new CArrowSchema();
        CArrowArrayExporter.ExportArray(array, &cArray);
        CArrowSchemaExporter.ExportType(array.Data.DataType, &cSchema);

        _output.WriteLine($"Array length: {cArray.length}");
        _output.WriteLine($"Array null_count: {cArray.null_count}");
        _output.WriteLine($"Array n_buffers: {cArray.n_buffers}");
        _output.WriteLine($"Schema format: {System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)cSchema.format)}");

        Assert.Equal(3, cArray.length);
        Assert.Equal(0, cArray.null_count);
        Assert.True(cSchema.format != null);

        // Import back to verify round-trip
        var importedType = CArrowSchemaImporter.ImportType(&cSchema);
        var importedArray = CArrowArrayImporter.ImportArray(&cArray, importedType);

        Assert.Equal(3, importedArray.Length);
        Assert.IsType<FloatArray>(importedArray);
        var floatArr = (FloatArray)importedArray;
        Assert.Equal(1.0f, floatArr.GetValue(0));
        Assert.Equal(2.0f, floatArr.GetValue(1));
        Assert.Equal(3.0f, floatArr.GetValue(2));
    }

    [Fact]
    public unsafe void ExportSchemaOnly()
    {
        // Test schema-only export (needed for component type registration)
        var dataType = new FloatType();
        var cSchema = new CArrowSchema();
        CArrowSchemaExporter.ExportType(dataType, &cSchema);

        var format = System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)cSchema.format);
        _output.WriteLine($"Float32 format string: '{format}'");
        Assert.Equal("f", format); // Arrow format string for float32

        // Clean up - import consumes the schema
        var imported = CArrowSchemaImporter.ImportType(&cSchema);
        Assert.IsType<FloatType>(imported);
    }
}
