namespace Brash.Compiler.Preprocessor.Directives;

internal sealed class DefineDirectiveHandler : IPreprocessorDirectiveHandler
{
    public bool CanHandle(string trimmedLine)
    {
        return trimmedLine.StartsWith("#define ", StringComparison.Ordinal);
    }

    public void Apply(DirectiveContext context)
    {
        if (!context.CurrentActive)
            return;

        var rest = context.Trimmed["#define ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(rest))
        {
            context.State.ReportError(context.LineNumber, "Preprocessor error: '#define' requires a macro name");
            return;
        }

        var splitIndex = DirectiveParsing.FindFirstWhitespace(rest);
        var name = splitIndex < 0 ? rest : rest[..splitIndex];
        var value = splitIndex < 0 ? "1" : rest[(splitIndex + 1)..].Trim();

        if (!DirectiveParsing.IsValidMacroName(name))
        {
            context.State.ReportError(context.LineNumber, $"Preprocessor error: invalid macro name '{name}'");
            return;
        }

        context.State.Macros[name] = value;
    }
}

