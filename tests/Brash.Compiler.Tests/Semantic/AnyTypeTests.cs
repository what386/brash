using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Preprocessor;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests.Semantic;

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
    public void Semantic_AllowsAnyThroughMacroExpandedExecArguments()
    {
        var diagnostics = Analyze(
            """
            let value: any = 5
            print!(value)
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void Semantic_AllowsExplicitCastFromAny()
    {
        var diagnostics = Analyze(
            """
            let value: any = 5
            print!(value as string)
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    private static DiagnosticBag Analyze(string source)
    {
        var diagnostics = new DiagnosticBag();
        var preprocessed = new BrashPreprocessor().Process(source, diagnostics);
        var input = new AntlrInputStream(preprocessed);
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
