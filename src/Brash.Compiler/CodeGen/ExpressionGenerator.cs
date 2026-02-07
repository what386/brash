namespace Brash.Compiler.CodeGen;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;

public partial class BashGenerator
{
    private string GenerateExpression(Expression expr)
    {
        return expr switch
        {
            LiteralExpression lit => GenerateLiteral(lit),
            IdentifierExpression ident => $"${{{ident.Name}}}",
            BinaryExpression bin => GenerateBinaryExpression(bin),
            UnaryExpression unary => GenerateUnaryExpression(unary),
            FunctionCallExpression call => GenerateFunctionCall(call),
            MethodCallExpression methodCall => GenerateMethodCall(methodCall),
            MemberAccessExpression member => GenerateMemberAccess(member),
            ArrayLiteral array => GenerateArrayLiteral(array),
            NullCoalesceExpression nullCoalesce => GenerateNullCoalesce(nullCoalesce),
            NullLiteral => "\"\"",
            _ => UnsupportedExpression(expr)
        };
    }

    private string GenerateLiteral(LiteralExpression lit)
    {
        if (lit.Type is PrimitiveType prim)
        {
            return prim.PrimitiveKind switch
            {
                PrimitiveType.Kind.String => $"\"{EscapeString(lit.Value.ToString() ?? "")}\"",
                PrimitiveType.Kind.Int => lit.Value.ToString() ?? "0",
                PrimitiveType.Kind.Float => lit.Value.ToString() ?? "0.0",
                PrimitiveType.Kind.Bool => lit.Value.ToString()?.ToLower() == "true" ? "0" : "1",
                PrimitiveType.Kind.Char => $"'{lit.Value}'",
                _ => "\"\""
            };
        }

        return "\"\"";
    }

    private string GenerateBinaryExpression(BinaryExpression bin)
    {
        var left = GenerateExpression(bin.Left);
        var right = GenerateExpression(bin.Right);

        return bin.Operator switch
        {
            "+" => $"$(({left} + {right}))",
            "-" => $"$(({left} - {right}))",
            "*" => $"$(({left} * {right}))",
            "/" => $"$(({left} / {right}))",
            "%" => $"$(({left} % {right}))",
            "==" => $"[ {left} -eq {right} ]",
            "!=" => $"[ {left} -ne {right} ]",
            "<" => $"[ {left} -lt {right} ]",
            ">" => $"[ {left} -gt {right} ]",
            "<=" => $"[ {left} -le {right} ]",
            ">=" => $"[ {left} -ge {right} ]",
            "&&" => $"{left} && {right}",
            "||" => $"{left} || {right}",
            _ => $"{left} {bin.Operator} {right}"
        };
    }

    private string GenerateUnaryExpression(UnaryExpression unary)
    {
        var operand = GenerateExpression(unary.Operand);
        return unary.Operator switch
        {
            "-" => $"$((-{operand}))",
            "+" => $"$((+{operand}))",
            "!" => $"$((!{operand}))",
            _ => operand
        };
    }

    private string GenerateFunctionCall(FunctionCallExpression call)
    {
        var args = string.Join(" ", call.Arguments.Select(GenerateExpression));

        // Handle built-in functions
        if (call.FunctionName == "print")
        {
            return $"echo {args}";
        }

        return args.Length > 0 ? $"$({call.FunctionName} {args})" : $"$({call.FunctionName})";
    }

    private string GenerateMethodCall(MethodCallExpression call)
    {
        if (call.Object is IdentifierExpression ident)
        {
            // Lower `x.foo(a, b)` to function-style call `foo x a b`.
            var args = new List<string> { $"${{{ident.Name}}}" };
            args.AddRange(call.Arguments.Select(GenerateExpression));
            return $"$({call.MethodName} {string.Join(" ", args)})";
        }

        ReportUnsupported($"method call receiver '{call.Object.GetType().Name}'");
        return "\"\"";
    }

    private string GenerateMemberAccess(MemberAccessExpression member)
    {
        // Simplified storage convention: `<var>_<field>`
        if (member.Object is IdentifierExpression ident)
            return $"${{{ident.Name}_{member.MemberName}}}";

        ReportUnsupported($"member access receiver '{member.Object.GetType().Name}'");
        return "\"\"";
    }

    private string GenerateArrayLiteral(ArrayLiteral array)
    {
        var elements = string.Join(" ", array.Elements.Select(GenerateExpression));
        return $"({elements})";
    }

    private string GenerateNullCoalesce(NullCoalesceExpression expr)
    {
        var right = GenerateExpression(expr.Right);

        return expr.Left switch
        {
            IdentifierExpression ident => $"${{{ident.Name}:-{right}}}",
            MemberAccessExpression member when member.Object is IdentifierExpression ident =>
                $"${{{ident.Name}_{member.MemberName}:-{right}}}",
            _ => HandleUnsupportedNullCoalesceLeft(expr.Left, right)
        };
    }

    private string HandleUnsupportedNullCoalesceLeft(Expression left, string right)
    {
        ReportUnsupported($"null coalesce left operand '{left.GetType().Name}'");
        return right;
    }
}
