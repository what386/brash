namespace Brash.Compiler.Preprocessor.Directives;

internal sealed class IfDefDirectiveHandler : IPreprocessorDirectiveHandler
{
    public bool CanHandle(string trimmedLine)
    {
        return trimmedLine.StartsWith("#ifdef ", StringComparison.Ordinal);
    }

    public void Apply(DirectiveContext context)
    {
        var name = context.Trimmed["#ifdef ".Length..].Trim();
        if (!DirectiveParsing.IsValidMacroName(name))
        {
            context.State.ReportError(context.LineNumber, "Preprocessor error: '#ifdef' requires a valid macro name");
            context.State.Frames.Push(new ConditionalFrame(context.LineNumber, context.CurrentActive, conditionTrue: false));
            return;
        }

        var condition = context.State.Macros.ContainsKey(name);
        context.State.Frames.Push(new ConditionalFrame(context.LineNumber, context.CurrentActive, condition));
    }
}

