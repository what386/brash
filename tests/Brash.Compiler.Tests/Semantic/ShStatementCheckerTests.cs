using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests;

public class ShStatementCheckerTests
{
    [Fact]
    public void ShStatement_DottedParameterExpansion_EmitsWarningOnly()
    {
        var diagnostics = Analyze(
            """
            sh echo "${user.name}"
            """);

        Assert.False(diagnostics.HasErrors);
        var warning = Assert.Single(diagnostics.GetWarnings());
        Assert.Equal(DiagnosticCodes.SuspiciousShInterpolation, warning.Code);
    }

    [Fact]
    public void ShStatement_FlatParameterExpansion_DoesNotWarn()
    {
        var diagnostics = Analyze(
            """
            sh echo "${user_name}"
            """);

        Assert.False(diagnostics.HasErrors);
        Assert.Empty(diagnostics.GetWarnings());
    }

    private static DiagnosticBag Analyze(string source)
    {
        var parserDiagnostics = new DiagnosticBag();
        var parser = CreateParser(source, parserDiagnostics);
        var tree = parser.program();
        Assert.False(parserDiagnostics.HasErrors, string.Join(Environment.NewLine, parserDiagnostics.GetErrors()));

        var program = Assert.IsType<ProgramNode>(new AstBuilder().VisitProgram(tree));
        var diagnostics = new DiagnosticBag();
        new SemanticAnalyzer(diagnostics).Analyze(program);
        return diagnostics;
    }

    private static BrashParser CreateParser(string source, DiagnosticBag diagnostics)
    {
        var input = new AntlrInputStream(source);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        parser.RemoveErrorListeners();
        parser.AddErrorListener(new Brash.Compiler.Diagnostics.DiagnosticErrorListener(diagnostics));
        return parser;
    }
}
