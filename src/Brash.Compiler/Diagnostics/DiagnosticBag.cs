namespace Brash.Compiler.Diagnostics;

using Antlr4.Runtime;

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info
}

public class Diagnostic
{
    public DiagnosticSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
}

public class DiagnosticBag
{
    private readonly List<Diagnostic> diagnostics = new();

    public bool HasErrors => diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    public bool HasWarnings => diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);

    public void AddError(string message, int line = 0, int column = 0)
    {
        diagnostics.Add(new Diagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Message = message,
            Line = line,
            Column = column
        });
    }

    public void AddWarning(string message, int line = 0, int column = 0)
    {
        diagnostics.Add(new Diagnostic
        {
            Severity = DiagnosticSeverity.Warning,
            Message = message,
            Line = line,
            Column = column
        });
    }

    public void AddInfo(string message, int line = 0, int column = 0)
    {
        diagnostics.Add(new Diagnostic
        {
            Severity = DiagnosticSeverity.Info,
            Message = message,
            Line = line,
            Column = column
        });
    }

    public IEnumerable<Diagnostic> GetDiagnostics()
    {
        return diagnostics.OrderBy(d => d.Line).ThenBy(d => d.Column);
    }

    public void Clear()
    {
        diagnostics.Clear();
    }
}

// Error listener for ANTLR parser
public class DiagnosticErrorListener : BaseErrorListener
{
    private readonly DiagnosticBag diagnostics;

    public DiagnosticErrorListener(DiagnosticBag diagnostics)
    {
        this.diagnostics = diagnostics;
    }

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        diagnostics.AddError($"Syntax error: {msg}", line, charPositionInLine);
    }
}
