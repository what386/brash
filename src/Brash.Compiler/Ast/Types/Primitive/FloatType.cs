namespace Brash.Compiler.Ast.Types.Primitive;

public sealed class FloatType : PrimitiveType
{
    public FloatType()
    {
        PrimitiveKind = Kind.Float;
    }
}
