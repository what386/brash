namespace Brash.Compiler.Ast.Types.Primitive;

public sealed class IntType : PrimitiveType
{
    public IntType()
    {
        PrimitiveKind = Kind.Int;
    }
}
