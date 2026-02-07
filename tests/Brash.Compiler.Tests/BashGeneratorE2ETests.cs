using System.Diagnostics;
using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.CodeGen;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests;

public class BashGeneratorE2ETests
{
    [Fact]
    public void E2E_FunctionCall_CanComputeAndPrintValue()
    {
        const string source =
            """
            fn inc(x: int): int
                return x + 1
            end

            let value = inc(41)
            exec("printf", "%s\n", value)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("42", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_ImplMethodsAndEnums_CanDispatchAndReadStructFields()
    {
        const string source =
            """
            enum JobLevel
                Junior,
                Senior
            end

            struct Person
                age: int
                level: JobLevel
            end

            impl Person
                fn age_plus(delta: int): int
                    return self.age + delta
                end

                fn level_name(): JobLevel
                    return self.level
                end
            end

            let person = Person {
                age: 30,
                level: JobLevel.Senior
            }

            let age = person.age_plus(5)
            let level = person.level_name()
            exec("printf", "%s|%s\n", age, level)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("35|Senior", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_MemberAssignmentAndNullCoalesce_WorkForStructBinding()
    {
        const string source =
            """
            struct User
                name: string?
            end

            let mut user = User {
                name: null
            }

            user.name = "Alice"
            let display = user.name ?? "unknown"
            exec("printf", "%s\n", display)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Alice", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_PipeOperator_TransformsCommandOutput()
    {
        const string source =
            """
            let output = exec(cmd("printf", "abc\n") | cmd("tr", "a-z", "A-Z") | cmd("tr", "B", "X"))
            exec("printf", "%s\n", output)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("AXC", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_ExecCanMaterializeCommandVariable()
    {
        const string source =
            """
            let pipeline = cmd("printf", "abc\n") | cmd("tr", "a-z", "A-Z")
            let output = exec(pipeline)
            exec("printf", "%s\n", output)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("ABC", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_CmdSingleStringArgumentWorksAsRawCommandText()
    {
        const string source =
            """
            let output = exec(cmd("printf 'ok\n'"))
            exec("printf", "%s\n", output)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("ok", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_TryCatchThrow_HandlesErrorAndContinues()
    {
        const string source =
            """
            try
                throw "boom"
            catch err
                exec("printf", "caught:%s\n", err)
            end
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("caught:boom", result.StdOut.Trim());
    }

    private static (int ExitCode, string StdOut, string StdErr) CompileAndRun(string source)
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

        var tempScript = Path.Combine(Path.GetTempPath(), $"brash-e2e-{Guid.NewGuid():N}.sh");
        File.WriteAllText(tempScript, bash);

        try
        {
            var startInfo = new ProcessStartInfo("bash", tempScript)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

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
        var input = new AntlrInputStream(source);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        parser.RemoveErrorListeners();
        parser.AddErrorListener(new Brash.Compiler.Diagnostics.DiagnosticErrorListener(diagnostics));

        return parser;
    }
}
