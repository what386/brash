using System.Diagnostics;
using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.CodeGen;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Preprocessor;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests.Integration;

public class MainEntryPointTests
{
    [Fact]
    public void SemanticAnalyzer_AcceptsMainWithStringArrayArgs()
    {
        var diagnostics = Analyze(
            """
            fn main(args: string[])
                exec("printf", "%s\n", args[0])
            end
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsInvalidMainSignature()
    {
        var diagnostics = Analyze(
            """
            fn main(a: int, b: int)
                return
            end
            """);

        Assert.Contains(diagnostics.GetErrors(),
            d => d.Message.Contains("Function 'main' must have signature"));
    }

    [Fact]
    public void SemanticAnalyzer_AcceptsMainReturningInt()
    {
        var diagnostics = Analyze(
            """
            fn main(args: string[]): int
                return 0
            end
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsMainWithNonIntReturnType()
    {
        var diagnostics = Analyze(
            """
            fn main(): string
                return "bad"
            end
            """);

        Assert.Contains(diagnostics.GetErrors(),
            d => d.Message.Contains("Function 'main' may only return 'int' or 'void'"));
    }

    [Fact]
    public void BashGenerator_CallsMainWithShellArguments()
    {
        var program = ParseProgram(
            """
            fn main(args: string[])
                exec("printf", "%s\n", args[0])
            end
            """);

        var bash = new BashGenerator().Generate(program);

        Assert.Contains("main \"$@\"", bash);
        Assert.Contains("local -a args=(\"$@\")", bash);
    }

    [Fact]
    public void E2E_MainReceivesPositionalArgs()
    {
        const string source =
            """
            fn main(args: string[])
                exec("printf", "%s-%s\n", args[0], args[1])
            end
            """;

        var result = CompileAndRun(source, "alpha", "beta");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("alpha-beta", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_MainIntReturnControlsProcessExitCode()
    {
        const string source =
            """
            fn main(args: string[]): int
                return 7
            end
            """;

        var result = CompileAndRun(source);

        Assert.Equal(7, result.ExitCode);
    }

    private static DiagnosticBag Analyze(string source)
    {
        var program = ParseProgram(source);
        var diagnostics = new DiagnosticBag();
        new SemanticAnalyzer(diagnostics).Analyze(program);
        return diagnostics;
    }

    private static ProgramNode ParseProgram(string source)
    {
        var diagnostics = new DiagnosticBag();
        var parser = CreateParser(source, diagnostics);
        var tree = parser.program();
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
        return Assert.IsType<ProgramNode>(new AstBuilder().VisitProgram(tree));
    }

    private static (int ExitCode, string StdOut, string StdErr) CompileAndRun(string source, params string[] args)
    {
        var parserDiagnostics = new DiagnosticBag();
        var parser = CreateParser(source, parserDiagnostics);
        var tree = parser.program();
        Assert.False(parserDiagnostics.HasErrors, string.Join(Environment.NewLine, parserDiagnostics.GetErrors()));

        var program = Assert.IsType<ProgramNode>(new AstBuilder().VisitProgram(tree));

        var semanticDiagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(semanticDiagnostics);
        analyzer.Analyze(program);
        Assert.False(semanticDiagnostics.HasErrors, string.Join(Environment.NewLine, semanticDiagnostics.GetErrors()));

        var generator = new BashGenerator();
        var bash = generator.Generate(program);
        Assert.Empty(generator.Warnings);

        var tempScript = Path.Combine(Path.GetTempPath(), $"brash-main-e2e-{Guid.NewGuid():N}.sh");
        File.WriteAllText(tempScript, bash);

        try
        {
            var startInfo = new ProcessStartInfo("bash")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(tempScript);
            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            var stdOut = process!.StandardOutput.ReadToEnd();
            var stdErr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, stdOut, stdErr);
        }
        finally
        {
            if (File.Exists(tempScript))
                File.Delete(tempScript);
        }
    }

    private static BrashParser CreateParser(string source, DiagnosticBag diagnostics)
    {
        var preprocessed = new BrashPreprocessor().Process(source, diagnostics);
        var input = new AntlrInputStream(preprocessed);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new Brash.Compiler.Diagnostics.DiagnosticErrorListener(diagnostics));

        return parser;
    }
}
