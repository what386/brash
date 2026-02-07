namespace Brash.Compiler.Preprocessor.Directives;

internal sealed class ElseDirectiveHandler : IPreprocessorDirectiveHandler
{
    public bool CanHandle(string trimmedLine)
    {
        return trimmedLine == "#else";
    }

    public void Apply(DirectiveContext context)
    {
        if (!context.State.TryPeekFrame(context.LineNumber, "#else", out var frame))
            return;

        if (frame.ElseSeen)
        {
            context.State.ReportError(context.LineNumber, "Preprocessor error: duplicate '#else' in conditional block");
            return;
        }

        frame.ElseSeen = true;
        frame.CurrentBranchActive = frame.ParentActive && !frame.ConditionMatched;
        frame.ConditionMatched = true;
    }
}

