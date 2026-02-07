namespace Brash.Formatter;

internal static class FormatterEngine
{
    public static string Format(string source, FormatterOptions options)
    {
        var normalized = source.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        lines = NamingRules.NormalizeIdentifiers(lines);
        var output = new List<string>(lines.Length);

        int indentLevel = 0;
        bool previousBlank = false;

        foreach (var rawLine in lines)
        {
            var trimmedRight = rawLine.TrimEnd();
            var trimmed = trimmedRight.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                if (!options.CollapseConsecutiveBlankLines)
                {
                    output.Add(string.Empty);
                    previousBlank = true;
                    continue;
                }

                if (!previousBlank && output.Count > 0)
                {
                    output.Add(string.Empty);
                    previousBlank = true;
                }
                continue;
            }

            var line = SpacingRules.Normalize(trimmed);
            indentLevel = Math.Max(0, indentLevel - IndentationRules.GetLeadingDeductions(line));

            var prefix = new string(' ', indentLevel * options.SpacesPerIndent);
            var wrapped = WrappingRules.WrapArgumentsIfNeeded(line, prefix, options);
            output.AddRange(wrapped);
            previousBlank = false;

            indentLevel += IndentationRules.GetTrailingIncreases(line);
            if (indentLevel < 0)
                indentLevel = 0;
        }

        var result = string.Join('\n', output);
        return options.EnsureTrailingNewline ? result + '\n' : result;
    }
}
