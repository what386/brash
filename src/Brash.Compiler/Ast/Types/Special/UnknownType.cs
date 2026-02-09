namespace Brash.Compiler.Ast.Types.Special;

public class UnknownType : TypeNode
{
    public override string ToString() => "<unknown>";

    public override bool Equals(object? obj) => obj is UnknownType;

    public override int GetHashCode() => typeof(UnknownType).GetHashCode();
}
