namespace Brash.Compiler.Optimization.Ast;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Types;

internal static class AstLiteralFactory
{
    public static LiteralExpression CloneLiteral(LiteralExpression source)
    {
        return new LiteralExpression
        {
            Line = source.Line,
            Column = source.Column,
            Value = source.Value,
            Type = source.Type,
            IsInterpolated = source.IsInterpolated,
            IsMultiline = source.IsMultiline
        };
    }

    public static LiteralExpression CreateNumberLiteral(
        AstNode source,
        double value,
        TypeNode preferredLeftType,
        TypeNode? preferredRightType = null)
    {
        bool shouldBeInt = preferredLeftType is PrimitiveType leftPrim &&
            leftPrim.PrimitiveKind == PrimitiveType.Kind.Int &&
            (preferredRightType is null || preferredRightType is PrimitiveType rightPrim && rightPrim.PrimitiveKind == PrimitiveType.Kind.Int);

        if (shouldBeInt && Math.Abs(value - Math.Round(value)) < double.Epsilon)
            return IntLiteral((long)Math.Round(value), source.Line, source.Column);

        return FloatLiteral(value, source.Line, source.Column);
    }

    public static LiteralExpression IntLiteral(long value, int line, int column)
    {
        object boxedValue = value >= int.MinValue && value <= int.MaxValue
            ? (object)(int)value
            : value;

        return new LiteralExpression
        {
            Line = line,
            Column = column,
            Value = boxedValue,
            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int }
        };
    }

    public static LiteralExpression FloatLiteral(double value, int line, int column)
    {
        return new LiteralExpression
        {
            Line = line,
            Column = column,
            Value = value,
            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Float }
        };
    }

    public static LiteralExpression BoolLiteral(bool value, int line, int column)
    {
        return new LiteralExpression
        {
            Line = line,
            Column = column,
            Value = value,
            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Bool }
        };
    }

    public static LiteralExpression StringLiteral(string value, int line, int column)
    {
        return new LiteralExpression
        {
            Line = line,
            Column = column,
            Value = value,
            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
        };
    }

    public static LiteralExpression CharLiteral(char value, int line, int column)
    {
        return new LiteralExpression
        {
            Line = line,
            Column = column,
            Value = value,
            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Char }
        };
    }
}
