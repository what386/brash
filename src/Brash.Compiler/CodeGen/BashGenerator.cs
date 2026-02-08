namespace Brash.Compiler.CodeGen;

using System.Text;
using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Optimization.Ast;
using Brash.Compiler.Optimization.Bash;

public partial class BashGenerator
{
    private readonly StringBuilder output = new();
    private readonly List<string> warnings = new();
    private readonly AstOptimizer astOptimizer = new();
    private readonly BashOptimizer optimizer = new();
    private int indentLevel = 0;
    private int tempVariableCounter = 0;
    private const string IndentString = "    ";
    private string currentContext = "<unknown>";
    private string? currentFunctionName;
    private TypeNode? currentFunctionReturnType;
    public AstOptimizationOptions AstOptimizationOptions { get; } = new();
    public BashOptimizationOptions OptimizationOptions { get; } = new();

    public IReadOnlyList<string> Warnings => warnings;

    public string Generate(ProgramNode program)
    {
        var optimizedProgram = astOptimizer.Optimize(program, AstOptimizationOptions);

        output.Clear();
        warnings.Clear();
        indentLevel = 0;
        tempVariableCounter = 0;
        var helperUsage = HelperUsageAnalyzer.Analyze(optimizedProgram);

        // Bash shebang
        EmitLine("#!/usr/bin/env bash");
        EmitLine();
        EmitLine("set -euo pipefail");
        EmitLine();
        EmitRuntimeHelpers(helperUsage);
        EmitLine();

        // Generate code for each statement
        foreach (var stmt in optimizedProgram.Statements)
        {
            GenerateStatement(stmt);
            EmitLine();
        }

        if (optimizedProgram.Statements.OfType<FunctionDeclaration>()
            .Any(f => string.Equals(f.Name, "main", StringComparison.Ordinal)))
        {
            EmitLine("main \"$@\"");
        }

        return optimizer.Optimize(output.ToString(), OptimizationOptions);
    }

    private void EmitRuntimeHelpers(HelperUsage usage)
    {
        if (usage.NeedsGetField)
        {
            EmitLine("brash_get_field() {");
            EmitLine("    local __obj=\"$1\"");
            EmitLine("    local __field=\"$2\"");
            EmitLine("    local __key=\"${__obj}_${__field}\"");
            EmitLine("    printf '%s' \"${!__key-}\"");
            EmitLine("}");
            EmitLine();
        }

        if (usage.NeedsSetField)
        {
            EmitLine("brash_set_field() {");
            EmitLine("    local __obj=\"$1\"");
            EmitLine("    local __field=\"$2\"");
            EmitLine("    local __value=\"$3\"");
            EmitLine("    local __key=\"${__obj}_${__field}\"");
            EmitLine("    printf -v \"$__key\" '%s' \"$__value\"");
            EmitLine("}");
            EmitLine();
        }

        if (usage.NeedsCallMethod)
        {
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
        }

        if (usage.NeedsMapNew)
        {
            EmitLine("brash_map_new() {");
            EmitLine("    mktemp");
            EmitLine("}");
            EmitLine();
        }

        if (usage.NeedsMapSet)
        {
            EmitLine("brash_map_set() {");
            EmitLine("    local __map=\"$1\"");
            EmitLine("    local __key=\"$2\"");
            EmitLine("    local __value=\"$3\"");
            EmitLine("    local __tmp");
            EmitLine("    __tmp=$(mktemp)");
            EmitLine("    if [[ -f \"${__map}\" ]]; then");
            EmitLine("        awk -v k=\"${__key}\" -F '\\t' '$1 != k' \"${__map}\" > \"${__tmp}\" || true");
            EmitLine("    fi");
            EmitLine("    printf '%s\\t%s\\n' \"${__key}\" \"${__value}\" >> \"${__tmp}\"");
            EmitLine("    mv \"${__tmp}\" \"${__map}\"");
            EmitLine("}");
            EmitLine();
        }

        if (usage.NeedsMapGet)
        {
            EmitLine("brash_map_get() {");
            EmitLine("    local __map=\"$1\"");
            EmitLine("    local __key=\"$2\"");
            EmitLine("    if [[ ! -f \"${__map}\" ]]; then");
            EmitLine("        return 0");
            EmitLine("    fi");
            EmitLine("    awk -v k=\"${__key}\" -F '\\t' '$1 == k { v = $2 } END { printf \"%s\", v }' \"${__map}\"");
            EmitLine("}");
            EmitLine();
        }

        if (usage.NeedsMapLiteral)
        {
            EmitLine("brash_map_literal() {");
            EmitLine("    local __map");
            EmitLine("    __map=$(brash_map_new)");
            EmitLine("    : > \"${__map}\"");
            EmitLine("    while [[ $# -ge 2 ]]; do");
            EmitLine("        brash_map_set \"${__map}\" \"$1\" \"$2\"");
            EmitLine("        shift 2");
            EmitLine("    done");
            EmitLine("    printf '%s' \"${__map}\"");
            EmitLine("}");
            EmitLine();
        }

        if (usage.NeedsIndexGet)
        {
            EmitLine("brash_index_get() {");
            EmitLine("    local __name=\"$1\"");
            EmitLine("    local __key=\"$2\"");
            EmitLine("    local __value=\"${!__name-}\"");
            EmitLine("    if [[ -f \"${__value}\" ]]; then");
            EmitLine("        brash_map_get \"${__value}\" \"${__key}\"");
            EmitLine("        return");
            EmitLine("    fi");
            EmitLine("    eval \"printf '%s' \\\"\\${${__name}[\\\"\\$__key\\\"]-}\\\"\"");
            EmitLine("}");
            EmitLine();
        }

        if (usage.NeedsIndexSet)
        {
            EmitLine("brash_index_set() {");
            EmitLine("    local __name=\"$1\"");
            EmitLine("    local __key=\"$2\"");
            EmitLine("    local __item=\"$3\"");
            EmitLine("    local __value=\"${!__name-}\"");
            EmitLine("    if [[ -f \"${__value}\" ]]; then");
            EmitLine("        brash_map_set \"${__value}\" \"${__key}\" \"${__item}\"");
            EmitLine("        return");
            EmitLine("    fi");
            EmitLine("    eval \"${__name}[\\\"\\$__key\\\"]=\\\"\\$__item\\\"\"");
            EmitLine("}");
            EmitLine();
        }

        if (usage.NeedsExecCmd)
        {
            EmitLine("brash_exec_cmd() {");
            EmitLine("    local __cmd=\"$1\"");
            EmitLine("    bash -lc \"$__cmd\"");
            EmitLine("    return $?");
            EmitLine("}");
            EmitLine();
        }

        if (usage.NeedsAsyncSpawnCmd)
        {
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
        }

        if (usage.NeedsAwait)
        {
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
        }

        if (usage.NeedsReadLn)
        {
            EmitLine("brash_readln() {");
            EmitLine("    local __prompt=\"${1-}\"");
            EmitLine("    if [[ -n \"${__prompt}\" ]]; then");
            EmitLine("        printf '%s' \"${__prompt}\"");
            EmitLine("    fi");
            EmitLine("    local __line=\"\"");
            EmitLine("    if IFS= read -r __line; then");
            EmitLine("        printf '%s' \"${__line}\"");
            EmitLine("        return 0");
            EmitLine("    fi");
            EmitLine("    printf ''");
            EmitLine("    return 0");
            EmitLine("}");
            EmitLine();
        }
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

    private string EscapeString(string str, bool preserveLineBreaks = false)
    {
        var escaped = str.Replace("\\", "\\\\")
                         .Replace("\"", "\\\"")
                         .Replace("$", "\\$")
                         .Replace("`", "\\`");

        if (!preserveLineBreaks)
        {
            escaped = escaped.Replace("\n", "\\n")
                             .Replace("\r", "\\r");
        }

        return escaped.Replace("\t", "\\t");
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
