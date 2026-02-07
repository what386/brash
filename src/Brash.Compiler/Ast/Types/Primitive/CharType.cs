namespace Brash.Compiler.Ast.Types;

public sealed class CharType : PrimitiveType
{
    public CharType()
    {
        PrimitiveKind = Kind.Char;
    }
}
