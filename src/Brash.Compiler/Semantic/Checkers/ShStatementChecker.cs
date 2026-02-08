namespace Brash.Compiler.Semantic;

using System.Text.RegularExpressions;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Diagnostics;

/// <summary>
/// Emits non-fatal diagnostics for suspicious shell interpolation patterns in `sh` statements.
/// </summary>
public sealed class ShStatementChecker
{
    private static readonly Regex ParameterExpansionRegex = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    private readonly DiagnosticBag diagnostics;

    public ShStatementChecker(DiagnosticBag diagnostics)
    {
        this.diagnostics = diagnostics;
    }

    public void Analyze(ShStatement statement)
    {
        var script = statement.Script;
        if (string.IsNullOrWhiteSpace(script))
            return;

        foreach (Match match in ParameterExpansionRegex.Matches(script))
        {
            if (!match.Success || match.Groups.Count < 2)
                continue;

            var body = match.Groups[1].Value.Trim();
            if (!body.Contains('.', StringComparison.Ordinal))
                continue;

            diagnostics.AddWarning(
                $"Suspicious interpolation '{match.Value}' in sh statement. Bash parameter expansion does not support dotted member access; use flattened generated names (for example 'user_name').",
                statement.Line,
                statement.Column,
                DiagnosticCodes.SuspiciousShInterpolation);
        }
    }
}
