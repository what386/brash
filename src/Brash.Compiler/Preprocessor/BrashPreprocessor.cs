namespace Brash.Compiler.Preprocessor;

using System.Text;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Preprocessor.Directives;

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
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var output = new StringBuilder(source.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;
            var trimmed = line.TrimStart();
            var isDirective = trimmed.StartsWith('#');
            var currentActive = state.IsCurrentBranchActive;

            if (!isDirective)
            {
                output.Append(currentActive ? macroExpander.Expand(line, state.Macros) : string.Empty);
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

        return output.ToString();
    }
}
