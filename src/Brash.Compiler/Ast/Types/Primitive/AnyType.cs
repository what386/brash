namespace Brash.Compiler.Ast.Types;

public sealed class AnyType : PrimitiveType
{
    public AnyType()
    {
        PrimitiveKind = Kind.Any;
    }
}
