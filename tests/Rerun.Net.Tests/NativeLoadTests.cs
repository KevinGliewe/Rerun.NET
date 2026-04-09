using Xunit;

namespace Rerun.Net.Tests;

public class NativeLoadTests
{
    [Fact]
    public void CanLoadNativeLibrary()
    {
        // Creating a RecordingStream triggers LibraryLoader which registers
        // the DllImportResolver for runtimes/{rid}/native/ lookup.
        using var rec = new RecordingStream("test_native_load");
        Assert.True(rec.IsEnabled);
    }

    [Fact]
    public void VersionStringIsNotNull()
    {
        var version = RecordingStream.VersionString();
        Assert.False(string.IsNullOrEmpty(version));
    }
}
