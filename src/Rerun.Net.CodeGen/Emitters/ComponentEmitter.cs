using System.Text;

namespace Rerun.Net.CodeGen.Emitters;

internal class ComponentEmitter : EmitterBase
{
    public string Emit(FbsType type)
    {
        // Handle enum components — they become enum wrappers
        if (type is FbsEnum e)
            return EmitEnumComponent(e);

        var field = type switch
        {
            FbsStruct s => s.Fields.FirstOrDefault(),
            FbsTable t => t.Fields.FirstOrDefault(),
            _ => null
        };

        if (field == null)
            throw new ArgumentException($"Component {type.Name} has no fields");

        var sb = new StringBuilder();
        WriteHeader(sb);
        sb.AppendLine("using Apache.Arrow;");
        sb.AppendLine("using Rerun.Net.Datatypes;");
        sb.AppendLine();
        sb.AppendLine("namespace Rerun.Net.Components;");
        sb.AppendLine();
        WriteDocComment(sb, type.DocComment);

        var fqn = $"{type.Namespace}.{type.Name}";
        var (csType, _) = ResolveFieldType(field.FieldType, type.Name);
        var propName = SafePropertyName(ToPascalCase(field.Name), type.Name);

        sb.AppendLine($"public readonly record struct {type.Name}({csType} {propName}) : ILoggable<{type.Name}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public static ComponentDescriptor Descriptor => new(null, null, \"{fqn}\");");
        sb.AppendLine();
        sb.AppendLine($"    public static IArrowArray ToArrow(ReadOnlySpan<{type.Name}> data)");
        sb.AppendLine("    {");

        switch (field.FieldType)
        {
            case FbsReference r when DatatypeEmitter.PrimitiveWrapperTypes.TryGetValue(r.FullyQualifiedName, out var prim):
                // Skipped datatype — emit primitive Arrow directly
                var primBuilder = MapPrimitiveToArrowBuilder(prim);
                sb.AppendLine($"        var builder = new {primBuilder}();");
                sb.AppendLine($"        foreach (var v in data)");
                sb.AppendLine($"            builder.Append(v.{propName});");
                sb.AppendLine($"        return builder.Build();");
                break;

            case FbsReference r:
                var innerName = QualifyDatatypeRef(r, type.Name);
                sb.AppendLine($"        var inner = new {innerName}[data.Length];");
                sb.AppendLine($"        for (var i = 0; i < data.Length; i++)");
                sb.AppendLine($"            inner[i] = data[i].{propName};");
                sb.AppendLine($"        return {innerName}.ToArrow(inner);");
                break;

            case FbsPrimitive p:
                var builder2 = MapPrimitiveToArrowBuilder(p.TypeName);
                sb.AppendLine($"        var builder = new {builder2}();");
                sb.AppendLine($"        foreach (var v in data)");
                sb.AppendLine($"            builder.Append(v.{propName});");
                sb.AppendLine($"        return builder.Build();");
                break;

            case FbsArray { ElementType: FbsReference elemRef }:
                // Array-of-datatype field (e.g., LineStrip2D wrapping Vec2D[])
                // Serialize all inner elements flat, then build a ListArray with offsets
                var elemName = QualifyDatatypeRef(elemRef, type.Name);
                sb.AppendLine($"        // Flatten all inner arrays and build offsets for ListArray");
                sb.AppendLine($"        var allItems = new System.Collections.Generic.List<{elemName}>();");
                sb.AppendLine($"        var offsets = new Int32Array.Builder();");
                sb.AppendLine($"        offsets.Append(0);");
                sb.AppendLine($"        foreach (var v in data)");
                sb.AppendLine("        {");
                sb.AppendLine($"            foreach (var item in v.{propName})");
                sb.AppendLine($"                allItems.Add(item);");
                sb.AppendLine($"            offsets.Append(allItems.Count);");
                sb.AppendLine("        }");
                sb.AppendLine($"        var valuesArray = {elemName}.ToArrow(allItems.ToArray());");
                sb.AppendLine($"        var offsetsArray = offsets.Build();");
                sb.AppendLine($"        var listType = new Apache.Arrow.Types.ListType(");
                sb.AppendLine($"            new Apache.Arrow.Field(\"item\", valuesArray.Data.DataType, false));");
                sb.AppendLine($"        var listData = new ArrayData(listType, data.Length, 0, 0,");
                sb.AppendLine($"            new[] {{ ArrowBuffer.Empty, offsetsArray.ValueBuffer }},");
                sb.AppendLine($"            new[] {{ valuesArray.Data }});");
                sb.AppendLine($"        return new ListArray(listData);");
                break;

            default:
                sb.AppendLine($"        throw new NotImplementedException();");
                break;
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string EmitEnumComponent(FbsEnum e)
    {
        var sb = new StringBuilder();
        WriteHeader(sb);
        sb.AppendLine("using Apache.Arrow;");
        sb.AppendLine();
        sb.AppendLine("namespace Rerun.Net.Components;");
        sb.AppendLine();

        var fqn = $"{e.Namespace}.{e.Name}";
        var csUnderlying = MapPrimitiveToCSharp(e.UnderlyingType);
        var arrowBuilder = MapPrimitiveToArrowBuilder(e.UnderlyingType);
        var enumTypeName = $"{e.Name}Value";

        // Emit the enum
        WriteDocComment(sb, e.DocComment);
        sb.AppendLine($"public enum {enumTypeName} : {csUnderlying}");
        sb.AppendLine("{");
        foreach (var v in e.Values)
        {
            if (v.Value.HasValue)
                sb.AppendLine($"    {v.Name} = {v.Value.Value},");
            else
                sb.AppendLine($"    {v.Name},");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // Emit the ILoggable wrapper struct
        WriteDocComment(sb, e.DocComment);
        sb.AppendLine($"public readonly record struct {e.Name}({enumTypeName} Value) : ILoggable<{e.Name}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public static ComponentDescriptor Descriptor => new(null, null, \"{fqn}\");");
        sb.AppendLine();
        sb.AppendLine($"    public static IArrowArray ToArrow(ReadOnlySpan<{e.Name}> data)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var builder = new {arrowBuilder}();");
        sb.AppendLine($"        foreach (var v in data)");
        sb.AppendLine($"            builder.Append(({csUnderlying})v.Value);");
        sb.AppendLine($"        return builder.Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>Qualify datatype references to avoid name collisions with component names.</summary>
    private static string QualifyDatatypeRef(FbsReference r, string componentName)
    {
        var shortName = GetShortName(r.FullyQualifiedName);
        if (shortName == componentName && r.FullyQualifiedName.Contains("datatypes"))
            return $"Datatypes.{shortName}";
        return shortName;
    }

    /// <summary>Avoid property name matching the enclosing type name.</summary>
    private static string SafePropertyName(string propName, string typeName)
        => propName == typeName ? $"{propName}Value" : propName;

    private static (string CsType, bool IsArray) ResolveFieldType(FbsFieldType ft, string componentName)
    {
        return ft switch
        {
            FbsPrimitive p => (MapPrimitiveToCSharp(p.TypeName), false),
            FbsReference r when DatatypeEmitter.PrimitiveWrapperTypes.TryGetValue(r.FullyQualifiedName, out var prim)
                => (MapPrimitiveToCSharp(prim), false),
            FbsReference r => (QualifyDatatypeRef(r, componentName), false),
            FbsArray a => ($"{ResolveFieldType(a.ElementType, componentName).CsType}[]", true),
            FbsFixedArray fa => ($"{ResolveFieldType(fa.ElementType, componentName).CsType}[]", true),
            _ => throw new ArgumentException($"Unknown field type: {ft}")
        };
    }
}
