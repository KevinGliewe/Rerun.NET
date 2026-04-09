using Apache.Arrow;

namespace Rerun.Net;

/// <summary>
/// Implemented by component types to serialize themselves into Arrow arrays.
/// </summary>
public interface ILoggable<T> where T : struct
{
    static abstract IArrowArray ToArrow(ReadOnlySpan<T> data);

    static abstract ComponentDescriptor Descriptor { get; }
}
