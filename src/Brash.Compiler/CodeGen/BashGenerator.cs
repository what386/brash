namespace Brash.Compiler.CodeGen;

using System.Text;
using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;

public partial class BashGenerator
{
    private readonly StringBuilder output = new();
    private readonly List<string> warnings = new();
    private int indentLevel = 0;
    private const string IndentString = "    ";
    private string currentContext = "<unknown>";

    public IReadOnlyList<string> Warnings => warnings;

    public string Generate(ProgramNode program)
    {
        output.Clear();
        warnings.Clear();
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

    private void EmitComment(string comment)
    {
        Emit($"# {comment}");
    }

    private string EscapeString(string str)
    {
        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }

    private void ReportUnsupported(string feature)
    {
        if (!warnings.Contains(feature))
            warnings.Add(feature);
    }

    private string UnsupportedExpression(Expression expr)
    {
        ReportUnsupported($"expression '{expr.GetType().Name}' in {currentContext}");
        return "\"\"";
    }
}
