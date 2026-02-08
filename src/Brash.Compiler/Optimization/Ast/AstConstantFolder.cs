namespace Brash.Compiler.Optimization.Ast;

using System.Globalization;
using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Types;

internal sealed class AstConstantFolder
{
    public bool TryFoldExpression(Expression expression, out Expression folded)
    {
        switch (expression)
        {
            case ParenthesizedExpression parenthesized:
                folded = parenthesized.Expression;
                return true;

            case NullCoalesceExpression nullCoalesce when nullCoalesce.Left is NullLiteral:
                folded = nullCoalesce.Right;
                return true;

            case NullCoalesceExpression nullCoalesce when nullCoalesce.Left is LiteralExpression:
                folded = nullCoalesce.Left;
                return true;

            case UnaryExpression unary when unary.Operand is LiteralExpression operand:
                return TryFoldUnary(unary, operand, out folded);

            case BinaryExpression binary when binary.Left is LiteralExpression left && binary.Right is LiteralExpression right:
                return TryFoldBinary(binary, left, right, out folded);

            case CastExpression cast when cast.Value is LiteralExpression literal:
                return TryFoldCast(cast, literal, out folded);

            default:
                folded = expression;
                return false;
        }
    }

    public bool TryEvaluateCondition(Expression expression, out bool value)
    {
        value = false;
        return expression is LiteralExpression literal && TryGetConditionValue(literal, out value);
    }

    private bool TryFoldUnary(UnaryExpression unary, LiteralExpression operand, out Expression folded)
    {
        folded = unary;
        switch (unary.Operator)
        {
            case "-" when TryGetDouble(operand.Value, out var doubleValue):
                folded = AstLiteralFactory.CreateNumberLiteral(unary, -doubleValue, operand.Type);
                return true;

            case "+" when TryGetDouble(operand.Value, out var unaryPlus):
                folded = AstLiteralFactory.CreateNumberLiteral(unary, unaryPlus, operand.Type);
                return true;

            case "!" when TryGetConditionValue(operand, out var condition):
                folded = AstLiteralFactory.BoolLiteral(!condition, unary.Line, unary.Column);
                return true;

            default:
                return false;
        }
    }

    private bool TryFoldBinary(BinaryExpression binary, LiteralExpression left, LiteralExpression right, out Expression folded)
    {
        folded = binary;

        switch (binary.Operator)
        {
            case "+":
                if (TryFoldStringConcatenation(binary, left, right, out folded))
                    return true;

                if (TryGetDouble(left.Value, out var addLeft) && TryGetDouble(right.Value, out var addRight))
                {
                    folded = AstLiteralFactory.CreateNumberLiteral(binary, addLeft + addRight, left.Type, right.Type);
                    return true;
                }
                return false;

            case "-" when TryGetDouble(left.Value, out var subLeft) && TryGetDouble(right.Value, out var subRight):
                folded = AstLiteralFactory.CreateNumberLiteral(binary, subLeft - subRight, left.Type, right.Type);
                return true;

            case "*" when TryGetDouble(left.Value, out var mulLeft) && TryGetDouble(right.Value, out var mulRight):
                folded = AstLiteralFactory.CreateNumberLiteral(binary, mulLeft * mulRight, left.Type, right.Type);
                return true;

            case "/" when TryGetDouble(left.Value, out var divLeft) && TryGetDouble(right.Value, out var divRight) && Math.Abs(divRight) > double.Epsilon:
                folded = AstLiteralFactory.CreateNumberLiteral(binary, divLeft / divRight, left.Type, right.Type);
                return true;

            case "%" when TryGetLong(left.Value, out var modLeft) && TryGetLong(right.Value, out var modRight) && modRight != 0:
                folded = AstLiteralFactory.IntLiteral(modLeft % modRight, binary.Line, binary.Column);
                return true;

            case "==":
                folded = AstLiteralFactory.BoolLiteral(Equals(left.Value, right.Value), binary.Line, binary.Column);
                return true;

            case "!=":
                folded = AstLiteralFactory.BoolLiteral(!Equals(left.Value, right.Value), binary.Line, binary.Column);
                return true;

            case "<" when TryCompareLiterals(left.Value, right.Value, out var lessCmp):
                folded = AstLiteralFactory.BoolLiteral(lessCmp < 0, binary.Line, binary.Column);
                return true;

            case ">" when TryCompareLiterals(left.Value, right.Value, out var greaterCmp):
                folded = AstLiteralFactory.BoolLiteral(greaterCmp > 0, binary.Line, binary.Column);
                return true;

            case "<=" when TryCompareLiterals(left.Value, right.Value, out var lessEqCmp):
                folded = AstLiteralFactory.BoolLiteral(lessEqCmp <= 0, binary.Line, binary.Column);
                return true;

            case ">=" when TryCompareLiterals(left.Value, right.Value, out var greaterEqCmp):
                folded = AstLiteralFactory.BoolLiteral(greaterEqCmp >= 0, binary.Line, binary.Column);
                return true;

            case "&&" when TryGetConditionValue(left, out var andLeft) && TryGetConditionValue(right, out var andRight):
                folded = AstLiteralFactory.BoolLiteral(andLeft && andRight, binary.Line, binary.Column);
                return true;

            case "||" when TryGetConditionValue(left, out var orLeft) && TryGetConditionValue(right, out var orRight):
                folded = AstLiteralFactory.BoolLiteral(orLeft || orRight, binary.Line, binary.Column);
                return true;

            default:
                return false;
        }
    }

    private bool TryFoldCast(CastExpression cast, LiteralExpression literal, out Expression folded)
    {
        folded = cast;
        if (cast.TargetType is not PrimitiveType target)
            return false;

        switch (target.PrimitiveKind)
        {
            case PrimitiveType.Kind.String:
                folded = AstLiteralFactory.StringLiteral(Convert.ToString(literal.Value, CultureInfo.InvariantCulture) ?? string.Empty, cast.Line, cast.Column);
                return true;

            case PrimitiveType.Kind.Int when TryGetLong(literal.Value, out var intValue):
                folded = AstLiteralFactory.IntLiteral(intValue, cast.Line, cast.Column);
                return true;

            case PrimitiveType.Kind.Float when TryGetDouble(literal.Value, out var floatValue):
                folded = AstLiteralFactory.FloatLiteral(floatValue, cast.Line, cast.Column);
                return true;

            case PrimitiveType.Kind.Bool when TryGetConditionValue(literal, out var boolValue):
                folded = AstLiteralFactory.BoolLiteral(boolValue, cast.Line, cast.Column);
                return true;

            case PrimitiveType.Kind.Char:
                var asString = Convert.ToString(literal.Value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(asString))
                {
                    folded = AstLiteralFactory.CharLiteral(asString[0], cast.Line, cast.Column);
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    private static bool TryFoldStringConcatenation(BinaryExpression binary, LiteralExpression left, LiteralExpression right, out Expression folded)
    {
        folded = binary;
        if (!IsStringLikeLiteral(left) && !IsStringLikeLiteral(right))
            return false;

        if (!TryGetConcatenationString(left.Value, out var leftString) || !TryGetConcatenationString(right.Value, out var rightString))
            return false;

        folded = AstLiteralFactory.StringLiteral(leftString + rightString, binary.Line, binary.Column);
        return true;
    }

    private static bool IsStringLikeLiteral(LiteralExpression literal)
    {
        if (literal.Value is string or char)
            return true;

        return literal.Type is PrimitiveType primitive &&
            primitive.PrimitiveKind is PrimitiveType.Kind.String or PrimitiveType.Kind.Char;
    }

    private static bool TryGetConditionValue(LiteralExpression literal, out bool value)
    {
        value = false;
        if (literal.Value is bool b)
        {
            value = b;
            return true;
        }

        if (TryGetLong(literal.Value, out var numeric))
        {
            value = numeric != 0;
            return true;
        }

        return false;
    }

    private static bool TryGetLong(object? value, out long numeric)
    {
        switch (value)
        {
            case sbyte v: numeric = v; return true;
            case byte v: numeric = v; return true;
            case short v: numeric = v; return true;
            case ushort v: numeric = v; return true;
            case int v: numeric = v; return true;
            case uint v: numeric = v; return true;
            case long v: numeric = v; return true;
            case ulong v when v <= long.MaxValue: numeric = (long)v; return true;
            default:
                numeric = 0;
                return false;
        }
    }

    private static bool TryGetDouble(object? value, out double numeric)
    {
        switch (value)
        {
            case float v: numeric = v; return true;
            case double v: numeric = v; return true;
            case decimal v: numeric = (double)v; return true;
            default:
                if (TryGetLong(value, out var longValue))
                {
                    numeric = longValue;
                    return true;
                }
                numeric = 0;
                return false;
        }
    }

    private static bool TryGetStringValue(object? value, out string text)
    {
        switch (value)
        {
            case string s:
                text = s;
                return true;
            case char c:
                text = c.ToString();
                return true;
            default:
                text = string.Empty;
                return false;
        }
    }

    private static bool TryGetConcatenationString(object? value, out string text)
    {
        if (TryGetStringValue(value, out text))
            return true;

        if (value is bool b)
        {
            text = b ? "true" : "false";
            return true;
        }

        if (TryGetDouble(value, out var number))
        {
            text = number % 1 == 0
                ? Convert.ToString((long)number, CultureInfo.InvariantCulture) ?? "0"
                : number.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static bool TryCompareLiterals(object? left, object? right, out int comparison)
    {
        comparison = 0;
        if (TryGetDouble(left, out var leftNumber) && TryGetDouble(right, out var rightNumber))
        {
            comparison = leftNumber.CompareTo(rightNumber);
            return true;
        }

        if (TryGetStringValue(left, out var leftString) && TryGetStringValue(right, out var rightString))
        {
            comparison = string.Compare(leftString, rightString, StringComparison.Ordinal);
            return true;
        }

        return false;
    }
}
