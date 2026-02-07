namespace Brash.Compiler.Ast.Types;

public sealed class BoolType : PrimitiveType
{
    public BoolType()
    {
        PrimitiveKind = Kind.Bool;
    }
}
