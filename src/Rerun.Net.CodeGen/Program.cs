using System.Text.RegularExpressions;
using Rerun.Net.CodeGen;
using Rerun.Net.CodeGen.Emitters;

var definitionsPath = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "extern", "rerun", "crates", "store", "re_sdk_types", "definitions", "rerun"));

var outputPath = args.Length > 1
    ? args[1]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Rerun.Net"));

if (!Directory.Exists(definitionsPath))
{
    Console.Error.WriteLine($"Definitions path not found: {definitionsPath}");
    return 1;
}

Console.WriteLine($"Definitions: {definitionsPath}");
Console.WriteLine($"Output:      {outputPath}");

var parser = new FbsParser();
var registry = new TypeRegistry();

// Parse all .fbs files (core + blueprint)
var fbsDirs = new[] {
    "datatypes", "components", "archetypes",
    "blueprint/datatypes", "blueprint/components", "blueprint/archetypes"
};
foreach (var dir in fbsDirs)
{
    var dirPath = Path.Combine(definitionsPath, dir);
    if (!Directory.Exists(dirPath)) continue;
    foreach (var file in Directory.GetFiles(dirPath, "*.fbs"))
    {
        try
        {
            var parsed = parser.Parse(file);
            registry.Register(parsed);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to parse {Path.GetFileName(file)}: {ex.Message}");
        }
    }
}

var datatypeEmitter = new DatatypeEmitter(registry);
var componentEmitter = new ComponentEmitter();
var archetypeEmitter = new ArchetypeEmitter();

var counts = new int[3];

var emittedTypes = new HashSet<string>(); // Track FQNs of successfully emitted types

// Generate datatypes
foreach (var type in registry.GetByNamespace("rerun.datatypes"))
{
    try
    {
        var code = datatypeEmitter.Emit(type);
        if (code == null) continue;
        var outFile = Path.Combine(outputPath, "Datatypes", $"{type.Name}.g.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        File.WriteAllText(outFile, code);
        emittedTypes.Add($"{type.Namespace}.{type.Name}");
        counts[0]++;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: Failed to emit datatype {type.Name}: {ex.Message}");
    }
}

// Helper: check if component references a type that wasn't emitted (and isn't a skipped primitive)
bool ReferencesUnavailableType(FbsType type)
{
    var fields = type switch
    {
        FbsStruct s => s.Fields,
        FbsTable t => t.Fields,
        _ => Array.Empty<FbsField>().ToList()
    };
    return fields.Any(f => HasMissingRef(f.FieldType));
}
bool HasMissingRef(FbsFieldType ft) => ft switch
{
    FbsReference r when r.FullyQualifiedName.StartsWith("rerun.datatypes.") =>
        !emittedTypes.Contains(r.FullyQualifiedName) && !DatatypeEmitter.PrimitiveWrapperTypes.ContainsKey(r.FullyQualifiedName),
    FbsArray a => HasMissingRef(a.ElementType),
    _ => false
};

// Generate components
foreach (var type in registry.GetByNamespace("rerun.components"))
{
    if (ReferencesUnavailableType(type)) continue;
    try
    {
        var code = componentEmitter.Emit(type);
        var outFile = Path.Combine(outputPath, "Components", $"{type.Name}.g.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        File.WriteAllText(outFile, code);
        emittedTypes.Add($"{type.Namespace}.{type.Name}");
        counts[1]++;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: Failed to emit component {type.Name}: {ex.Message}");
    }
}

// Helper: check if archetype references unavailable components
bool ArchetypeHasMissingComponent(FbsTable arch)
{
    return arch.Fields.Any(f =>
    {
        var ft = f.FieldType;
        if (ft is FbsArray a) ft = a.ElementType;
        return ft is FbsReference r && r.FullyQualifiedName.StartsWith("rerun.components.") && !emittedTypes.Contains(r.FullyQualifiedName);
    });
}

// Generate archetypes
foreach (var type in registry.GetByNamespace("rerun.archetypes").OfType<FbsTable>())
{
    if (ArchetypeHasMissingComponent(type)) continue;
    try
    {
        var code = archetypeEmitter.Emit(type);
        var outFile = Path.Combine(outputPath, "Archetypes", $"{type.Name}.g.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        File.WriteAllText(outFile, code);
        counts[2]++;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: Failed to emit archetype {type.Name}: {ex.Message}");
    }
}

// Sets of all known type short-names per category, used by the blueprint
// post-processor to fully-qualify cross-namespace references.
var coreDatatypeNames = registry.GetByNamespace("rerun.datatypes")
    .Select(t => t.Name).ToHashSet();
var coreComponentNames = registry.GetByNamespace("rerun.components")
    .Select(t => t.Name).ToHashSet();
var bpDatatypeNames = registry.GetByNamespace("rerun.blueprint.datatypes")
    .Select(t => t.Name).ToHashSet();
var bpComponentNames = registry.GetByNamespace("rerun.blueprint.components")
    .Select(t => t.Name).ToHashSet();

// Inside Rerun.Net.Blueprint.* the unqualified names "Datatypes" and "Components"
// resolve to the closer Rerun.Net.Blueprint.{Datatypes,Components} sibling, which
// shadows the matching core namespaces. Rewrite every "Datatypes.X" / "Components.X"
// reference to a fully-qualified global:: path so the resolution is unambiguous.
var qualifiedRefRegex = new Regex(@"\b(Datatypes|Components)\.([A-Z]\w*)");
string FullyQualifyBlueprintRefs(string code) => qualifiedRefRegex.Replace(code, m =>
{
    var category = m.Groups[1].Value;
    var name = m.Groups[2].Value;
    if (category == "Datatypes")
    {
        if (bpDatatypeNames.Contains(name))
            return $"global::Rerun.Net.Blueprint.Datatypes.{name}";
        if (coreDatatypeNames.Contains(name))
            return $"global::Rerun.Net.Datatypes.{name}";
    }
    else
    {
        if (bpComponentNames.Contains(name))
            return $"global::Rerun.Net.Blueprint.Components.{name}";
        if (coreComponentNames.Contains(name))
            return $"global::Rerun.Net.Components.{name}";
    }
    return m.Value;
});

// Generate blueprint types — same emitters, different namespace mapping
var bpNamespaces = new[] {
    ("rerun.blueprint.datatypes", "Blueprint/Datatypes", datatypeEmitter),
    ("rerun.blueprint.components", "Blueprint/Components", (object)componentEmitter),
    ("rerun.blueprint.archetypes", "Blueprint/Archetypes", (object)archetypeEmitter),
};

var bpCounts = new int[3];
for (var idx = 0; idx < bpNamespaces.Length; idx++)
{
    var (ns, subDir, emitter) = bpNamespaces[idx];
    foreach (var type in registry.GetByNamespace(ns))
    {
        if (ReferencesUnavailableType(type)) continue;
        if (type is FbsTable t && idx == 2 && ArchetypeHasMissingComponent(t)) continue;
        try
        {
            string? code = idx switch
            {
                0 => datatypeEmitter.Emit(type),
                1 => componentEmitter.Emit(type),
                2 => type is FbsTable at ? archetypeEmitter.Emit(at) : null,
                _ => null
            };
            if (code == null) continue;

            // Rewrite namespace to Blueprint.* but keep core type imports
            code = code.Replace("namespace Rerun.Net.Datatypes;",
                           "using Rerun.Net.Datatypes;\n\nnamespace Rerun.Net.Blueprint.Datatypes;")
                       .Replace("namespace Rerun.Net.Components;",
                           "using Rerun.Net.Datatypes;\nusing Rerun.Net.Components;\n\nnamespace Rerun.Net.Blueprint.Components;")
                       .Replace("namespace Rerun.Net.Archetypes;",
                           "using Rerun.Net.Components;\nusing Rerun.Net.Blueprint.Components;\n\nnamespace Rerun.Net.Blueprint.Archetypes;");

            // Fully-qualify cross-namespace type references so blueprint sibling
            // namespaces don't shadow the core ones.
            code = FullyQualifyBlueprintRefs(code);

            var outFile = Path.Combine(outputPath, subDir, $"{type.Name}.g.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
            File.WriteAllText(outFile, code);
            emittedTypes.Add($"{type.Namespace}.{type.Name}");
            bpCounts[idx]++;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to emit blueprint {type.Name}: {ex.Message}");
        }
    }
}

Console.WriteLine($"Generated: {counts[0]} datatypes, {counts[1]} components, {counts[2]} archetypes");
Console.WriteLine($"Blueprint: {bpCounts[0]} datatypes, {bpCounts[1]} components, {bpCounts[2]} archetypes");
return 0;
