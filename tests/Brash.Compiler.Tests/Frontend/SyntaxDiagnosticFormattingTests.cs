using Antlr4.Runtime;
using Brash.Compiler.Diagnostics;
using Xunit;

namespace Brash.Compiler.Tests;

public class SyntaxDiagnosticFormattingTests
{
    [Fact]
    public void ParserErrors_ReportPreprocessorTokensAsUnrecognizedSymbols()
    {
        var diagnostics = Parse(
            """
            #endif
            """);

        var error = Assert.Single(diagnostics.GetErrors());
        Assert.Contains("Unrecognized symbol '#endif'", error.Message);
    }

    [Fact]
    public void ParserErrors_UseConciseUnexpectedTokenMessage()
    {
        var diagnostics = Parse(
            """
            let x = 1
            end
            """);

        var error = Assert.Single(diagnostics.GetErrors());
        Assert.Contains("Unexpected token 'end'", error.Message);
        Assert.DoesNotContain("expecting {", error.Message);
    }

    private static DiagnosticBag Parse(string source)
    {
        var diagnostics = new DiagnosticBag();
        var input = new AntlrInputStream(source);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        parser.RemoveErrorListeners();
        parser.AddErrorListener(new Brash.Compiler.Diagnostics.DiagnosticErrorListener(diagnostics));

        parser.program();
        return diagnostics;
    }
}
