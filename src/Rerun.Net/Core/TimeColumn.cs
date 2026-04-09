namespace Rerun.Net;

/// <summary>
/// A column of timestamps for the columnar send API.
/// </summary>
public sealed class TimeColumn
{
    public string TimelineName { get; }
    public TimeType Type { get; }
    public long[] Values { get; }
    public SortingStatus Sorting { get; }

    public TimeColumn(string timelineName, TimeType type, long[] values, SortingStatus sorting = SortingStatus.Unknown)
    {
        TimelineName = timelineName;
        Type = type;
        Values = values;
        Sorting = sorting;
    }
}

public enum TimeType : uint
{
    Sequence = 1,
    Duration = 2,
    Timestamp = 3,
}

public enum SortingStatus : uint
{
    Unknown = 0,
    Sorted = 1,
    Unsorted = 2,
}
