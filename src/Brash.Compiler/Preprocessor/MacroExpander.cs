namespace Brash.Compiler.Preprocessor;

using System.Text.RegularExpressions;

internal sealed class MacroExpander
{
    public string Expand(string value, IReadOnlyDictionary<string, string> macros)
    {
        if (macros.Count == 0)
            return value;

        var expanded = value;
        for (var i = 0; i < 8; i++)
        {
            var changed = false;

            foreach (var (name, replacement) in macros)
            {
                var pattern = $@"\b{Regex.Escape(name)}\b";
                var next = Regex.Replace(expanded, pattern, replacement);
                if (!ReferenceEquals(next, expanded) && next != expanded)
                {
                    expanded = next;
                    changed = true;
                }
            }

            if (!changed)
                break;
        }

        return expanded;
    }
}

