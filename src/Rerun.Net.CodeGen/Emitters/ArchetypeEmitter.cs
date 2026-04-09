using System.Text;

namespace Rerun.Net.CodeGen.Emitters;

internal class ArchetypeEmitter : EmitterBase
{
    public string Emit(FbsTable archetype)
    {
        var sb = new StringBuilder();
        WriteHeader(sb);
        sb.AppendLine("using Rerun.Net.Components;");
        sb.AppendLine();
        sb.AppendLine("namespace Rerun.Net.Archetypes;");
        sb.AppendLine();
        WriteDocComment(sb, archetype.DocComment);

        var fqn = $"{archetype.Namespace}.{archetype.Name}";

        sb.AppendLine($"public sealed partial class {archetype.Name} : IAsComponents");
        sb.AppendLine("{");
        sb.AppendLine($"    private const string ArchetypeName = \"{fqn}\";");
        sb.AppendLine();

        var required = archetype.Fields.Where(f => f.Attributes.ContainsKey("attr.rerun.component_required")).ToList();
        var recommended = archetype.Fields.Where(f => f.Attributes.ContainsKey("attr.rerun.component_recommended")).ToList();
        var optional = archetype.Fields.Where(f => f.Attributes.ContainsKey("attr.rerun.component_optional")).ToList();
        var allFields = required.Concat(recommended).Concat(optional).ToList();

        // Properties
        foreach (var field in required)
            EmitProperty(sb, field, false, archetype.Name);
        foreach (var field in recommended.Concat(optional))
            EmitProperty(sb, field, true, archetype.Name);
        sb.AppendLine();

        // Constructor with required fields
        EmitConstructor(sb, archetype.Name, required);

        // With* builder methods for recommended + optional
        foreach (var field in recommended.Concat(optional))
            EmitWithMethod(sb, archetype.Name, field);

        // Partial update: UpdateFields / ClearFields
        EmitPartialUpdateMethods(sb, archetype.Name, required);

        // AsBatches
        EmitAsBatches(sb, allFields, archetype.Name);

        sb.AppendLine("}");
        return sb.ToString();
    }

    private void EmitProperty(StringBuilder sb, FbsField field, bool nullable, string archetypeName)
    {
        var propName = SafePropName(ToPascalCase(field.Name), archetypeName);
        var nullSuffix = nullable ? "?" : "";
        sb.AppendLine($"    public ComponentBatch{nullSuffix} {propName} {{ get; private set; }}");
    }

    private string PropName(FbsField f, string archetypeName) => SafePropName(ToPascalCase(f.Name), archetypeName);

    private void EmitConstructor(StringBuilder sb, string typeName, List<FbsField> required)
    {
        if (required.Count == 0)
        {
            sb.AppendLine($"    public {typeName}() {{ }}");
            sb.AppendLine();
            return;
        }

        var paramList = string.Join(", ", required.Select(f =>
        {
            var compType = GetComponentType(f, typeName);
            return $"ReadOnlySpan<{compType}> {f.Name}";
        }));

        sb.AppendLine($"    public {typeName}({paramList})");
        sb.AppendLine("    {");
        foreach (var field in required)
        {
            var pn = PropName(field, typeName);
            var compFqn = GetComponentFqn(field);
            sb.AppendLine($"        {pn} = ComponentBatch.FromLoggable({field.Name},");
            sb.AppendLine($"            new ComponentDescriptor(ArchetypeName, \"{typeName}:{field.Name}\", \"{compFqn}\"));");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        // Array overload (only params for single-required to avoid C# limitation)
        var arrayParams = required.Select((f, idx) =>
        {
            var compType = GetComponentType(f, typeName);
            var prefix = (required.Count == 1) ? "params " : "";
            return $"{prefix}{compType}[] {f.Name}";
        });
        var callArgs = string.Join(", ", required.Select(f => $"{f.Name}.AsSpan()"));

        sb.AppendLine($"    public {typeName}({string.Join(", ", arrayParams)}) : this({callArgs}) {{ }}");
        sb.AppendLine();
    }

    private void EmitWithMethod(StringBuilder sb, string typeName, FbsField field)
    {
        var pn = PropName(field, typeName);
        var compType = GetComponentType(field, typeName);
        var compFqn = GetComponentFqn(field);

        sb.AppendLine($"    public {typeName} With{pn}(ReadOnlySpan<{compType}> {field.Name})");
        sb.AppendLine("    {");
        sb.AppendLine($"        {pn} = ComponentBatch.FromLoggable({field.Name},");
        sb.AppendLine($"            new ComponentDescriptor(ArchetypeName, \"{typeName}:{field.Name}\", \"{compFqn}\"));");
        sb.AppendLine($"        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public {typeName} With{pn}(params {compType}[] {field.Name}) => With{pn}({field.Name}.AsSpan());");
        sb.AppendLine();
    }

    private void EmitAsBatches(StringBuilder sb, List<FbsField> allFields, string archetypeName)
    {
        sb.AppendLine("    public IReadOnlyList<ComponentBatch> AsBatches()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var batches = new List<ComponentBatch>({allFields.Count});");
        foreach (var field in allFields)
        {
            var pn = PropName(field, archetypeName);
            var isRequired = field.Attributes.ContainsKey("attr.rerun.component_required");
            if (isRequired)
                sb.AppendLine($"        batches.Add({pn});");
            else
                sb.AppendLine($"        if ({pn} != null) batches.Add({pn});");
        }
        sb.AppendLine("        return batches;");
        sb.AppendLine("    }");
    }

    private void EmitPartialUpdateMethods(StringBuilder sb, string typeName, List<FbsField> required)
    {
        sb.AppendLine($"    /// <summary>Create an empty {typeName} for partial updates. Use With*() to set fields.</summary>");
        sb.AppendLine($"    public static {typeName} UpdateFields() => new();");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Clear all fields, then use With*() to set specific ones.</summary>");
        sb.AppendLine($"    public static {typeName} ClearFields() => new();");
        sb.AppendLine();

        // Only emit private parameterless constructor if there are required fields
        // (archetypes with no required fields already have a public parameterless constructor)
        if (required.Count > 0)
        {
            sb.AppendLine($"    private {typeName}() {{ }}");
            sb.AppendLine();
        }
    }

    private static string GetComponentType(FbsField field, string? archetypeName = null)
    {
        var ft = field.FieldType;
        if (ft is FbsArray a) ft = a.ElementType;
        var shortName = ft switch
        {
            FbsReference r => GetShortName(r.FullyQualifiedName),
            _ => throw new ArgumentException($"Archetype field must reference a component type: {field.Name}")
        };
        // Always qualify with Components. to avoid collisions with archetype class names
        return $"Components.{shortName}";
    }

    private static string GetComponentFqn(FbsField field)
    {
        var ft = field.FieldType;
        if (ft is FbsArray a) ft = a.ElementType;
        return ft switch
        {
            FbsReference r => r.FullyQualifiedName,
            _ => throw new ArgumentException($"Cannot get FQN for field: {field.Name}")
        };
    }
}
