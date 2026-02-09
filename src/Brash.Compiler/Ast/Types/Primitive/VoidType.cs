namespace Brash.Compiler.Ast.Types.Primitive;

public sealed class VoidType : PrimitiveType
{
    public VoidType()
    {
        PrimitiveKind = Kind.Void;
    }
}
