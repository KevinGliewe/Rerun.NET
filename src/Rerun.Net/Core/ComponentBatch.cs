using Apache.Arrow;

namespace Rerun.Net;

/// <summary>
/// A batch of component instances serialized as an Arrow array, paired with a component descriptor.
/// </summary>
public sealed class ComponentBatch
{
    public IArrowArray Array { get; }
    public ComponentDescriptor Descriptor { get; }

    public ComponentBatch(IArrowArray array, ComponentDescriptor descriptor)
    {
        Array = array;
        Descriptor = descriptor;
    }

    public static ComponentBatch FromLoggable<T>(ReadOnlySpan<T> data, ComponentDescriptor descriptor)
        where T : struct, ILoggable<T>
    {
        var array = T.ToArrow(data);
        return new ComponentBatch(array, descriptor);
    }
}
