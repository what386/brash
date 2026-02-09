namespace Brash.Compiler.Ast.Types.Primitive;

public class PrimitiveType : TypeNode
{
    public enum Kind { Int, Float, String, Bool, Char, Any, Void }

    public Kind PrimitiveKind { get; set; }

    public override string ToString() => PrimitiveKind.ToString().ToLowerInvariant();

    public override bool Equals(object? obj)
    {
        return obj is PrimitiveType other && PrimitiveKind == other.PrimitiveKind;
    }

    public override int GetHashCode() => PrimitiveKind.GetHashCode();
}
