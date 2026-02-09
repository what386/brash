namespace Brash.Compiler.Preprocessor.Core;

using System.Globalization;

internal sealed class ConditionParser
{
    private readonly List<string> tokens;
    private readonly Dictionary<string, string> macros;
    private int index;

    public ConditionParser(string input, Dictionary<string, string> macros)
    {
        this.tokens = Tokenize(input);
        this.macros = macros;
    }

    public bool Parse()
    {
        var result = ParseOr();
        if (index != tokens.Count)
            throw new InvalidOperationException($"unexpected token '{tokens[index]}'");
        return result;
    }

    private bool ParseOr()
    {
        var value = ParseAnd();
        while (Match("||"))
            value = value || ParseAnd();
        return value;
    }

    private bool ParseAnd()
    {
        var value = ParseUnary();
        while (Match("&&"))
            value = value && ParseUnary();
        return value;
    }

    private bool ParseUnary()
    {
        if (Match("!"))
            return !ParseUnary();
        return ParsePrimary();
    }

    private bool ParsePrimary()
    {
        if (Match("("))
        {
            var value = ParseOr();
            if (!Match(")"))
                throw new InvalidOperationException("missing ')'");
            return value;
        }

        if (index >= tokens.Count)
            throw new InvalidOperationException("unexpected end of expression");

        var token = tokens[index++];
        return Truthy(token);
    }

    private bool Match(string token)
    {
        if (index < tokens.Count && tokens[index] == token)
        {
            index++;
            return true;
        }

        return false;
    }

    private bool Truthy(string token)
    {
        if (token.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (token.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;

        if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            return integer != 0;

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating))
            return Math.Abs(floating) > double.Epsilon;

        if (macros.TryGetValue(token, out var value))
        {
            if (value == token)
                return true;
            return Truthy(value.Trim());
        }

        return false;
    }

    private static List<string> Tokenize(string input)
    {
        var result = new List<string>();
        for (var i = 0; i < input.Length;)
        {
            if (char.IsWhiteSpace(input[i]))
            {
                i++;
                continue;
            }

            if (i + 1 < input.Length)
            {
                var two = input.Substring(i, 2);
                if (two is "&&" or "||")
                {
                    result.Add(two);
                    i += 2;
                    continue;
                }
            }

            var one = input[i];
            if (one is '!' or '(' or ')')
            {
                result.Add(one.ToString());
                i++;
                continue;
            }

            var start = i;
            while (i < input.Length
                   && !char.IsWhiteSpace(input[i])
                   && input[i] != '!'
                   && input[i] != '('
                   && input[i] != ')')
            {
                if (i + 1 < input.Length && input.Substring(i, 2) is "&&" or "||")
                    break;
                i++;
            }

            result.Add(input[start..i]);
        }

        return result;
    }
}

