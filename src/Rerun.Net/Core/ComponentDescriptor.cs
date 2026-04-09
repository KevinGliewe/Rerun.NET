namespace Rerun.Net;

/// <summary>
/// Semantic metadata for a component: archetype name, field name, and component type name.
/// </summary>
public readonly record struct ComponentDescriptor(
    string? ArchetypeName,
    string? ComponentName,
    string? ComponentTypeName);
