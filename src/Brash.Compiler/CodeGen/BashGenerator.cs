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
    private string? currentFunctionName;
    private TypeNode? currentFunctionReturnType;

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

        if (program.Statements.OfType<FunctionDeclaration>()
            .Any(f => string.Equals(f.Name, "main", StringComparison.Ordinal)))
        {
            EmitLine("main \"$@\"");
            EmitLine("exit $?");
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

        EmitLine("brash_async_exec_cmd() {");
        EmitLine("    local __cmd=\"$1\"");
        EmitLine("    bash -lc \"$__cmd\" >/dev/null 2>/dev/null &");
        EmitLine("}");
        EmitLine();

        EmitLine("brash_async_spawn_cmd() {");
        EmitLine("    local __cmd=\"$1\"");
        EmitLine("    local __out");
        EmitLine("    local __err");
        EmitLine("    local __status_file");
        EmitLine("    __out=$(mktemp)");
        EmitLine("    __err=$(mktemp)");
        EmitLine("    __status_file=$(mktemp)");
        EmitLine("    (");
        EmitLine("        bash -lc \"$__cmd\" >\"${__out}\" 2>\"${__err}\"");
        EmitLine("        printf '%s' \"$?\" >\"${__status_file}\"");
        EmitLine("    ) &");
        EmitLine("    local __pid=\"$!\"");
        EmitLine("    printf '%s:%s:%s:%s' \"${__pid}\" \"${__out}\" \"${__err}\" \"${__status_file}\"");
        EmitLine("}");
        EmitLine();

        EmitLine("brash_await() {");
        EmitLine("    local __handle=\"$1\"");
        EmitLine("    local __pid");
        EmitLine("    local __out");
        EmitLine("    local __err");
        EmitLine("    local __status_file");
        EmitLine("    IFS=':' read -r __pid __out __err __status_file <<< \"${__handle}\"");
        EmitLine("    if [[ -z \"${__pid}\" || -z \"${__out}\" || -z \"${__err}\" || -z \"${__status_file}\" ]]; then");
        EmitLine("        echo \"Invalid Process handle for await\" >&2");
        EmitLine("        return 1");
        EmitLine("    fi");
        EmitLine("    while kill -0 \"${__pid}\" 2>/dev/null; do");
        EmitLine("        sleep 0.01");
        EmitLine("    done");
        EmitLine("    local __status=\"1\"");
        EmitLine("    if [[ -f \"${__status_file}\" ]]; then");
        EmitLine("        __status=$(cat \"${__status_file}\")");
        EmitLine("    fi");
        EmitLine("    if [[ -f \"${__out}\" ]]; then");
        EmitLine("        cat \"${__out}\"");
        EmitLine("    fi");
        EmitLine("    if [[ ${__status} -ne 0 && -f \"${__err}\" ]]; then");
        EmitLine("        cat \"${__err}\" >&2");
        EmitLine("    fi");
        EmitLine("    rm -f \"${__out}\" \"${__err}\" \"${__status_file}\"");
        EmitLine("    return ${__status}");
        EmitLine("}");
        EmitLine();

        EmitLine("brash_throw() {");
        EmitLine("    local __msg=\"$1\"");
        EmitLine("    printf '%s\\n' \"$__msg\" >&2");
        EmitLine("    return 1 2>/dev/null || exit 1");
        EmitLine("}");
        EmitLine();

        EmitLine("brash_panic() {");
        EmitLine("    local __msg=\"$*\"");
        EmitLine("    if [[ -z \"$__msg\" ]]; then");
        EmitLine("        __msg=\"panic\"");
        EmitLine("    fi");
        EmitLine("    printf '%s\\n' \"$__msg\" >&2");
        EmitLine("    exit 1");
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
