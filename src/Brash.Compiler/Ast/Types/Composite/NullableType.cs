namespace Brash.Compiler.Ast.Types.Composite;

public class NullableType : TypeNode
{
    public TypeNode BaseType { get; set; } = null!;

    public override string ToString() => $"{BaseType}?";

    public override bool Equals(object? obj)
    {
        return obj is NullableType other && BaseType.Equals(other.BaseType);
    }

    public override int GetHashCode() => HashCode.Combine(BaseType, "?");
}
