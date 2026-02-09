namespace Brash.Compiler.Preprocessor.Directives;

internal sealed class IfDirectiveHandler : IPreprocessorDirectiveHandler
{
    public bool CanHandle(string trimmedLine)
    {
        return trimmedLine.StartsWith("#if ", StringComparison.Ordinal);
    }

    public void Apply(DirectiveContext context)
    {
        var expression = context.Trimmed["#if ".Length..].Trim();
        if (expression.Length == 0)
        {
            context.State.ReportError(context.LineNumber, "Preprocessor error: '#if' requires a condition expression");
            context.State.Frames.Push(new ConditionalFrame(context.LineNumber, context.CurrentActive, conditionTrue: false));
            return;
        }

        var expanded = context.MacroExpander.Expand(expression, context.State.Macros, context.State.BlockMacros);
        var condition = false;
        try
        {
            condition = new ConditionParser(expanded, context.State.Macros).Parse();
        }
        catch (InvalidOperationException ex)
        {
            context.State.ReportError(
                context.LineNumber,
                $"Preprocessor error: invalid #if expression '{expression}': {ex.Message}");
        }

        context.State.Frames.Push(new ConditionalFrame(context.LineNumber, context.CurrentActive, condition));
    }
}
