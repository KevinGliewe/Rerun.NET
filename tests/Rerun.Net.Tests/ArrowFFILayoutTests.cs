using System.Runtime.InteropServices;
using Apache.Arrow.C;
using Rerun.Net.Native;
using Xunit;
using Xunit.Abstractions;

namespace Rerun.Net.Tests;

public class ArrowFFILayoutTests
{
    private readonly ITestOutputHelper _output;
    public ArrowFFILayoutTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ArrowArrayLayoutMatchesCArrowArray()
    {
        var ourSize = Marshal.SizeOf<ArrowArray>();
        var apacheSize = Marshal.SizeOf<CArrowArray>();
        _output.WriteLine($"Our ArrowArray:     {ourSize} bytes");
        _output.WriteLine($"Apache CArrowArray: {apacheSize} bytes");
        Assert.Equal(apacheSize, ourSize);
    }

    [Fact]
    public void ArrowSchemaLayoutMatchesCArrowSchema()
    {
        var ourSize = Marshal.SizeOf<ArrowSchema>();
        var apacheSize = Marshal.SizeOf<CArrowSchema>();
        _output.WriteLine($"Our ArrowSchema:     {ourSize} bytes");
        _output.WriteLine($"Apache CArrowSchema: {apacheSize} bytes");
        Assert.Equal(apacheSize, ourSize);
    }
}
