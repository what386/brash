namespace Brash.Compiler.CodeGen;

using System.Text;
using Brash.Compiler.Ast;

public class BashGenerator
{
    private readonly StringBuilder output = new();
    private int indentLevel = 0;
    private const string IndentString = "    ";

    public string Generate(ProgramNode program)
    {
        output.Clear();
        indentLevel = 0;

        // Bash shebang
        EmitLine("#!/usr/bin/env bash");
        EmitLine();
        EmitLine("set -euo pipefail");
        EmitLine();

        // Generate code for each statement
        foreach (var stmt in program.Statements)
        {
            GenerateStatement(stmt);
            EmitLine();
        }

        return output.ToString();
    }

    private void GenerateStatement(Statement stmt)
    {
        switch (stmt)
        {
            case VariableDeclaration varDecl:
                GenerateVariableDeclaration(varDecl);
                break;

            case Assignment assignment:
                GenerateAssignment(assignment);
                break;

            case FunctionDeclaration funcDecl:
                GenerateFunctionDeclaration(funcDecl);
                break;

            case IfStatement ifStmt:
                GenerateIfStatement(ifStmt);
                break;

            case ForLoop forLoop:
                GenerateForLoop(forLoop);
                break;

            case WhileLoop whileLoop:
                GenerateWhileLoop(whileLoop);
                break;

            case ReturnStatement returnStmt:
                GenerateReturnStatement(returnStmt);
                break;

            case BreakStatement:
                Emit("break");
                break;

            case ContinueStatement:
                Emit("continue");
                break;

            case ExpressionStatement exprStmt:
                Emit(GenerateExpression(exprStmt.Expression));
                break;
        }
    }

    private void GenerateVariableDeclaration(VariableDeclaration varDecl)
    {
        var value = GenerateExpression(varDecl.Value);

        if (varDecl.Kind == VariableDeclaration.VarKind.Const)
        {
            Emit($"readonly {varDecl.Name}={value}");
        }
        else
        {
            // Both 'let' and 'mut' become regular bash variables
            Emit($"{varDecl.Name}={value}");
        }
    }

    private void GenerateAssignment(Assignment assignment)
    {
        var target = GenerateExpression(assignment.Target);
        var value = GenerateExpression(assignment.Value);
        Emit($"{target}={value}");
    }

    private void GenerateFunctionDeclaration(FunctionDeclaration func)
    {
        Emit($"{func.Name}() {{");
        indentLevel++;

        // Generate parameter assignments
        for (int i = 0; i < func.Parameters.Count; i++)
        {
            var param = func.Parameters[i];
            EmitLine($"local {param.Name}=\"${{{i + 1}}}\"");
        }

        if (func.Parameters.Count > 0)
            EmitLine();

        // Generate body
        foreach (var stmt in func.Body)
        {
            GenerateStatement(stmt);
            EmitLine();
        }

        indentLevel--;
        Emit("}");
    }

    private void GenerateIfStatement(IfStatement ifStmt)
    {
        var condition = GenerateCondition(ifStmt.Condition);
        Emit($"if {condition}; then");
        indentLevel++;

        foreach (var stmt in ifStmt.ThenBlock)
        {
            EmitLine();
            GenerateStatement(stmt);
        }

        indentLevel--;

        foreach (var elif in ifStmt.ElifClauses)
        {
            EmitLine();
            var elifCondition = GenerateCondition(elif.Condition);
            Emit($"elif {elifCondition}; then");
            indentLevel++;

            foreach (var stmt in elif.Block)
            {
                EmitLine();
                GenerateStatement(stmt);
            }

            indentLevel--;
        }

        if (ifStmt.ElseBlock != null)
        {
            EmitLine();
            Emit("else");
            indentLevel++;

            foreach (var stmt in ifStmt.ElseBlock)
            {
                EmitLine();
                GenerateStatement(stmt);
            }

            indentLevel--;
        }

        EmitLine();
        Emit("fi");
    }

    private void GenerateForLoop(ForLoop forLoop)
    {
        // Generate range
        string rangeExpr;
        if (forLoop.Range is RangeExpression range)
        {
            var start = GenerateExpression(range.Start);
            var end = GenerateExpression(range.End);

            if (forLoop.Step != null)
            {
                var step = GenerateExpression(forLoop.Step);
                rangeExpr = $"$(seq {start} {step} {end})";
            }
            else
            {
                rangeExpr = $"$(seq {start} {end})";
            }
        }
        else
        {
            // Collection iteration
            rangeExpr = GenerateExpression(forLoop.Range);
        }

        Emit($"for {forLoop.Variable} in {rangeExpr}; do");
        indentLevel++;

        foreach (var stmt in forLoop.Body)
        {
            EmitLine();
            GenerateStatement(stmt);
        }

        indentLevel--;
        EmitLine();
        Emit("done");
    }

    private void GenerateWhileLoop(WhileLoop whileLoop)
    {
        var condition = GenerateCondition(whileLoop.Condition);
        Emit($"while {condition}; do");
        indentLevel++;

        foreach (var stmt in whileLoop.Body)
        {
            EmitLine();
            GenerateStatement(stmt);
        }

        indentLevel--;
        EmitLine();
        Emit("done");
    }

    private void GenerateReturnStatement(ReturnStatement returnStmt)
    {
        if (returnStmt.Value != null)
        {
            var value = GenerateExpression(returnStmt.Value);
            Emit($"echo {value}");
            EmitLine();
            Emit("return 0");
        }
        else
        {
            Emit("return 0");
        }
    }

    private string GenerateExpression(Expression expr)
    {
        return expr switch
        {
            LiteralExpression lit => GenerateLiteral(lit),
            IdentifierExpression ident => $"${{{ident.Name}}}",
            BinaryExpression bin => GenerateBinaryExpression(bin),
            FunctionCallExpression call => GenerateFunctionCall(call),
            MemberAccessExpression member => GenerateMemberAccess(member),
            ArrayLiteral array => GenerateArrayLiteral(array),
            NullLiteral => "\"\"",
            _ => "\"\""
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

    private string GenerateMemberAccess(MemberAccessExpression member)
    {
        // Simplified: assume struct fields are stored as bash associative arrays
        var obj = GenerateExpression(member.Object);
        return $"${{{obj}_{member.MemberName}}}";
    }

    private string GenerateArrayLiteral(ArrayLiteral array)
    {
        var elements = string.Join(" ", array.Elements.Select(GenerateExpression));
        return $"({elements})";
    }

    private string GenerateCondition(Expression condition)
    {
        if (condition is BinaryExpression bin && IsComparisonOperator(bin.Operator))
        {
            return GenerateBinaryExpression(bin);
        }

        // Treat as boolean test
        var expr = GenerateExpression(condition);
        return $"[ {expr} -eq 0 ]";
    }

    private bool IsComparisonOperator(string op)
    {
        return op is "==" or "!=" or "<" or ">" or "<=" or ">=";
    }

    private string EscapeString(string str)
    {
        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }

    private void Emit(string code)
    {
        output.Append(new string(' ', indentLevel * IndentString.Length));
        output.Append(code);
    }

    private void EmitLine(string code = "")
    {
        if (!string.IsNullOrEmpty(code))
            Emit(code);
        output.AppendLine();
    }
}
