namespace Brash.Compiler.Ast.Types.Primitive;

public sealed class BoolType : PrimitiveType
{
    public BoolType()
    {
        PrimitiveKind = Kind.Bool;
    }
}
