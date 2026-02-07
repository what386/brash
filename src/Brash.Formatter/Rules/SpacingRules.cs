namespace Brash.Formatter;

using System.Text;

internal static class SpacingRules
{
    public static string Normalize(string line)
    {
        if (line.StartsWith("//", StringComparison.Ordinal))
            return line;

        var sb = new StringBuilder(line.Length + 8);
        bool inString = false;
        bool escaped = false;

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (inString)
            {
                sb.Append(ch);
                if (escaped)
                {
                    escaped = false;
                }
                else if (ch == '\\')
                {
                    escaped = true;
                }
                else if (ch == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (ch == '"')
            {
                inString = true;
                sb.Append(ch);
                continue;
            }

            if (ch == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                    sb.Append(' ');
                sb.Append(line.AsSpan(i));
                break;
            }

            if (ch == ',' || ch == ':')
            {
                TrimTrailingSpaces(sb);
                sb.Append(ch);
                sb.Append(' ');
                SkipFollowingSpaces(line, ref i);
                continue;
            }

            if (ch == '{')
            {
                EnsureSpaceBeforeBrace(sb);
                sb.Append('{');
                if (i + 1 < line.Length && line[i + 1] != '}')
                    sb.Append(' ');
                SkipFollowingSpaces(line, ref i);
                continue;
            }

            if (ch == '}')
            {
                TrimTrailingSpaces(sb);
                if (sb.Length > 0 && sb[^1] != '{' && sb[^1] != ' ')
                    sb.Append(' ');
                sb.Append('}');
                continue;
            }

            if (TryReadOperator(line, i, out var op, out var consume))
            {
                TrimTrailingSpaces(sb);
                sb.Append(' ');
                sb.Append(op);
                sb.Append(' ');
                i += consume - 1;
                SkipFollowingSpaces(line, ref i);
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length == 0 || sb[^1] != ' ')
                    sb.Append(' ');
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static bool TryReadOperator(string line, int index, out string op, out int consume)
    {
        var twoChar = index + 1 < line.Length ? line.Substring(index, 2) : string.Empty;
        if (twoChar is "==" or "!=" or "<=" or ">=" or "&&" or "||" or "??" or "..")
        {
            op = twoChar;
            consume = 2;
            return true;
        }

        var single = line[index];
        if (single is '=' or '+' or '-' or '*' or '/' or '%' or '<' or '>' or '|')
        {
            op = single.ToString();
            consume = 1;
            return true;
        }

        op = string.Empty;
        consume = 0;
        return false;
    }

    private static void TrimTrailingSpaces(StringBuilder sb)
    {
        while (sb.Length > 0 && sb[^1] == ' ')
            sb.Length--;
    }

    private static void SkipFollowingSpaces(string line, ref int index)
    {
        while (index + 1 < line.Length && char.IsWhiteSpace(line[index + 1]))
            index++;
    }

    private static void EnsureSpaceBeforeBrace(StringBuilder sb)
    {
        if (sb.Length == 0)
            return;

        var prev = sb[^1];
        if (prev == ' ')
            return;

        if (char.IsLetterOrDigit(prev) || prev == '_' || prev == ')' || prev == ']')
            sb.Append(' ');
    }
}
