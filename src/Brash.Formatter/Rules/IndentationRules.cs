namespace Brash.Formatter;

using System.Text.RegularExpressions;

internal static class IndentationRules
{
    private static readonly Regex LeadingDedentPattern = new(
        @"^(end|else|elif|catch)\b",
        RegexOptions.Compiled);

    private static readonly Regex TrailingIndentPattern = new(
        @"^(async\s+fn|fn|if|elif|else|for|while|try|catch|struct|enum|impl)\b",
        RegexOptions.Compiled);

    public static int GetLeadingDeductions(string line)
    {
        return LeadingDedentPattern.IsMatch(line) ? 1 : 0;
    }

    public static int GetTrailingIncreases(string line)
    {
        return TrailingIndentPattern.IsMatch(line) ? 1 : 0;
    }
}
