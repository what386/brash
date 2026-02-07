namespace Brash.Compiler.Preprocessor.Directives;

internal sealed class ShebangDirectiveHandler : IPreprocessorDirectiveHandler
{
    public bool CanHandle(string trimmedLine)
    {
        return trimmedLine.StartsWith("#!", StringComparison.Ordinal);
    }

    public void Apply(DirectiveContext context)
    {
        // Shebang is only valid on the first line for script execution.
        if (context.LineNumber != 1)
        {
            context.State.ReportError(
                context.LineNumber,
                "Preprocessor error: shebang directive must appear on the first line");
        }
    }
}

