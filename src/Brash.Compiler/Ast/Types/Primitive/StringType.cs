namespace Brash.Compiler.Ast.Types.Primitive;

public sealed class StringType : PrimitiveType
{
    public sealed class BuiltinMethod
    {
        public required string Name { get; init; }
        public required TypeNode ReturnType { get; init; }
        public required Kind[] ParameterKinds { get; init; }
        public required Func<string, IReadOnlyList<string>, string> EmitBash { get; init; }
    }

    private static readonly Dictionary<string, BuiltinMethod> Builtins = new(StringComparer.Ordinal)
    {
        ["length"] = new BuiltinMethod
        {
            Name = "length",
            ReturnType = new PrimitiveType { PrimitiveKind = Kind.Int },
            ParameterKinds = Array.Empty<Kind>(),
            EmitBash = (receiver, _) =>
                $"$(bash -lc \"printf '%s' \\\"\\${{#1}}\\\"\" _ {receiver})"
        },
        ["trim"] = new BuiltinMethod
        {
            Name = "trim",
            ReturnType = new PrimitiveType { PrimitiveKind = Kind.String },
            ParameterKinds = Array.Empty<Kind>(),
            EmitBash = (receiver, _) =>
                $"$(bash -lc \"printf '%s' \\\"\\$1\\\" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'\" _ {receiver})"
        },
        ["to_upper"] = new BuiltinMethod
        {
            Name = "to_upper",
            ReturnType = new PrimitiveType { PrimitiveKind = Kind.String },
            ParameterKinds = Array.Empty<Kind>(),
            EmitBash = (receiver, _) =>
                $"$(bash -lc \"printf '%s' \\\"\\$1\\\" | tr '[:lower:]' '[:upper:]'\" _ {receiver})"
        },
        ["to_lower"] = new BuiltinMethod
        {
            Name = "to_lower",
            ReturnType = new PrimitiveType { PrimitiveKind = Kind.String },
            ParameterKinds = Array.Empty<Kind>(),
            EmitBash = (receiver, _) =>
                $"$(bash -lc \"printf '%s' \\\"\\$1\\\" | tr '[:upper:]' '[:lower:]'\" _ {receiver})"
        },
        ["contains"] = new BuiltinMethod
        {
            Name = "contains",
            ReturnType = new PrimitiveType { PrimitiveKind = Kind.Bool },
            ParameterKinds = new[] { Kind.String },
            EmitBash = (receiver, args) =>
                $"$(bash -lc \"if printf '%s' \\\"\\$1\\\" | grep -Fq -- \\\"\\$2\\\"; then printf '1'; else printf '0'; fi\" _ {receiver} {args[0]})"
        },
        ["starts_with"] = new BuiltinMethod
        {
            Name = "starts_with",
            ReturnType = new PrimitiveType { PrimitiveKind = Kind.Bool },
            ParameterKinds = new[] { Kind.String },
            EmitBash = (receiver, args) =>
                $"$(bash -lc \"if [[ \\\"\\$1\\\" == \\\"\\$2\\\"* ]]; then printf '1'; else printf '0'; fi\" _ {receiver} {args[0]})"
        },
        ["ends_with"] = new BuiltinMethod
        {
            Name = "ends_with",
            ReturnType = new PrimitiveType { PrimitiveKind = Kind.Bool },
            ParameterKinds = new[] { Kind.String },
            EmitBash = (receiver, args) =>
                $"$(bash -lc \"if [[ \\\"\\$1\\\" == *\\\"\\$2\\\" ]]; then printf '1'; else printf '0'; fi\" _ {receiver} {args[0]})"
        },
        ["is_empty"] = new BuiltinMethod
        {
            Name = "is_empty",
            ReturnType = new PrimitiveType { PrimitiveKind = Kind.Bool },
            ParameterKinds = Array.Empty<Kind>(),
            EmitBash = (receiver, _) =>
                $"$(bash -lc \"if [ -z \\\"\\$1\\\" ]; then printf '1'; else printf '0'; fi\" _ {receiver})"
        },
        ["split"] = new BuiltinMethod
        {
            Name = "split",
            ReturnType = new ArrayType
            {
                ElementType = new PrimitiveType { PrimitiveKind = Kind.String }
            },
            ParameterKinds = new[] { Kind.String },
            EmitBash = (receiver, args) =>
                $"$(brash_string_split {receiver} {args[0]})"
        },
        ["substring"] = new BuiltinMethod
        {
            Name = "substring",
            ReturnType = new PrimitiveType { PrimitiveKind = Kind.String },
            ParameterKinds = new[] { Kind.Int, Kind.Int },
            EmitBash = (receiver, args) =>
                $"$(brash_string_substring {receiver} {args[0]} {args[1]})"
        }
    };

    public StringType()
    {
        PrimitiveKind = Kind.String;
    }

    public static bool TryGetBuiltinMethod(string methodName, out BuiltinMethod method)
    {
        return Builtins.TryGetValue(methodName, out method!);
    }
}
