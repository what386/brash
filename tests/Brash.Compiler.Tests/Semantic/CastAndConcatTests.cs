using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.CodeGen;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests;

public class CastAndConcatTests
{
    [Fact]
    public void Parser_BuildsCastExpressionAstNode()
    {
        const string source = "let text = 5 as string";

        var program = ParseProgram(source);
        var declaration = Assert.IsType<Brash.Compiler.Ast.Statements.VariableDeclaration>(program.Statements[0]);
        var cast = Assert.IsType<CastExpression>(declaration.Value);
        var target = Assert.IsType<PrimitiveType>(cast.TargetType);

        Assert.Equal(PrimitiveType.Kind.String, target.PrimitiveKind);
    }

    [Fact]
    public void Semantic_AllowsExplicitPrimitiveCast()
    {
        var diagnostics = Analyze(
            """
            let text = 5 as string
            let n = 3.14 as int
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void Semantic_RejectsInvalidCast()
    {
        var diagnostics = Analyze(
            """
            struct Person
                name: string
            end

            let person = Person { name: "Alice" }
            let bad = person as int
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Cannot cast value of type 'Person' to 'int'"));
    }

    [Fact]
    public void CodeGen_ConcatenatesStringVariablesAndLiteralsWithPlus()
    {
        const string source =
            """
            fn greet(name: string, greeting: string = "Hello"): string
                return greeting + "_" + name
            end

            let msg = greet("Brash", "Hello")
            exec("printf", "%s\n", msg)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Hello_Brash", result.StdOut.Trim());
    }

    [Fact]
    public void Semantic_AllowsStringSplitReturningStringArrayLikeValue()
    {
        var diagnostics = Analyze(
            """
            let parts = "a,b,c".split(",")
            let first = parts[0]
            print(first)
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void CodeGen_SupportsStringSplitAndIndexAccess()
    {
        const string source =
            """
            let parts = "alpha,beta,gamma".split(",")
            print(parts[1])
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("beta", result.StdOut.Trim());
    }

    [Fact]
    public void Semantic_AllowsStringSubstring()
    {
        var diagnostics = Analyze(
            """
            let text = "abcdef".substring(1, 4)
            print(text)
            """);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void CodeGen_SupportsStringSubstring()
    {
        const string source =
            """
            print("abcdef".substring(1, 4))
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("bcd", result.StdOut.Trim());
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

    private static (int ExitCode, string StdOut, string StdErr) CompileAndRun(string source)
    {
        var program = ParseProgram(source);

        var semanticDiagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(semanticDiagnostics);
        analyzer.Analyze(program);
        Assert.False(semanticDiagnostics.HasErrors, string.Join(Environment.NewLine, semanticDiagnostics.GetErrors()));

        var generator = new BashGenerator();
        var bash = generator.Generate(program);
        Assert.Empty(generator.Warnings);

        var tempScript = Path.Combine(Path.GetTempPath(), $"brash-cast-concat-{Guid.NewGuid():N}.sh");
        File.WriteAllText(tempScript, bash);

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo("bash", tempScript)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
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
