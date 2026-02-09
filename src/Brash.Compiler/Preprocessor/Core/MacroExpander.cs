namespace Brash.Compiler.Preprocessor.Core;

using System.Text.RegularExpressions;

internal sealed class MacroExpander
{
    public string Expand(
        string value,
        IReadOnlyDictionary<string, string> macros,
        IReadOnlyDictionary<string, MacroDefinition> blockMacros)
    {
        if (macros.Count == 0 && blockMacros.Count == 0)
            return value;

        var expanded = value;
        for (var i = 0; i < 8; i++)
        {
            var changed = false;

            foreach (var macro in blockMacros.Values)
            {
                var next = ExpandFunctionLikeMacro(expanded, macro);

                if (macro.Parameters.Count == 0)
                    next = ExpandNoArgMacro(next, macro);

                if (!ReferenceEquals(next, expanded) && next != expanded)
                {
                    expanded = next;
                    changed = true;
                }
            }

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

    private static string ExpandNoArgMacro(string value, MacroDefinition macro)
    {
        var pattern = $@"\b{Regex.Escape(macro.Name)}!(?!\s*\()";
        return Regex.Replace(value, pattern, macro.Body);
    }

    private static string ExpandFunctionLikeMacro(string value, MacroDefinition macro)
    {
        var pattern = $@"\b{Regex.Escape(macro.Name)}!\s*\((?<args>[^)]*)\)";
        return Regex.Replace(
            value,
            pattern,
            match =>
            {
                var args = SplitArguments(match.Groups["args"].Value);
                if (args.Count > macro.Parameters.Count)
                    return match.Value;

                var body = macro.Body;
                for (var i = 0; i < macro.Parameters.Count; i++)
                {
                    var paramName = macro.Parameters[i];
                    var argValue = i < args.Count ? args[i].Trim() : "\"\"";
                    var paramPattern = $@"\b{Regex.Escape(paramName)}\b";
                    body = Regex.Replace(body, paramPattern, argValue);
                }

                return body;
            });
    }

    private static List<string> SplitArguments(string arguments)
    {
        var values = new List<string>();
        if (string.IsNullOrWhiteSpace(arguments))
            return values;

        var depth = 0;
        var start = 0;
        for (var i = 0; i < arguments.Length; i++)
        {
            var c = arguments[i];
            if (c == '(')
            {
                depth++;
                continue;
            }

            if (c == ')')
            {
                if (depth > 0)
                    depth--;
                continue;
            }

            if (c == ',' && depth == 0)
            {
                values.Add(arguments[start..i]);
                start = i + 1;
            }
        }

        values.Add(arguments[start..]);
        return values;
    }
}
