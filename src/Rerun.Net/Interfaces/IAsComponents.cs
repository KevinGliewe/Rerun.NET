namespace Rerun.Net;

/// <summary>
/// Implemented by archetypes to provide their data as a collection of component batches.
/// </summary>
public interface IAsComponents
{
    IReadOnlyList<ComponentBatch> AsBatches();
}
