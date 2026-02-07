namespace Brash.Compiler.Ast.Types;

public sealed class IntType : PrimitiveType
{
    public IntType()
    {
        PrimitiveKind = Kind.Int;
    }
}
