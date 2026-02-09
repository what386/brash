namespace Brash.Compiler.Ast.Types.Composite;

public class NamedType : TypeNode
{
    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;

    public override bool Equals(object? obj)
    {
        return obj is NamedType other && Name == other.Name;
    }

    public override int GetHashCode() => Name.GetHashCode();
}
