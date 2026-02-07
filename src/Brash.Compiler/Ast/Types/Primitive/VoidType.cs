namespace Brash.Compiler.Ast.Types;

public sealed class VoidType : PrimitiveType
{
    public VoidType()
    {
        PrimitiveKind = Kind.Void;
    }
}
