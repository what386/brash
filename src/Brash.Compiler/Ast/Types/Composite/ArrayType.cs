namespace Brash.Compiler.Ast.Types.Composite;

public class ArrayType : TypeNode
{
    public TypeNode ElementType { get; set; } = null!;

    public override string ToString() => $"{ElementType}[]";

    public override bool Equals(object? obj)
    {
        return obj is ArrayType other && ElementType.Equals(other.ElementType);
    }

    public override int GetHashCode() => HashCode.Combine(ElementType, "[]");
}
