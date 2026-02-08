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
    public void E2E_StaticImplMethod_CanBeCalledOnType()
    {
        const string source =
            """
            struct PathTools
                value: string
            end

            impl PathTools
                static fn cwd(): string
                    return "root"
                end
            end

            let dir = PathTools.cwd()
            exec("printf", "%s\n", dir)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("root", result.StdOut.Trim());
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
    public void E2E_ValuePipe_ChainsFunctions()
    {
        const string source =
            """
            fn add_two(x: int): int
                return x + 2
            end

            fn double(x: int): int
                return x * 2
            end

            let mut a = 5
            a = a | add_two() | double()
            exec("printf", "%s\n", a)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("14", result.StdOut.Trim());
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
        Assert.EndsWith("ok", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_MapLiteral_IndexReadAndWrite_Work()
    {
        const string source =
            """
            let mut m: map<string, string> = {"name": "brash"}
            m["name"] = "Brash"
            let name = m["name"]
            exec("printf", "%s\n", name)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Brash", result.StdOut.Trim());
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

    [Fact]
    public void E2E_Panic_IsFatalAndNotCatchable()
    {
        const string source =
            """
            try
                panic("boom")
            catch err
                exec("printf", "caught:%s\n", err)
            end

            exec("printf", "after\n")
            """;

        var result = CompileAndRun(source);

        Assert.Equal(1, result.ExitCode);
        Assert.DoesNotContain("caught:", result.StdOut);
        Assert.DoesNotContain("after", result.StdOut);
    }

    [Fact]
    public void E2E_AsyncExec_IsFireAndForget()
    {
        const string source =
            """
            async exec("printf", "async-ignored")
            exec("printf", "done\n")
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("done", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_AsyncSpawnAndAwait_ReturnsCapturedOutput()
    {
        const string source =
            """
            let proc = async spawn("printf", "spawn-ok")
            let output = await proc
            exec("printf", "%s\n", output)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("spawn-ok", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_InterpolatedString_SubstitutesAwaitedVariables()
    {
        const string source =
            """
            let proc1 = async spawn("printf", "one")
            let proc2 = async spawn("printf", "two")
            let res1 = await proc1
            let res2 = await proc2
            let output = $"vals:{res1}-{res2}"
            exec("printf", "%s\n", output)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("vals:one-two", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_CommandArgs_PreserveConcatenatedStringAsSingleArgument()
    {
        const string source =
            """
            let text = "Building " + "Brash"
            exec("printf", "%s\n", text)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Building Brash", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_ShStatement_EmitsRawShellDirectly()
    {
        const string source =
            """
            sh echo inline-1
            sh echo inline-2
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("inline-1\ninline-2", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_ShStatement_CanUseShellVariables()
    {
        const string source =
            """
            let script = "printf '%s\n' dynamic-inline"
            sh eval "$script"
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("dynamic-inline", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_PrintBuiltin_PreservesSpacesInConcatenatedString()
    {
        const string source =
            """
            fn print(text: string)
                exec("printf", "%s\n", text)
            end

            let text = "Building " + "Brash"
            print(text)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Building Brash", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_FunctionCallArgument_PreservesSpacesInNestedExpression()
    {
        const string source =
            """
            fn wrap(msg: string): string
                return "<<" + msg + ">>"
            end

            let text = "value=" + (string)14
            let wrapped = wrap("prefix " + text)
            exec("printf", "%s\n", wrapped)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("<<prefix value=14>>", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_StringBuiltinMethods_WorkOnPrimitives()
    {
        const string source =
            """
            let raw = "  Abc Def  "
            let len = raw.length()
            let trimmed = raw.trim()
            let upper = trimmed.to_upper()
            let lower = upper.to_lower()
            let has = lower.contains("abc")
            let starts = lower.starts_with("abc")
            let ends = lower.ends_with("def")
            let empty = "".is_empty()

            exec("printf", "%s|%s|%s|%s|%s|%s|%s|%s\n", len, trimmed, upper, lower, has, starts, ends, empty)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("11|Abc Def|ABC DEF|abc def|1|1|1|1", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_MultilineString_PreservesLineBreaks()
    {
        const string source =
            """
            let text = [[line1
            line2]]
            exec("printf", "%s\n", text)
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("line1\nline2", result.StdOut.Trim());
    }

    [Fact]
    public void E2E_StringLiteral_DollarSigns_AreNotExpandedByOuterShell()
    {
        const string source =
            """
            let script = "printf '%s\\n' \"$candidate\""
            exec("sh", "-c", script)
            exec("printf", "ok\n")
            """;

        var result = CompileAndRun(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ok", result.StdOut);
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
