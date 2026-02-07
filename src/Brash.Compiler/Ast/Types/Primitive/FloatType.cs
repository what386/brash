namespace Brash.Compiler.Ast.Types;

public sealed class FloatType : PrimitiveType
{
    public FloatType()
    {
        PrimitiveKind = Kind.Float;
    }
}
