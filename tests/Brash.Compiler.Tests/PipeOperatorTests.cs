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

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Pipe operator right operand must be a callable stage"));
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
    public void SemanticAnalyzer_AllowsValuePipeWhenFunctionPreservesType()
    {
        var diagnostics = Analyze(
            """
            fn add_two(x: int): int
                return x + 2
            end

            fn double(x: int): int
                return x * 2
            end

            let mut a = 5
            a = a | add_two() | double()
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsValuePipeWhenStageChangesType()
    {
        var diagnostics = Analyze(
            """
            fn to_text(x: int): string
                return "x"
            end

            let mut a = 5
            a = a | to_text()
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("must preserve type"));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsAwaitOnAsyncExecResult()
    {
        var diagnostics = Analyze(
            """
            let proc = async exec("printf", "ok")
            let result = await proc
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("await expects a Process handle"));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsAwaitOnNonProcess()
    {
        var diagnostics = Analyze(
            """
            let x = 1
            let result = await x
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("await expects a Process handle"));
    }

    [Fact]
    public void SemanticAnalyzer_AllowsAsyncSpawnAndAwait()
    {
        var diagnostics = Analyze(
            """
            let proc = async spawn("printf", "ok")
            let result = await proc
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void SemanticAnalyzer_FailsFast_OnMapLiteral()
    {
        var diagnostics = Analyze(
            """
            try
                throw "boom"
            catch err
                print(err)
            end

            let m = {"k": 1}
            """);

        var errors = diagnostics.GetErrors().ToList();
        Assert.Contains(errors, d => d.Message.Contains("Feature 'map literal code generation' is not supported"));
    }

    [Fact]
    public void SemanticAnalyzer_AllowsTryCatchThrow()
    {
        var diagnostics = Analyze(
            """
            try
                throw "boom"
            catch err
                print(err)
            end
            """);

        Assert.DoesNotContain(diagnostics.GetErrors(), d => d.Message.Contains("Feature 'try/catch' is not supported"));
        Assert.DoesNotContain(diagnostics.GetErrors(), d => d.Message.Contains("Feature 'throw' is not supported"));
    }

    [Fact]
    public void SemanticAnalyzer_AllowsRangeForLoop_WithOptionalStep()
    {
        var diagnostics = Analyze(
            """
            for i in 0..5
                exec("printf", "%s\n", i)
            end

            for j in 10..0 step -2
                exec("printf", "%s\n", j)
            end
            """);

        Assert.DoesNotContain(diagnostics.GetErrors(), d => d.Message.Contains("Feature 'range value code generation' is not supported"));
    }

    [Fact]
    public void SemanticAnalyzer_StillRejectsRangeAsStandaloneValue()
    {
        var diagnostics = Analyze(
            """
            let values = 0..5
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Feature 'range value code generation' is not supported"));
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
