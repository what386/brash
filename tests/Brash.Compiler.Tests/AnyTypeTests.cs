using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests;

public class AnyTypeTests
{
    [Fact]
    public void Semantic_AllowsAssigningConcreteValuesToAny()
    {
        var diagnostics = Analyze(
            """
            let value: any = 5
            let other: any = "text"
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void Semantic_RequiresExplicitCastFromAnyToString()
    {
        var diagnostics = Analyze(
            """
            fn print(text: string)
                exec("echo", text)
            end

            let value: any = 5
            print(value)
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Argument 1 to 'print': expected 'string', got 'any'"));
    }

    [Fact]
    public void Semantic_AllowsExplicitCastFromAny()
    {
        var diagnostics = Analyze(
            """
            fn print(text: string)
                exec("echo", text)
            end

            let value: any = 5
            print((string)value)
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    private static DiagnosticBag Analyze(string source)
    {
        var diagnostics = new DiagnosticBag();
        var input = new AntlrInputStream(source);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        parser.RemoveErrorListeners();
        parser.AddErrorListener(new Brash.Compiler.Diagnostics.DiagnosticErrorListener(diagnostics));

        var tree = parser.program();
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));

        var program = Assert.IsType<ProgramNode>(new AstBuilder().VisitProgram(tree));
        var semanticDiagnostics = new DiagnosticBag();
        new SemanticAnalyzer(semanticDiagnostics).Analyze(program);
        return semanticDiagnostics;
    }
}
