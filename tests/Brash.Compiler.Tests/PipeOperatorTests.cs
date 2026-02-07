using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.CodeGen;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests;

public class PipeOperatorTests
{
    [Fact]
    public void SemanticAnalyzer_RejectsPipeWithNonPipableLeftOperand()
    {
        var diagnostics = Analyze(
            """
            let result = 1 | cmd("cat")
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Pipe operator left operand must be"));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsPipeWithNonPipableRightOperand()
    {
        var diagnostics = Analyze(
            """
            let result = cmd("printf", "x") | 123
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Pipe operator right operand must be"));
    }

    [Fact]
    public void CodeGen_EmitsChainedPipeForExpressionAssignment()
    {
        var program = ParseProgram(
            """
            let result = exec(cmd("printf", "abc\n") | cmd("tr", "a-z", "A-Z") | cmd("tr", "B", "X"))
            """);

        var diagnostics = new DiagnosticBag();
        new SemanticAnalyzer(diagnostics).Analyze(program);
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));

        var bash = new BashGenerator().Generate(program);

        Assert.Contains("|", bash);
        Assert.Contains("tr", bash);
        Assert.Contains("result=$(", bash);
    }

    [Fact]
    public void SemanticAnalyzer_RejectsAsyncAndAwaitForNow()
    {
        var diagnostics = Analyze(
            """
            let proc = async exec("sleep", "0")
            let result = await proc
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("async exec(...) is not supported"));
        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("await is not supported"));
    }

    [Fact]
    public void SemanticAnalyzer_AllowsSpawnAssignedToProcessType()
    {
        var diagnostics = Analyze(
            """
            let proc: Process = spawn("sleep", "0")
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    private static DiagnosticBag Analyze(string source)
    {
        var program = ParseProgram(source);
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);
        analyzer.Analyze(program);
        return diagnostics;
    }

    private static ProgramNode ParseProgram(string source)
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

        return Assert.IsType<ProgramNode>(new AstBuilder().VisitProgram(tree));
    }
}
