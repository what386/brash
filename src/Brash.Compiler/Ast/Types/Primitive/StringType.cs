namespace Brash.Compiler.Ast.Types;

public sealed class StringType : PrimitiveType
{
    public sealed class BuiltinMethod
    {
        public required string Name { get; init; }
        public required Kind ReturnKind { get; init; }
        public required Kind[] ParameterKinds { get; init; }
        public required Func<string, IReadOnlyList<string>, string> EmitBash { get; init; }
    }

    private static readonly Dictionary<string, BuiltinMethod> Builtins = new(StringComparer.Ordinal)
    {
        ["length"] = new BuiltinMethod
        {
            Name = "length",
            ReturnKind = Kind.Int,
            ParameterKinds = Array.Empty<Kind>(),
            EmitBash = (receiver, _) =>
                $"$(bash -lc \"printf '%s' \\\"\\${{#1}}\\\"\" _ {receiver})"
        },
        ["trim"] = new BuiltinMethod
        {
            Name = "trim",
            ReturnKind = Kind.String,
            ParameterKinds = Array.Empty<Kind>(),
            EmitBash = (receiver, _) =>
                $"$(bash -lc \"printf '%s' \\\"\\$1\\\" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'\" _ {receiver})"
        },
        ["to_upper"] = new BuiltinMethod
        {
            Name = "to_upper",
            ReturnKind = Kind.String,
            ParameterKinds = Array.Empty<Kind>(),
            EmitBash = (receiver, _) =>
                $"$(bash -lc \"printf '%s' \\\"\\$1\\\" | tr '[:lower:]' '[:upper:]'\" _ {receiver})"
        },
        ["to_lower"] = new BuiltinMethod
        {
            Name = "to_lower",
            ReturnKind = Kind.String,
            ParameterKinds = Array.Empty<Kind>(),
            EmitBash = (receiver, _) =>
                $"$(bash -lc \"printf '%s' \\\"\\$1\\\" | tr '[:upper:]' '[:lower:]'\" _ {receiver})"
        },
        ["contains"] = new BuiltinMethod
        {
            Name = "contains",
            ReturnKind = Kind.Bool,
            ParameterKinds = new[] { Kind.String },
            EmitBash = (receiver, args) =>
                $"$(bash -lc \"if printf '%s' \\\"\\$1\\\" | grep -Fq -- \\\"\\$2\\\"; then printf '1'; else printf '0'; fi\" _ {receiver} {args[0]})"
        },
        ["starts_with"] = new BuiltinMethod
        {
            Name = "starts_with",
            ReturnKind = Kind.Bool,
            ParameterKinds = new[] { Kind.String },
            EmitBash = (receiver, args) =>
                $"$(bash -lc \"if [[ \\\"\\$1\\\" == \\\"\\$2\\\"* ]]; then printf '1'; else printf '0'; fi\" _ {receiver} {args[0]})"
        },
        ["ends_with"] = new BuiltinMethod
        {
            Name = "ends_with",
            ReturnKind = Kind.Bool,
            ParameterKinds = new[] { Kind.String },
            EmitBash = (receiver, args) =>
                $"$(bash -lc \"if [[ \\\"\\$1\\\" == *\\\"\\$2\\\" ]]; then printf '1'; else printf '0'; fi\" _ {receiver} {args[0]})"
        },
        ["is_empty"] = new BuiltinMethod
        {
            Name = "is_empty",
            ReturnKind = Kind.Bool,
            ParameterKinds = Array.Empty<Kind>(),
            EmitBash = (receiver, _) =>
                $"$(bash -lc \"if [ -z \\\"\\$1\\\" ]; then printf '1'; else printf '0'; fi\" _ {receiver})"
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
