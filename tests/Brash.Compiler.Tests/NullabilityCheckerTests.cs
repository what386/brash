using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests;

public class NullabilityCheckerTests
{
    [Fact]
    public void SemanticAnalyzer_WarnsOnNullableMemberAccessWithoutSafeNavigation()
    {
        var program = ParseProgram(
            """
            struct User
                name: string
            end

            let user: User? = null
            let display = user.name
            """);

        var diagnostics = Analyze(program);

        Assert.Contains(diagnostics.GetWarnings(), d => d.Message.Contains("member access 'name'"));
    }

    [Fact]
    public void SemanticAnalyzer_DoesNotWarnWhenUsingSafeNavigationAndCoalesce()
    {
        var program = ParseProgram(
            """
            struct User
                name: string
            end

            let user: User? = null
            let display = user?.name ?? "unknown"
            """);

        var diagnostics = Analyze(program);

        Assert.DoesNotContain(diagnostics.GetWarnings(), d => d.Message.Contains("member access 'name'"));
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void SemanticAnalyzer_WarnsOnRedundantNullCoalesce()
    {
        var program = ParseProgram(
            """
            let value: int = 5
            let result = value ?? 10
            """);

        var diagnostics = Analyze(program);

        Assert.Contains(diagnostics.GetWarnings(), d => d.Message.Contains("Left side of '??' is not nullable"));
    }

    private static DiagnosticBag Analyze(ProgramNode program)
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);
        analyzer.Analyze(program);
        return diagnostics;
    }

    private static ProgramNode ParseProgram(string source)
    {
        var input = new AntlrInputStream(source);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        var diagnostics = new DiagnosticBag();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new Brash.Compiler.Diagnostics.DiagnosticErrorListener(diagnostics));

        var tree = parser.program();
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));

        var ast = new AstBuilder().VisitProgram(tree);
        return Assert.IsType<ProgramNode>(ast);
    }
}
