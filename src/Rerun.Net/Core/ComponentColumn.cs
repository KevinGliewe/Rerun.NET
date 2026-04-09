using Apache.Arrow;

namespace Rerun.Net;

/// <summary>
/// A column of component data for the columnar send API.
/// Wraps a ListArray of component batches with a component descriptor.
/// </summary>
public sealed class ComponentColumn
{
    public IArrowArray Array { get; }
    public ComponentDescriptor Descriptor { get; }

    public ComponentColumn(IArrowArray array, ComponentDescriptor descriptor)
    {
        Array = array;
        Descriptor = descriptor;
    }
}
