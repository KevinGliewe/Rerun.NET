using System.Collections.Concurrent;
using System.Text;
using Apache.Arrow.Types;
using Rerun.Net.Native;

namespace Rerun.Net;

/// <summary>
/// Thread-safe registry for component types. Each unique component descriptor is registered
/// with the native rerun_c library exactly once, and the returned handle is cached.
/// </summary>
internal static class ComponentTypeRegistry
{
    private static readonly ConcurrentDictionary<string, uint> _handles = new();

    /// <summary>
    /// Returns the native component type handle for the given descriptor and Arrow type.
    /// Registers with rerun_c on first encounter.
    /// </summary>
    public static unsafe uint GetOrRegister(ComponentDescriptor descriptor, IArrowType arrowType)
    {
        // Key must include the full descriptor since the same component type (e.g., Position3D)
        // can appear with different archetype/field descriptors (Points3D:positions vs Arrows3D:origins)
        var key = $"{descriptor.ArchetypeName}|{descriptor.ComponentName}|{descriptor.ComponentTypeName}";

        return _handles.GetOrAdd(key, _ => Register(descriptor, arrowType));
    }

    private static unsafe uint Register(ComponentDescriptor descriptor, IArrowType arrowType)
    {
        var archetypeBytes = descriptor.ArchetypeName != null ? Encoding.UTF8.GetBytes(descriptor.ArchetypeName) : [];
        var componentBytes = descriptor.ComponentName != null ? Encoding.UTF8.GetBytes(descriptor.ComponentName) : [];
        var componentTypeBytes = descriptor.ComponentTypeName != null ? Encoding.UTF8.GetBytes(descriptor.ComponentTypeName) : [];

        fixed (byte* archPtr = archetypeBytes)
        fixed (byte* compPtr = componentBytes)
        fixed (byte* typePtr = componentTypeBytes)
        {
            var componentType = new RrComponentType
            {
                Descriptor = new RrComponentDescriptor
                {
                    Archetype = descriptor.ArchetypeName != null
                        ? new RrString { Utf8 = archPtr, LengthInBytes = (uint)archetypeBytes.Length }
                        : default,
                    Component = descriptor.ComponentName != null
                        ? new RrString { Utf8 = compPtr, LengthInBytes = (uint)componentBytes.Length }
                        : default,
                    ComponentType = descriptor.ComponentTypeName != null
                        ? new RrString { Utf8 = typePtr, LengthInBytes = (uint)componentTypeBytes.Length }
                        : default,
                },
            };

            // Export the Arrow schema — rerun_c takes ownership of the schema's release callback
            ArrowFFI.ExportSchema(arrowType, &componentType.Schema);

            var error = new RrError();
            var handle = NativeMethods.RrRegisterComponentType(componentType, ref error);
            RerunException.ThrowIfError(error);
            return handle;
        }
    }
}
