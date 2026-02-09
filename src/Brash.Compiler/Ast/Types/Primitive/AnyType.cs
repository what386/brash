namespace Brash.Compiler.Ast.Types.Primitive;

public sealed class AnyType : PrimitiveType
{
    public AnyType()
    {
        PrimitiveKind = Kind.Any;
    }
}
