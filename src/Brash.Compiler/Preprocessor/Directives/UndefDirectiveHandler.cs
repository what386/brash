namespace Brash.Compiler.Preprocessor.Directives;

internal sealed class UndefDirectiveHandler : IPreprocessorDirectiveHandler
{
    public bool CanHandle(string trimmedLine)
    {
        return trimmedLine.StartsWith("#undef ", StringComparison.Ordinal);
    }

    public void Apply(DirectiveContext context)
    {
        if (!context.CurrentActive)
            return;

        var name = context.Trimmed["#undef ".Length..].Trim();
        if (!DirectiveParsing.IsValidMacroName(name))
        {
            context.State.ReportError(context.LineNumber, "Preprocessor error: '#undef' requires a valid macro name");
            return;
        }

        context.State.Macros.Remove(name);
    }
}

