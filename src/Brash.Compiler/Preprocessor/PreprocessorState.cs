namespace Brash.Compiler.Preprocessor;

using Brash.Compiler.Diagnostics;

internal sealed class PreprocessorState
{
    public PreprocessorState(DiagnosticBag diagnostics)
    {
        Diagnostics = diagnostics;
    }

    public DiagnosticBag Diagnostics { get; }
    public Dictionary<string, string> Macros { get; } = new(StringComparer.Ordinal);
    public Stack<ConditionalFrame> Frames { get; } = new();

    public bool IsCurrentBranchActive => Frames.All(f => f.CurrentBranchActive);

    public void ReportError(int lineNumber, string message)
    {
        Diagnostics.AddError(message, lineNumber, 0, DiagnosticCodes.SyntaxError);
    }

    public bool TryPeekFrame(int lineNumber, string directive, out ConditionalFrame frame)
    {
        if (Frames.Count == 0)
        {
            ReportError(lineNumber, $"Preprocessor error: '{directive}' without matching conditional block");
            frame = null!;
            return false;
        }

        frame = Frames.Peek();
        return true;
    }
}

