namespace Brash.Compiler.Preprocessor.Directives;

internal sealed class IfNDefDirectiveHandler : IPreprocessorDirectiveHandler
{
    public bool CanHandle(string trimmedLine)
    {
        return trimmedLine.StartsWith("#ifndef ", StringComparison.Ordinal);
    }

    public void Apply(DirectiveContext context)
    {
        var name = context.Trimmed["#ifndef ".Length..].Trim();
        if (!DirectiveParsing.IsValidMacroName(name))
        {
            context.State.ReportError(context.LineNumber, "Preprocessor error: '#ifndef' requires a valid macro name");
            context.State.Frames.Push(new ConditionalFrame(context.LineNumber, context.CurrentActive, conditionTrue: false));
            return;
        }

        var condition = !context.State.Macros.ContainsKey(name);
        context.State.Frames.Push(new ConditionalFrame(context.LineNumber, context.CurrentActive, condition));
    }
}

