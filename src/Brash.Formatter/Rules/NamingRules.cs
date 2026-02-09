namespace Brash.Formatter.Rules;

using System.Text;
using System.Text.RegularExpressions;

internal static class NamingRules
{
    private static readonly Regex TypeDeclPattern = new(
        @"^(struct|enum|impl)\s+([A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled);

    private static readonly Regex ConstDeclPattern = new(
        @"^const\s+([A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled);

    private static readonly Regex WordSplitPattern = new(
        @"[A-Z]?[a-z0-9]+|[A-Z]+(?![a-z])",
        RegexOptions.Compiled);

    public static string[] NormalizeIdentifiers(string[] lines)
    {
        var map = BuildRenameMap(lines);
        if (map.Count == 0)
            return lines;

        var result = new string[lines.Length];
        for (int i = 0; i < lines.Length; i++)
            result[i] = ReplaceIdentifiers(lines[i], map);

        return result;
    }

    private static Dictionary<string, string> BuildRenameMap(IEnumerable<string> lines)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;

            var typeMatch = TypeDeclPattern.Match(trimmed);
            if (typeMatch.Success)
            {
                var original = typeMatch.Groups[2].Value;
                var renamed = ToPascalCase(original);
                if (original != renamed && !map.ContainsKey(original))
                    map[original] = renamed;
            }

            var constMatch = ConstDeclPattern.Match(trimmed);
            if (constMatch.Success)
            {
                var original = constMatch.Groups[1].Value;
                var renamed = ToUpperSnakeCase(original);
                if (original != renamed && !map.ContainsKey(original))
                    map[original] = renamed;
            }
        }

        return map;
    }

    private static string ReplaceIdentifiers(string line, IReadOnlyDictionary<string, string> map)
    {
        if (line.Length == 0)
            return line;

        var sb = new StringBuilder(line.Length + 16);
        bool inString = false;
        bool escaped = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];

            if (!inString && ch == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                sb.Append(line.AsSpan(i));
                break;
            }

            if (ch == '"' && !escaped)
            {
                inString = !inString;
                sb.Append(ch);
                escaped = false;
                continue;
            }

            if (inString)
            {
                sb.Append(ch);
                escaped = ch == '\\' && !escaped;
                continue;
            }

            escaped = false;

            if (IsIdentifierStart(ch))
            {
                int start = i;
                int end = i + 1;
                while (end < line.Length && IsIdentifierPart(line[end]))
                    end++;

                var token = line[start..end];
                if (map.TryGetValue(token, out var renamed))
                    sb.Append(renamed);
                else
                    sb.Append(token);

                i = end - 1;
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string ToPascalCase(string value)
    {
        var words = SplitWords(value);
        if (words.Count == 0)
            return value;

        var sb = new StringBuilder(value.Length);
        foreach (var word in words)
        {
            if (word.Length == 0)
                continue;

            sb.Append(char.ToUpperInvariant(word[0]));
            if (word.Length > 1)
                sb.Append(word[1..].ToLowerInvariant());
        }

        return sb.ToString();
    }

    private static string ToUpperSnakeCase(string value)
    {
        var words = SplitWords(value);
        if (words.Count == 0)
            return value.ToUpperInvariant();

        return string.Join("_", words.Select(w => w.ToUpperInvariant()));
    }

    private static List<string> SplitWords(string value)
    {
        var normalized = value.Replace('-', '_').Replace(' ', '_');
        var parts = normalized.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var words = new List<string>();

        foreach (var part in parts)
        {
            foreach (Match match in WordSplitPattern.Matches(part))
            {
                if (match.Length > 0)
                    words.Add(match.Value);
            }
        }

        return words;
    }

    private static bool IsIdentifierStart(char c)
    {
        return c == '_' || char.IsLetter(c);
    }

    private static bool IsIdentifierPart(char c)
    {
        return c == '_' || char.IsLetterOrDigit(c);
    }
}
