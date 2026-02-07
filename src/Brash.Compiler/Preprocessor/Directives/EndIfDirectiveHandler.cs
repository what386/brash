namespace Brash.Compiler.Preprocessor.Directives;

internal sealed class EndIfDirectiveHandler : IPreprocessorDirectiveHandler
{
    public bool CanHandle(string trimmedLine)
    {
        return trimmedLine == "#endif";
    }

    public void Apply(DirectiveContext context)
    {
        if (!context.State.TryPeekFrame(context.LineNumber, "#endif", out _))
            return;

        context.State.Frames.Pop();
    }
}

