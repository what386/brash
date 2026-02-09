namespace Brash.Compiler.Ast.Types.Primitive;

public sealed class CharType : PrimitiveType
{
    public CharType()
    {
        PrimitiveKind = Kind.Char;
    }
}
