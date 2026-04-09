using System.Reflection;
using System.Runtime.InteropServices;

namespace Rerun.Net.Native;

internal static class LibraryLoader
{
    private const string LibraryName = "rerun_c";

    static LibraryLoader()
    {
        NativeLibrary.SetDllImportResolver(
            typeof(LibraryLoader).Assembly,
            ResolveLibrary);
    }

    /// <summary>Ensures the native library resolver is registered.</summary>
    internal static void Initialize() { }

    private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
            return IntPtr.Zero;

        var rid = RuntimeInformation.RuntimeIdentifier;
        var ext = GetNativeExtension();
        var prefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : "lib";

        var assemblyDir = Path.GetDirectoryName(assembly.Location) ?? ".";
        var candidatePaths = new[]
        {
            Path.Combine(assemblyDir, "runtimes", rid, "native", $"{prefix}{LibraryName}{ext}"),
            Path.Combine(assemblyDir, $"{prefix}{LibraryName}{ext}"),
        };

        foreach (var path in candidatePaths)
        {
            if (NativeLibrary.TryLoad(path, out var handle))
                return handle;
        }

        // Fall back to default OS search
        return IntPtr.Zero;
    }

    private static string GetNativeExtension()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return ".dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return ".so";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return ".dylib";
        return ".so";
    }
}
