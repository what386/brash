using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Preprocessor;
using Xunit;

namespace Brash.Compiler.Tests.Frontend;

public class PreprocessorTests
{
    [Fact]
    public void Preprocessor_HandlesDefineIfElseEndif_AndEmitsActiveBranch()
    {
        const string source =
            """
            #define DEBUG 1
            #if DEBUG
            let mode = "debug"
            #else
            let mode = "release"
            #endif
            """;

        var diagnostics = new DiagnosticBag();
        var preprocessed = new BrashPreprocessor().Process(source, diagnostics);
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
        Assert.Contains("let mode = \"debug\"", preprocessed);
        Assert.DoesNotContain("let mode = \"release\"", preprocessed);

        var program = ParseProgram(preprocessed);
        var modeDecl = Assert.IsType<VariableDeclaration>(Assert.Single(program.Statements));
        Assert.Equal("mode", modeDecl.Name);
    }

    [Fact]
    public void Preprocessor_SupportsIfDefIfNDefAndUndef()
    {
        const string source =
            """
            #define FEATURE_X 1
            #ifdef FEATURE_X
            let x = 1
            #endif
            #undef FEATURE_X
            #ifndef FEATURE_X
            let y = 2
            #endif
            """;

        var diagnostics = new DiagnosticBag();
        var preprocessed = new BrashPreprocessor().Process(source, diagnostics);
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));

        var program = ParseProgram(preprocessed);
        Assert.Equal(2, program.Statements.Count);
        Assert.IsType<VariableDeclaration>(program.Statements[0]);
        Assert.IsType<VariableDeclaration>(program.Statements[1]);
    }

    [Fact]
    public void Preprocessor_PreservesLineCountForDiagnosticsStability()
    {
        const string source =
            """
            #define DEBUG 1
            #if DEBUG
            let x = 1
            #endif
            """;

        var diagnostics = new DiagnosticBag();
        var preprocessed = new BrashPreprocessor().Process(source, diagnostics);
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));

        var originalLineCount = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Length;
        var preprocessedLineCount = preprocessed.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Length;
        Assert.Equal(originalLineCount, preprocessedLineCount);
    }

    [Fact]
    public void Preprocessor_ReportsUnmatchedEndif()
    {
        const string source =
            """
            let x = 1
            #endif
            """;

        var diagnostics = new DiagnosticBag();
        _ = new BrashPreprocessor().Process(source, diagnostics);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("'#endif' without matching conditional block"));
    }

    [Fact]
    public void Preprocessor_ReportsMissingEndif()
    {
        const string source =
            """
            #if 1
            let x = 1
            """;

        var diagnostics = new DiagnosticBag();
        _ = new BrashPreprocessor().Process(source, diagnostics);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("missing '#endif'"));
    }

    [Fact]
    public void Preprocessor_AllowsShebangOnFirstLine()
    {
        const string source =
            """
            #!/bin/env brash run
            let x = 1
            """;

        var diagnostics = new DiagnosticBag();
        var preprocessed = new BrashPreprocessor().Process(source, diagnostics);
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));

        var program = ParseProgram(preprocessed);
        var varDecl = Assert.IsType<VariableDeclaration>(Assert.Single(program.Statements));
        Assert.Equal("x", varDecl.Name);
    }

    [Fact]
    public void Preprocessor_RejectsShebangOutsideFirstLine()
    {
        const string source =
            """
            let x = 1
            #!/bin/env brash run
            """;

        var diagnostics = new DiagnosticBag();
        _ = new BrashPreprocessor().Process(source, diagnostics);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("shebang directive must appear on the first line"));
    }

    [Fact]
    public void Preprocessor_ExpandsBlockMacroWithoutParameters()
    {
        const string source =
            """
            #macro SAY_HELLO
            println!("hello")
            #endmacro

            fn main()
                SAY_HELLO!
            end
            """;

        var diagnostics = new DiagnosticBag();
        var preprocessed = new BrashPreprocessor().Process(source, diagnostics);
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
        Assert.Contains("exec(\"printf\", \"%s\\n\", \"hello\")", preprocessed, StringComparison.Ordinal);
        Assert.DoesNotContain("SAY_HELLO!", preprocessed, StringComparison.Ordinal);
    }

    [Fact]
    public void Preprocessor_ExpandsBlockMacroWithParameters()
    {
        const string source =
            """
            #macro ASSERT_EQ(actual: any, expected: any)
            if actual != expected
                throw "assert failed"
            end
            #endmacro

            fn main()
                ASSERT_EQ!(1 + 1, 2)
            end
            """;

        var diagnostics = new DiagnosticBag();
        var preprocessed = new BrashPreprocessor().Process(source, diagnostics);
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
        Assert.Contains("if 1 + 1 != 2", preprocessed, StringComparison.Ordinal);
        Assert.DoesNotContain("ASSERT_EQ!(", preprocessed, StringComparison.Ordinal);
    }

    [Fact]
    public void Preprocessor_DoesNotExpandBlockMacroWithoutBang()
    {
        const string source =
            """
            #macro SAY_HELLO
            println!("hello")
            #endmacro

            fn main()
                SAY_HELLO
            end
            """;

        var diagnostics = new DiagnosticBag();
        var preprocessed = new BrashPreprocessor().Process(source, diagnostics);
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
        Assert.Contains("SAY_HELLO", preprocessed, StringComparison.Ordinal);
    }

    [Fact]
    public void Preprocessor_LoadsPredefinedMacrosFromRegistry()
    {
        const string source =
            """
            fn main()
                println!("hello")
            end
            """;

        var diagnostics = new DiagnosticBag();
        var preprocessed = new BrashPreprocessor().Process(source, diagnostics);
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
        Assert.Contains("exec(\"printf\", \"%s\\n\", \"hello\")", preprocessed, StringComparison.Ordinal);
        Assert.DoesNotContain("println!(", preprocessed, StringComparison.Ordinal);
    }

    [Fact]
    public void Preprocessor_ReportsMissingEndMacro()
    {
        const string source =
            """
            #macro INCOMPLETE(name: string)
            println!(name)
            """;

        var diagnostics = new DiagnosticBag();
        _ = new BrashPreprocessor().Process(source, diagnostics);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("missing '#endmacro'"));
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
