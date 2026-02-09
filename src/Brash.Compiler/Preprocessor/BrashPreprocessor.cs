namespace Brash.Compiler.Preprocessor;

using System.Text;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Preprocessor.Directives;
using Brash.Compiler.Preprocessor.Macros;

/// <summary>
/// Applies pure text preprocessor transformations before lexing/parsing.
/// </summary>
public sealed class BrashPreprocessor
{
    private readonly IReadOnlyList<IPreprocessorDirectiveHandler> directiveHandlers;
    private readonly MacroExpander macroExpander = new();

    public BrashPreprocessor()
    {
        directiveHandlers = new IPreprocessorDirectiveHandler[]
        {
            new ShebangDirectiveHandler(),
            new DefineDirectiveHandler(),
            new UndefDirectiveHandler(),
            new IfDefDirectiveHandler(),
            new IfNDefDirectiveHandler(),
            new IfDirectiveHandler(),
            new ElseDirectiveHandler(),
            new EndIfDirectiveHandler()
        };
    }

    public string Process(string source, DiagnosticBag diagnostics)
    {
        var state = new PreprocessorState(diagnostics);
        foreach (var macro in PredefinedMacroRegistry.All)
            state.BlockMacros[macro.Name] = macro;
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var output = new StringBuilder(source.Length);
        var macroBodyBuilder = new StringBuilder();
        string? activeMacroName = null;
        List<string>? activeMacroParameters = null;
        int activeMacroStartLine = -1;
        bool activeMacroShouldRegister = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;
            var trimmed = line.TrimStart();
            var isDirective = trimmed.StartsWith('#');
            var currentActive = state.IsCurrentBranchActive;

            if (activeMacroName != null)
            {
                if (trimmed.StartsWith("#endmacro", StringComparison.Ordinal))
                {
                    if (activeMacroShouldRegister)
                    {
                        state.BlockMacros[activeMacroName] = new MacroDefinition
                        {
                            Name = activeMacroName,
                            Parameters = activeMacroParameters is null
                                ? Array.Empty<string>()
                                : activeMacroParameters,
                            Body = macroBodyBuilder.ToString()
                        };
                    }

                    activeMacroName = null;
                    activeMacroParameters = null;
                    activeMacroStartLine = -1;
                    activeMacroShouldRegister = false;
                    macroBodyBuilder.Clear();
                }
                else if (activeMacroShouldRegister)
                {
                    if (macroBodyBuilder.Length > 0)
                        macroBodyBuilder.Append('\n');
                    macroBodyBuilder.Append(line);
                }

                if (i < lines.Length - 1)
                    output.Append('\n');
                continue;
            }

            if (!isDirective)
            {
                output.Append(
                    currentActive
                        ? macroExpander.Expand(line, state.Macros, state.BlockMacros)
                        : string.Empty);
                if (i < lines.Length - 1)
                    output.Append('\n');
                continue;
            }

            if (trimmed.StartsWith("#macro", StringComparison.Ordinal))
            {
                ParseMacroHeader(
                    trimmed,
                    lineNumber,
                    state,
                    out activeMacroName,
                    out activeMacroParameters);
                activeMacroStartLine = lineNumber;
                activeMacroShouldRegister = currentActive && activeMacroName != null;
                macroBodyBuilder.Clear();

                if (i < lines.Length - 1)
                    output.Append('\n');
                continue;
            }

            if (trimmed.StartsWith("#endmacro", StringComparison.Ordinal))
            {
                state.ReportError(lineNumber, "Preprocessor error: '#endmacro' without matching '#macro'");
                if (i < lines.Length - 1)
                    output.Append('\n');
                continue;
            }

            var context = new DirectiveContext(trimmed, lineNumber, currentActive, state, macroExpander);
            var handler = directiveHandlers.FirstOrDefault(h => h.CanHandle(trimmed));
            if (handler == null)
            {
                state.ReportError(lineNumber, $"Preprocessor error: unknown directive '{trimmed}'");
            }
            else
            {
                handler.Apply(context);
            }

            // Keep line mapping stable by replacing directive lines with empty lines.
            if (i < lines.Length - 1)
                output.Append('\n');
        }

        while (state.Frames.Count > 0)
        {
            var frame = state.Frames.Pop();
            state.ReportError(frame.StartLine, "Preprocessor error: missing '#endif' for conditional block");
        }

        if (activeMacroName != null)
        {
            state.ReportError(
                activeMacroStartLine,
                $"Preprocessor error: missing '#endmacro' for macro '{activeMacroName}'");
        }

        return output.ToString();
    }

    private static void ParseMacroHeader(
        string trimmed,
        int lineNumber,
        PreprocessorState state,
        out string? name,
        out List<string>? parameters)
    {
        name = null;
        parameters = null;

        var header = trimmed["#macro".Length..].Trim();
        if (string.IsNullOrWhiteSpace(header))
        {
            state.ReportError(lineNumber, "Preprocessor error: '#macro' requires a macro name");
            return;
        }

        var parenIndex = header.IndexOf('(');
        if (parenIndex < 0)
        {
            var splitIndex = DirectiveParsing.FindFirstWhitespace(header);
            var candidate = splitIndex < 0 ? header : header[..splitIndex];
            if (!DirectiveParsing.IsValidMacroName(candidate))
            {
                state.ReportError(lineNumber, $"Preprocessor error: invalid macro name '{candidate}'");
                return;
            }

            name = candidate;
            parameters = new List<string>();
            return;
        }

        var candidateName = header[..parenIndex].Trim();
        if (!DirectiveParsing.IsValidMacroName(candidateName))
        {
            state.ReportError(lineNumber, $"Preprocessor error: invalid macro name '{candidateName}'");
            return;
        }

        var closeIndex = header.IndexOf(')', parenIndex + 1);
        if (closeIndex < 0)
        {
            state.ReportError(lineNumber, "Preprocessor error: '#macro' parameter list is missing ')'");
            return;
        }

        var parameterBlock = header[(parenIndex + 1)..closeIndex];
        var parsedParameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(parameterBlock))
        {
            var parts = parameterBlock.Split(',');
            foreach (var rawPart in parts)
            {
                var piece = rawPart.Trim();
                if (piece.Length == 0)
                    continue;

                var colonIndex = piece.IndexOf(':');
                var paramName = (colonIndex >= 0 ? piece[..colonIndex] : piece).Trim();
                if (!DirectiveParsing.IsValidMacroName(paramName))
                {
                    state.ReportError(lineNumber, $"Preprocessor error: invalid macro parameter '{paramName}'");
                    return;
                }

                parsedParameters.Add(paramName);
            }
        }

        name = candidateName;
        parameters = parsedParameters;
    }
}
