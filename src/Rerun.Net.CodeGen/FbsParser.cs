using System.Text.RegularExpressions;

namespace Rerun.Net.CodeGen;

// --- Model ---

internal record FbsFile(string Namespace, List<FbsType> Types);

internal abstract record FbsType(string Name, string Namespace, Dictionary<string, string?> Attributes, string? DocComment);

internal record FbsStruct(string Name, string Namespace, List<FbsField> Fields,
    Dictionary<string, string?> Attributes, string? DocComment) : FbsType(Name, Namespace, Attributes, DocComment);

internal record FbsTable(string Name, string Namespace, List<FbsField> Fields,
    Dictionary<string, string?> Attributes, string? DocComment) : FbsType(Name, Namespace, Attributes, DocComment);

internal record FbsEnum(string Name, string Namespace, string UnderlyingType, List<FbsEnumValue> Values,
    Dictionary<string, string?> Attributes, string? DocComment) : FbsType(Name, Namespace, Attributes, DocComment);

internal record FbsUnion(string Name, string Namespace, List<FbsUnionVariant> Variants,
    Dictionary<string, string?> Attributes, string? DocComment) : FbsType(Name, Namespace, Attributes, DocComment);

internal record FbsUnionVariant(string Name, string TypeRef, string? DocComment);

internal record FbsEnumValue(string Name, int? Value, bool IsDefault);

internal record FbsField(string Name, FbsFieldType FieldType, Dictionary<string, string?> Attributes, bool Nullable, int Order, string? DocComment);

internal abstract record FbsFieldType;
internal record FbsPrimitive(string TypeName) : FbsFieldType; // float, uint, bool, string, etc.
internal record FbsReference(string FullyQualifiedName) : FbsFieldType; // rerun.datatypes.Vec3D
internal record FbsArray(FbsFieldType ElementType) : FbsFieldType; // [type]
internal record FbsFixedArray(FbsFieldType ElementType, int Size) : FbsFieldType; // [type: N]

// --- Parser ---

internal class FbsParser
{
    private static readonly HashSet<string> Primitives = new()
    {
        "bool", "byte", "ubyte", "short", "ushort", "int", "uint",
        "long", "ulong", "float", "float32", "float64", "double", "string",
        "int8", "uint8", "int16", "uint16", "int32", "uint32", "int64", "uint64"
    };

    public FbsFile Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var ns = "";
        var types = new List<FbsType>();
        var docBuffer = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Collect doc comments
            if (line.StartsWith("///"))
            {
                var comment = line.Length > 3 ? line[3..].TrimStart() : "";
                // Skip language-specific doc lines
                if (!comment.StartsWith("\\py") && !comment.StartsWith("\\cpp") &&
                    !comment.StartsWith("\\rs") && !comment.StartsWith("\\example"))
                    docBuffer.Add(comment);
                continue;
            }

            if (line.StartsWith("//") || line == "") { docBuffer.Clear(); continue; }
            if (line.StartsWith("include ")) { docBuffer.Clear(); continue; }

            // Namespace
            if (line.StartsWith("namespace "))
            {
                ns = line["namespace ".Length..].TrimEnd(';').Trim();
                docBuffer.Clear();
                continue;
            }

            // Struct/Table/Enum — may span multiple lines, collect until closing brace
            if (line.StartsWith("struct ") || line.StartsWith("table ") || line.StartsWith("enum ") || line.StartsWith("union "))
            {
                var doc = docBuffer.Count > 0 ? string.Join("\n", docBuffer) : null;
                docBuffer.Clear();

                var block = CollectBlock(lines, ref i);
                var type = ParseTypeBlock(block, ns, doc);
                if (type != null)
                    types.Add(type);
                continue;
            }

            docBuffer.Clear();
        }

        return new FbsFile(ns, types);
    }

    private static string CollectBlock(string[] lines, ref int i)
    {
        var sb = new System.Text.StringBuilder();
        var braceDepth = 0;
        var started = false;

        for (; i < lines.Length; i++)
        {
            var line = lines[i];
            sb.AppendLine(line);

            foreach (var ch in line)
            {
                if (ch == '{') { braceDepth++; started = true; }
                if (ch == '}') braceDepth--;
            }

            if (started && braceDepth <= 0)
                break;
        }

        return sb.ToString();
    }

    private FbsType? ParseTypeBlock(string block, string ns, string? doc)
    {
        // Match: (struct|table|enum|union) Name [: underlying] (attrs) { body }
        // The attribute block can span multiple lines and contain single-quoted strings
        var headerMatch = Regex.Match(block,
            @"^(struct|table|enum|union)\s+(\w+)\s*(?::\s*(\w+))?\s*(\([\s\S]*?\))?\s*\{",
            RegexOptions.Singleline);
        if (!headerMatch.Success) return null;

        var kind = headerMatch.Groups[1].Value;
        var name = headerMatch.Groups[2].Value;
        var underlying = headerMatch.Groups[3].Value;
        var attrsRaw = headerMatch.Groups[4].Value;
        var attrs = ParseAttributes(attrsRaw);

        var bodyStart = block.IndexOf('{') + 1;
        var bodyEnd = block.LastIndexOf('}');
        var body = block[bodyStart..bodyEnd];

        return kind switch
        {
            "struct" => new FbsStruct(name, ns, ParseFields(body), attrs, doc),
            "table" => new FbsTable(name, ns, ParseFields(body), attrs, doc),
            "enum" => new FbsEnum(name, ns, underlying, ParseEnumValues(body), attrs, doc),
            "union" => new FbsUnion(name, ns, ParseUnionVariants(body), attrs, doc),
            _ => null,
        };
    }

    private List<FbsField> ParseFields(string body)
    {
        var fields = new List<FbsField>();
        var docBuffer = new List<string>();

        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("///"))
            {
                var comment = line.Length > 3 ? line[3..].TrimStart() : "";
                if (!comment.StartsWith("\\py") && !comment.StartsWith("\\cpp") &&
                    !comment.StartsWith("\\rs"))
                    docBuffer.Add(comment);
                continue;
            }
            if (line.StartsWith("//") || line == "") { docBuffer.Clear(); continue; }

            // Match: name: type (attrs);
            var match = Regex.Match(line, @"^(\w+)\s*:\s*(.+?)\s*(?:\(([^)]*)\))?\s*;");
            if (!match.Success) { docBuffer.Clear(); continue; }

            var fieldName = match.Groups[1].Value;
            var typeStr = match.Groups[2].Value.Trim();
            var fieldAttrsRaw = match.Groups[3].Value;
            var fieldAttrs = ParseAttributes("(" + fieldAttrsRaw + ")");
            var doc = docBuffer.Count > 0 ? string.Join("\n", docBuffer) : null;
            docBuffer.Clear();

            var nullable = fieldAttrs.Remove("nullable");
            var order = 0;
            if (fieldAttrs.TryGetValue("order", out var orderStr))
            {
                int.TryParse(orderStr, out order);
                fieldAttrs.Remove("order");
            }

            var fieldType = ParseFieldType(typeStr);
            fields.Add(new FbsField(fieldName, fieldType, fieldAttrs, nullable, order, doc));
        }

        return fields.OrderBy(f => f.Order).ToList();
    }

    private FbsFieldType ParseFieldType(string typeStr)
    {
        // Fixed array: [type: N]
        var fixedMatch = Regex.Match(typeStr, @"^\[(\w+)\s*:\s*(\d+)\]$");
        if (fixedMatch.Success)
        {
            var elemType = ParseFieldType(fixedMatch.Groups[1].Value);
            return new FbsFixedArray(elemType, int.Parse(fixedMatch.Groups[2].Value));
        }

        // Dynamic array: [type]
        var arrayMatch = Regex.Match(typeStr, @"^\[(.+)\]$");
        if (arrayMatch.Success)
        {
            var elemType = ParseFieldType(arrayMatch.Groups[1].Value);
            return new FbsArray(elemType);
        }

        // Primitive
        if (Primitives.Contains(typeStr))
            return new FbsPrimitive(typeStr);

        // Qualified reference
        return new FbsReference(typeStr);
    }

    private List<FbsEnumValue> ParseEnumValues(string body)
    {
        var values = new List<FbsEnumValue>();
        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd(',');
            if (line == "" || line.StartsWith("//")) continue;

            var isDefault = line.Contains("(default)");
            line = line.Replace("(default)", "").Trim();

            var parts = line.Split('=', 2);
            var name = parts[0].Trim();
            int? value = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var v) ? v : null;

            if (name.Length > 0)
                values.Add(new FbsEnumValue(name, value, isDefault));
        }
        return values;
    }

    private List<FbsUnionVariant> ParseUnionVariants(string body)
    {
        var variants = new List<FbsUnionVariant>();
        var docBuffer = new List<string>();
        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd(',');
            if (line.StartsWith("///"))
            {
                docBuffer.Add(line.Length > 3 ? line[3..].TrimStart() : "");
                continue;
            }
            if (line == "" || line.StartsWith("//")) { docBuffer.Clear(); continue; }

            // Match: Name: TypeRef [(attrs)]
            var match = Regex.Match(line, @"^(\w+)\s*:\s*([\w.]+)\s*(?:\(.*\))?$");
            if (!match.Success) { docBuffer.Clear(); continue; }

            var name = match.Groups[1].Value;
            var typeRef = match.Groups[2].Value;
            var doc = docBuffer.Count > 0 ? string.Join("\n", docBuffer) : null;
            docBuffer.Clear();

            variants.Add(new FbsUnionVariant(name, typeRef, doc));
        }
        return variants;
    }

    private static Dictionary<string, string?> ParseAttributes(string raw)
    {
        var attrs = new Dictionary<string, string?>();
        if (string.IsNullOrWhiteSpace(raw)) return attrs;

        // Strip outer parens
        raw = raw.Trim();
        if (raw.StartsWith("(")) raw = raw[1..];
        if (raw.EndsWith(")")) raw = raw[..^1];

        // Split on commas, respecting quoted strings
        foreach (var token in SplitAttributes(raw))
        {
            var t = token.Trim();
            if (t == "") continue;

            // Quoted attribute: "attr.name" or "attr.name": "value"
            if (t.StartsWith("\""))
            {
                var colonIdx = t.IndexOf("\":");
                if (colonIdx > 0)
                {
                    var key = t[1..colonIdx];
                    var val = t[(colonIdx + 2)..].Trim().Trim('"');
                    attrs[key] = val;
                }
                else
                {
                    var key = t.Trim('"');
                    attrs[key] = null;
                }
            }
            // key: value
            else if (t.Contains(':'))
            {
                var parts = t.Split(':', 2);
                attrs[parts[0].Trim()] = parts[1].Trim().Trim('"');
            }
            // bare keyword (e.g., nullable, transparent)
            else
            {
                attrs[t] = null;
            }
        }

        return attrs;
    }

    private static IEnumerable<string> SplitAttributes(string raw)
    {
        var depth = 0;
        var start = 0;
        var inDoubleQuote = false;
        var inSingleQuote = false;

        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (ch == '"' && !inSingleQuote) inDoubleQuote = !inDoubleQuote;
            if (ch == '\'' && !inDoubleQuote) inSingleQuote = !inSingleQuote;
            if (!inDoubleQuote && !inSingleQuote)
            {
                if (ch == '(') depth++;
                if (ch == ')') depth--;
                if (ch == ',' && depth == 0)
                {
                    yield return raw[start..i];
                    start = i + 1;
                }
            }
        }

        if (start < raw.Length)
            yield return raw[start..];
    }
}

// --- Type Registry (cross-file resolution) ---

internal class TypeRegistry
{
    private readonly Dictionary<string, FbsType> _types = new();

    public void Register(FbsFile file)
    {
        foreach (var type in file.Types)
        {
            var fqn = $"{type.Namespace}.{type.Name}";
            _types[fqn] = type;
        }
    }

    public FbsType? Resolve(string fullyQualifiedName)
        => _types.TryGetValue(fullyQualifiedName, out var t) ? t : null;

    public IEnumerable<T> GetAll<T>() where T : FbsType
        => _types.Values.OfType<T>();

    public IEnumerable<FbsType> GetByNamespace(string ns)
        => _types.Values.Where(t => t.Namespace == ns);
}
