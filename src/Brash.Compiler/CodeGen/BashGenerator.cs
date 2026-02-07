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
    private int tempVariableCounter = 0;
    private const string IndentString = "    ";
    private string currentContext = "<unknown>";

    public IReadOnlyList<string> Warnings => warnings;

    public string Generate(ProgramNode program)
    {
        output.Clear();
        warnings.Clear();
        indentLevel = 0;
        tempVariableCounter = 0;

        // Bash shebang
        EmitLine("#!/usr/bin/env bash");
        EmitLine();
        EmitLine("set -euo pipefail");
        EmitLine();
        EmitRuntimeHelpers();
        EmitLine();

        // Generate code for each statement
        foreach (var stmt in program.Statements)
        {
            GenerateStatement(stmt);
            EmitLine();
        }

        return output.ToString();
    }

    private void EmitRuntimeHelpers()
    {
        EmitLine("brash_get_field() {");
        EmitLine("    local __obj=\"$1\"");
        EmitLine("    local __field=\"$2\"");
        EmitLine("    local __key=\"${__obj}_${__field}\"");
        EmitLine("    printf '%s' \"${!__key-}\"");
        EmitLine("}");
        EmitLine();

        EmitLine("brash_set_field() {");
        EmitLine("    local __obj=\"$1\"");
        EmitLine("    local __field=\"$2\"");
        EmitLine("    local __value=\"$3\"");
        EmitLine("    local __key=\"${__obj}_${__field}\"");
        EmitLine("    printf -v \"$__key\" '%s' \"$__value\"");
        EmitLine("}");
        EmitLine();

        EmitLine("brash_call_method() {");
        EmitLine("    local __obj=\"$1\"");
        EmitLine("    local __method=\"$2\"");
        EmitLine("    shift 2");
        EmitLine("    local __type_key=\"${__obj}__type\"");
        EmitLine("    local __type=\"${!__type_key-}\"");
        EmitLine("    if [[ -z \"$__type\" ]]; then");
        EmitLine("        echo \"\" >&2");
        EmitLine("        return 1");
        EmitLine("    fi");
        EmitLine("    local __fn=\"${__type}__${__method}\"");
        EmitLine("    \"$__fn\" \"$__obj\" \"$@\"");
        EmitLine("}");
        EmitLine();

        EmitLine("brash_build_cmd() {");
        EmitLine("    local __part");
        EmitLine("    local __out=\"\"");
        EmitLine("    for __part in \"$@\"; do");
        EmitLine("        printf -v __part '%q' \"$__part\"");
        EmitLine("        __out+=\" ${__part}\"");
        EmitLine("    done");
        EmitLine("    printf '%s' \"${__out# }\"");
        EmitLine("}");
        EmitLine();

        EmitLine("brash_pipe_cmd() {");
        EmitLine("    local __left=\"$1\"");
        EmitLine("    local __right=\"$2\"");
        EmitLine("    printf '%s | %s' \"$__left\" \"$__right\"");
        EmitLine("}");
        EmitLine();

        EmitLine("brash_exec_cmd() {");
        EmitLine("    local __cmd=\"$1\"");
        EmitLine("    bash -lc \"$__cmd\"");
        EmitLine("}");
        EmitLine();

        EmitLine("brash_spawn_cmd() {");
        EmitLine("    local __cmd=\"$1\"");
        EmitLine("    bash -lc \"$__cmd\" &");
        EmitLine("    printf '%s' \"$!\"");
        EmitLine("}");
        EmitLine();

        EmitLine("brash_throw() {");
        EmitLine("    local __msg=\"$1\"");
        EmitLine("    printf '%s\\n' \"$__msg\" >&2");
        EmitLine("    return 1 2>/dev/null || exit 1");
        EmitLine("}");
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

    private string NextTempVariable(string prefix)
    {
        var name = $"{prefix}_{tempVariableCounter}";
        tempVariableCounter++;
        return name;
    }
}
