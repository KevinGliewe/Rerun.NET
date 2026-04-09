using System.Text;

namespace Rerun.Net.CodeGen.Emitters;

internal class DatatypeEmitter : EmitterBase
{
    // Primitive-wrapper datatypes that clash with System types.
    // Components referencing them can optionally inline the primitive Arrow directly.
    internal static readonly Dictionary<string, string> PrimitiveWrapperTypes = new()
    {
        ["rerun.datatypes.UInt16"] = "ushort", ["rerun.datatypes.UInt32"] = "uint",
        ["rerun.datatypes.UInt64"] = "ulong", ["rerun.datatypes.Int16"] = "short",
        ["rerun.datatypes.Int32"] = "int", ["rerun.datatypes.Int64"] = "long",
        ["rerun.datatypes.Float32"] = "float", ["rerun.datatypes.Float64"] = "double",
        ["rerun.datatypes.Bool"] = "bool",
    };

    public string? Emit(FbsType type)
    {
        return type switch
        {
            FbsStruct s => EmitStruct(s),
            FbsTable t => EmitTable(t),
            FbsEnum e => EmitEnum(e),
            FbsUnion u => EmitUnion(u),
            _ => throw new ArgumentException($"Unsupported type: {type.GetType().Name}")
        };
    }

    private string EmitStruct(FbsStruct s)
    {
        var sb = new StringBuilder();
        WriteHeader(sb);
        sb.AppendLine("using Apache.Arrow;");
        sb.AppendLine("using Apache.Arrow.Types;");
        sb.AppendLine();
        sb.AppendLine($"namespace Rerun.Net.Datatypes;");
        sb.AppendLine();
        WriteDocComment(sb, s.DocComment);

        var isTransparent = s.Attributes.ContainsKey("attr.arrow.transparent") && s.Fields.Count == 1;
        var fqn = $"{s.Namespace}.{s.Name}";

        if (isTransparent)
            EmitTransparentStruct(sb, s, fqn);
        else
            EmitRegularStruct(sb, s, fqn);

        return sb.ToString();
    }

    private void EmitTransparentStruct(StringBuilder sb, FbsStruct s, string fqn)
    {
        var field = s.Fields[0];
        var (csType, _) = ResolveFieldType(field.FieldType);
        var propName = SafePropName(ToPascalCase(field.Name), s.Name);

        sb.AppendLine($"public readonly record struct {s.Name}({csType} {propName}) : ILoggable<{s.Name}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public static ComponentDescriptor Descriptor => new(null, null, \"{fqn}\");");
        sb.AppendLine();
        sb.AppendLine($"    public static IArrowArray ToArrow(ReadOnlySpan<{s.Name}> data)");
        sb.AppendLine("    {");
        EmitTransparentToArrow(sb, field, s.Name);
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private void EmitRegularStruct(StringBuilder sb, FbsStruct s, string fqn)
    {
        // Record struct with all fields as constructor params
        var paramList = string.Join(", ", s.Fields.Select(f =>
        {
            var (csType, _) = ResolveFieldType(f.FieldType);
            return $"{csType} {SafePropName(ToPascalCase(f.Name), s.Name)}";
        }));

        sb.AppendLine($"public readonly record struct {s.Name}({paramList}) : ILoggable<{s.Name}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public static ComponentDescriptor Descriptor => new(null, null, \"{fqn}\");");
        sb.AppendLine();
        sb.AppendLine($"    public static IArrowArray ToArrow(ReadOnlySpan<{s.Name}> data)");
        sb.AppendLine("    {");

        // For multi-field structs, emit FixedSizeList of the flattened primitives
        if (s.Fields.Count == 1 && s.Fields[0].FieldType is FbsFixedArray fa && fa.ElementType is FbsPrimitive fp)
            EmitFixedArrayToArrow(sb, s, fa, fp);
        else
            EmitStructFieldsToArrow(sb, s);

        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private void EmitTransparentToArrow(StringBuilder sb, FbsField field, string typeName)
    {
        var propName = SafePropName(ToPascalCase(field.Name), typeName);
        switch (field.FieldType)
        {
            // Reference to a skipped primitive wrapper — emit primitive Arrow directly
            case FbsReference r when PrimitiveWrapperTypes.TryGetValue(r.FullyQualifiedName, out var skippedPrim):
                var skippedBuilder = MapPrimitiveToArrowBuilder(skippedPrim);
                sb.AppendLine($"        var builder = new {skippedBuilder}();");
                sb.AppendLine($"        foreach (var v in data)");
                sb.AppendLine($"            builder.Append(v.{propName});");
                sb.AppendLine($"        return builder.Build();");
                break;

            case FbsPrimitive p when p.TypeName == "string":
                // Build StringArray from raw buffers — CArrowArrayExporter can't export
                // StringArray built with StringArray.Builder (Apache.Arrow limitation).
                sb.AppendLine($"        var enc = System.Text.Encoding.UTF8;");
                sb.AppendLine($"        var offsets = new int[data.Length + 1];");
                sb.AppendLine($"        var total = 0;");
                sb.AppendLine($"        for (var i = 0; i < data.Length; i++) {{ offsets[i] = total; total += enc.GetByteCount(data[i].{propName} ?? \"\"); }}");
                sb.AppendLine($"        offsets[data.Length] = total;");
                sb.AppendLine($"        var valueBytes = new byte[total];");
                sb.AppendLine($"        var pos = 0;");
                sb.AppendLine($"        for (var i = 0; i < data.Length; i++) {{ var s = data[i].{propName} ?? \"\"; enc.GetBytes(s, 0, s.Length, valueBytes, pos); pos += enc.GetByteCount(s); }}");
                sb.AppendLine($"        var offsetBytes = new byte[offsets.Length * 4];");
                sb.AppendLine($"        System.Buffer.BlockCopy(offsets, 0, offsetBytes, 0, offsetBytes.Length);");
                sb.AppendLine($"        var arrayData = new ArrayData(StringType.Default, data.Length, 0, 0,");
                sb.AppendLine($"            new[] {{ ArrowBuffer.Empty, new ArrowBuffer(offsetBytes), new ArrowBuffer(valueBytes) }});");
                sb.AppendLine($"        return new StringArray(arrayData);");
                break;

            case FbsPrimitive p:
                var builder = MapPrimitiveToArrowBuilder(p.TypeName);
                sb.AppendLine($"        var builder = new {builder}();");
                sb.AppendLine($"        foreach (var v in data)");
                sb.AppendLine($"            builder.Append(v.{propName});");
                sb.AppendLine($"        return builder.Build();");
                break;

            case FbsFixedArray fa when fa.ElementType is FbsPrimitive fp:
                EmitFixedArrayFieldToArrow(sb, field, fa, fp, typeName);
                break;

            case FbsReference r:
                var shortName = GetShortName(r.FullyQualifiedName);
                sb.AppendLine($"        var inner = new {shortName}[data.Length];");
                sb.AppendLine($"        for (var i = 0; i < data.Length; i++)");
                sb.AppendLine($"            inner[i] = data[i].{propName};");
                sb.AppendLine($"        return {shortName}.ToArrow(inner);");
                break;

            default:
                sb.AppendLine($"        throw new NotImplementedException(); // {field.FieldType}");
                break;
        }
    }

    private void EmitFixedArrayFieldToArrow(StringBuilder sb, FbsField field, FbsFixedArray fa, FbsPrimitive fp, string typeName)
    {
        var csElemType = MapPrimitiveToCSharp(fp.TypeName);
        var builder = MapPrimitiveToArrowBuilder(fp.TypeName);
        var arrowType = MapPrimitiveToArrowType(fp.TypeName);
        var propName = ToPascalCase(field.Name);

        sb.AppendLine($"        var valuesBuilder = new {builder}();");
        sb.AppendLine($"        foreach (var v in data)");
        sb.AppendLine($"            foreach (var elem in v.{propName})");
        sb.AppendLine($"                valuesBuilder.Append(elem);");
        sb.AppendLine($"        var values = valuesBuilder.Build();");
        sb.AppendLine($"        var listType = new FixedSizeListType(new Field(\"item\", new {arrowType}(), false), {fa.Size});");
        sb.AppendLine($"        var arrayData = new ArrayData(listType, data.Length, 0, 0,");
        sb.AppendLine($"            new[] {{ ArrowBuffer.Empty }}, new[] {{ values.Data }});");
        sb.AppendLine($"        return new FixedSizeListArray(arrayData);");
    }

    private void EmitFixedArrayToArrow(StringBuilder sb, FbsStruct s, FbsFixedArray fa, FbsPrimitive fp)
    {
        var field = s.Fields[0];
        EmitFixedArrayFieldToArrow(sb, field, fa, fp, s.Name);
    }

    private void EmitStructFieldsToArrow(StringBuilder sb, FbsStruct s)
    {
        // Multi-field struct: build a StructArray with child arrays for each field
        foreach (var field in s.Fields)
        {
            var propName = SafePropName(ToPascalCase(field.Name), s.Name);
            switch (field.FieldType)
            {
                case FbsPrimitive p when p.TypeName != "string":
                    var builder = MapPrimitiveToArrowBuilder(p.TypeName);
                    sb.AppendLine($"        var {field.Name}Builder = new {builder}();");
                    sb.AppendLine($"        foreach (var v in data) {field.Name}Builder.Append(v.{propName});");
                    break;
                case FbsReference r when PrimitiveWrapperTypes.TryGetValue(r.FullyQualifiedName, out var prim):
                    var primBuilder = MapPrimitiveToArrowBuilder(prim);
                    sb.AppendLine($"        var {field.Name}Builder = new {primBuilder}();");
                    sb.AppendLine($"        foreach (var v in data) {field.Name}Builder.Append(v.{propName});");
                    break;
                default:
                    // Fallback for complex nested fields
                    sb.AppendLine($"        throw new NotImplementedException(\"Complex field: {field.Name}\");");
                    return;
            }
        }
        sb.AppendLine($"        var fields = new Apache.Arrow.Field[] {{");
        foreach (var field in s.Fields)
        {
            var arrowType = field.FieldType switch
            {
                FbsPrimitive p => MapPrimitiveToArrowType(p.TypeName),
                FbsReference r when PrimitiveWrapperTypes.TryGetValue(r.FullyQualifiedName, out var prim)
                    => MapPrimitiveToArrowType(prim),
                _ => "FloatType" // fallback
            };
            sb.AppendLine($"            new Apache.Arrow.Field(\"{field.Name}\", new {arrowType}(), false),");
        }
        sb.AppendLine($"        }};");
        sb.AppendLine($"        var structType = new Apache.Arrow.Types.StructType(fields);");
        var childArrays = string.Join(", ", s.Fields.Select(f => $"{f.Name}Builder.Build()"));
        sb.AppendLine($"        return new StructArray(structType, data.Length, new IArrowArray[] {{ {childArrays} }}, ArrowBuffer.Empty);");
    }

    private string EmitTable(FbsTable t)
    {
        var sb = new StringBuilder();
        WriteHeader(sb);
        sb.AppendLine("using Apache.Arrow;");
        sb.AppendLine("using Apache.Arrow.Types;");
        if (t.Fields.Count > 1)
            sb.AppendLine("using System.Linq;");
        sb.AppendLine();
        sb.AppendLine($"namespace Rerun.Net.Datatypes;");
        sb.AppendLine();
        WriteDocComment(sb, t.DocComment);

        var fqn = $"{t.Namespace}.{t.Name}";
        // Use partial for multi-field tables (allows .ext.cs extensions)
        var partialKw = t.Fields.Count > 1 ? "partial " : "";
        if (t.Fields.Count == 1)
        {
            var field0 = t.Fields[0];
            var (csType0, _) = ResolveFieldType(field0.FieldType);
            var propName0 = SafePropName(ToPascalCase(field0.Name), t.Name);
            sb.AppendLine($"public readonly {partialKw}record struct {t.Name}({csType0} {propName0}) : ILoggable<{t.Name}>");
        }
        else
        {
            var paramList = string.Join(", ", t.Fields.Select(f =>
            {
                var (ct, _) = ResolveFieldType(f.FieldType);
                return $"{ct} {SafePropName(ToPascalCase(f.Name), t.Name)}";
            }));
            sb.AppendLine($"public readonly {partialKw}record struct {t.Name}({paramList}) : ILoggable<{t.Name}>");
        }

        var field = t.Fields[0];
        sb.AppendLine("{");
        sb.AppendLine($"    public static ComponentDescriptor Descriptor => new(null, null, \"{fqn}\");");
        sb.AppendLine();
        sb.AppendLine($"    public static IArrowArray ToArrow(ReadOnlySpan<{t.Name}> data)");
        sb.AppendLine("    {");

        if (t.Fields.Count == 1)
        {
            EmitTransparentToArrow(sb, field, t.Name);
        }
        else
        {
            // Multi-field table: build each field as a child array, then combine into StructArray
            foreach (var f in t.Fields)
            {
                var fPropName = SafePropName(ToPascalCase(f.Name), t.Name);
                switch (f.FieldType)
                {
                    case FbsReference r when PrimitiveWrapperTypes.TryGetValue(r.FullyQualifiedName, out var prim):
                        var primBuilder = MapPrimitiveToArrowBuilder(prim);
                        sb.AppendLine($"        var {f.Name}Builder = new {primBuilder}();");
                        sb.AppendLine($"        foreach (var v in data) {f.Name}Builder.Append(v.{fPropName});");
                        sb.AppendLine($"        var {f.Name}Array = (IArrowArray){f.Name}Builder.Build();");
                        break;
                    case FbsReference r:
                        var innerName = GetShortName(r.FullyQualifiedName);
                        sb.AppendLine($"        var {f.Name}Tmp = new {innerName}[data.Length];");
                        sb.AppendLine($"        for (var i = 0; i < data.Length; i++) {f.Name}Tmp[i] = data[i].{fPropName};");
                        sb.AppendLine($"        var {f.Name}Array = {innerName}.ToArrow({f.Name}Tmp);");
                        break;
                    case FbsPrimitive p when p.TypeName == "string":
                        sb.AppendLine($"        var {f.Name}Builder = new StringArray.Builder();");
                        sb.AppendLine($"        foreach (var v in data) {f.Name}Builder.Append(v.{fPropName});");
                        sb.AppendLine($"        var {f.Name}Array = (IArrowArray){f.Name}Builder.Build();");
                        break;
                    case FbsPrimitive p:
                        var builder = MapPrimitiveToArrowBuilder(p.TypeName);
                        sb.AppendLine($"        var {f.Name}Builder = new {builder}();");
                        sb.AppendLine($"        foreach (var v in data) {f.Name}Builder.Append(v.{fPropName});");
                        sb.AppendLine($"        var {f.Name}Array = (IArrowArray){f.Name}Builder.Build();");
                        break;
                    default:
                        sb.AppendLine($"        IArrowArray {f.Name}Array = null!; // unsupported field type");
                        break;
                }
            }
            sb.AppendLine($"        var fields = new Apache.Arrow.Field[] {{");
            foreach (var f in t.Fields)
                sb.AppendLine($"            new(\"{f.Name}\", {f.Name}Array.Data.DataType, false),");
            sb.AppendLine($"        }};");
            sb.AppendLine($"        var structType = new Apache.Arrow.Types.StructType(fields);");
            var childArrays = string.Join(", ", t.Fields.Select(f => $"{f.Name}Array"));
            sb.AppendLine($"        return new StructArray(structType, data.Length, new IArrowArray[] {{ {childArrays} }}, ArrowBuffer.Empty);");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string EmitEnum(FbsEnum e)
    {
        var sb = new StringBuilder();
        WriteHeader(sb);
        sb.AppendLine($"namespace Rerun.Net.Datatypes;");
        sb.AppendLine();
        WriteDocComment(sb, e.DocComment);
        sb.AppendLine($"public enum {e.Name} : {MapPrimitiveToCSharp(e.UnderlyingType)}");
        sb.AppendLine("{");
        foreach (var v in e.Values)
        {
            if (v.Value.HasValue)
                sb.AppendLine($"    {v.Name} = {v.Value.Value},");
            else
                sb.AppendLine($"    {v.Name},");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private string EmitUnion(FbsUnion u)
    {
        var sb = new StringBuilder();
        WriteHeader(sb);
        sb.AppendLine("using Apache.Arrow;");
        sb.AppendLine("using Apache.Arrow.Types;");
        sb.AppendLine();
        sb.AppendLine($"namespace Rerun.Net.Datatypes;");
        sb.AppendLine();

        var fqn = $"{u.Namespace}.{u.Name}";

        // Tag enum
        sb.AppendLine($"public enum {u.Name}Tag : byte");
        sb.AppendLine("{");
        for (var i = 0; i < u.Variants.Count; i++)
            sb.AppendLine($"    {u.Variants[i].Name} = {i},");
        sb.AppendLine("}");
        sb.AppendLine();

        // Union struct — holds tag + raw data as object (the variant's inner value)
        WriteDocComment(sb, u.DocComment);
        sb.AppendLine($"public readonly record struct {u.Name}({u.Name}Tag Tag, object? Data) : ILoggable<{u.Name}>");
        sb.AppendLine("{");

        // Factory methods for each variant
        foreach (var v in u.Variants)
        {
            WriteDocComment(sb, v.DocComment, "    ");
            sb.AppendLine($"    public static {u.Name} {v.Name}(object? data = null) => new({u.Name}Tag.{v.Name}, data);");
        }
        sb.AppendLine();

        sb.AppendLine($"    public static ComponentDescriptor Descriptor => new(null, null, \"{fqn}\");");
        sb.AppendLine();

        // ToArrow — for now, serialize the tag as a uint8 array (simplified)
        // Full DenseUnion support can be added later
        sb.AppendLine($"    public static IArrowArray ToArrow(ReadOnlySpan<{u.Name}> data)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var builder = new UInt8Array.Builder();");
        sb.AppendLine($"        foreach (var v in data)");
        sb.AppendLine($"            builder.Append((byte)v.Tag);");
        sb.AppendLine($"        return builder.Build();");
        sb.AppendLine("    }");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static (string CsType, bool IsArray) ResolveFieldType(FbsFieldType ft)
    {
        return ft switch
        {
            FbsPrimitive p => (MapPrimitiveToCSharp(p.TypeName), false),
            FbsReference r when PrimitiveWrapperTypes.TryGetValue(r.FullyQualifiedName, out var prim)
                => (MapPrimitiveToCSharp(prim), false),
            FbsReference r => (GetShortName(r.FullyQualifiedName), false),
            FbsArray a => ($"{ResolveFieldType(a.ElementType).CsType}[]", true),
            FbsFixedArray fa => ($"{ResolveFieldType(fa.ElementType).CsType}[]", true),
            _ => throw new ArgumentException($"Unknown field type: {ft}")
        };
    }
}
