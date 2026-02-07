namespace Brash.Compiler.Preprocessor.Directives;

internal static class DirectiveParsing
{
    public static int FindFirstWhitespace(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
                return i;
        }

        return -1;
    }

    public static bool IsValidMacroName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (!(char.IsLetter(name[0]) || name[0] == '_'))
            return false;

        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (!(char.IsLetterOrDigit(c) || c == '_'))
                return false;
        }

        return true;
    }
}

