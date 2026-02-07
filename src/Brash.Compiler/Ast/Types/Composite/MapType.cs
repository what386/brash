namespace Brash.Compiler.Ast.Types;

public class MapType : TypeNode
{
    public TypeNode KeyType { get; set; } = null!;
    public TypeNode ValueType { get; set; } = null!;

    public override string ToString() => $"map<{KeyType}, {ValueType}>";

    public override bool Equals(object? obj)
    {
        return obj is MapType other
               && KeyType.Equals(other.KeyType)
               && ValueType.Equals(other.ValueType);
    }

    public override int GetHashCode() => HashCode.Combine(KeyType, ValueType);
}
