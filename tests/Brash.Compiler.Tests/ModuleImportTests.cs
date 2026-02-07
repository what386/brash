using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests;

public class ModuleImportTests
{
    [Fact]
    public void ModuleLoader_ResolvesNamedImportsFromEntryRoot()
    {
        using var fixture = new ModuleFixture();

        fixture.Write(
            "main.bsh",
            """
            import { greet, API_VERSION } from "lib/tools.bsh"
            exec("printf", "%s\n", greet("Ada"))
            """);

        fixture.Write(
            "lib/tools.bsh",
            """
            pub const API_VERSION = "0.1.0"
            pub fn greet(name: string): string
                return "hi " + name
            end
            """);

        var diagnostics = new DiagnosticBag();
        var ok = ModuleLoader.TryLoadProgram(fixture.Path("main.bsh"), diagnostics, out var program);

        Assert.True(ok, string.Join(Environment.NewLine, diagnostics.GetErrors()));
        Assert.NotNull(program);
        Assert.DoesNotContain(program!.Statements, s => s is ImportStatement);
        Assert.Contains(program.Statements, s => s is FunctionDeclaration { Name: "greet", IsPublic: true });
        Assert.Contains(program.Statements, s => s is VariableDeclaration { Name: "API_VERSION", IsPublic: true });
    }

    [Fact]
    public void ModuleLoader_RejectsImportOfNonPublicSymbol()
    {
        using var fixture = new ModuleFixture();

        fixture.Write(
            "main.bsh",
            """
            import { hidden } from "lib/tools.bsh"
            hidden()
            """);

        fixture.Write(
            "lib/tools.bsh",
            """
            fn hidden()
                exec("printf", "nope\n")
            end
            """);

        var diagnostics = new DiagnosticBag();
        var ok = ModuleLoader.TryLoadProgram(fixture.Path("main.bsh"), diagnostics, out _);

        Assert.False(ok);
        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("is not public"));
    }

    [Fact]
    public void ModuleLoader_ResolvesNestedImportsFromEntryRoot()
    {
        using var fixture = new ModuleFixture();

        fixture.Write(
            "main.bsh",
            """
            import { use_shared } from "pkg/feature.bsh"
            exec("printf", "%s\n", use_shared())
            """);

        fixture.Write(
            "pkg/feature.bsh",
            """
            import { SHARED } from "shared/common.bsh"

            pub fn use_shared(): string
                return SHARED
            end
            """);

        fixture.Write(
            "shared/common.bsh",
            """
            pub const SHARED = "from-root"
            """);

        var diagnostics = new DiagnosticBag();
        var ok = ModuleLoader.TryLoadProgram(fixture.Path("main.bsh"), diagnostics, out var program);

        Assert.True(ok, string.Join(Environment.NewLine, diagnostics.GetErrors()));
        Assert.NotNull(program);
        Assert.Contains(program!.Statements, s => s is VariableDeclaration { Name: "SHARED", IsPublic: true });
        Assert.Contains(program.Statements, s => s is FunctionDeclaration { Name: "use_shared", IsPublic: true });
    }

    [Fact]
    public void SemanticAnalyzer_RejectsPublicNonConstVariable()
    {
        var diagnostics = Analyze(
            """
            pub let x = 1
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Only const declarations can be public"));
    }

    private static DiagnosticBag Analyze(string source)
    {
        var parserDiagnostics = new DiagnosticBag();
        var parser = CreateParser(source, parserDiagnostics);
        var tree = parser.program();
        Assert.False(parserDiagnostics.HasErrors, string.Join(Environment.NewLine, parserDiagnostics.GetErrors()));

        var program = Assert.IsType<ProgramNode>(new AstBuilder().VisitProgram(tree));

        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);
        analyzer.Analyze(program);
        return diagnostics;
    }

    private static BrashParser CreateParser(string source, DiagnosticBag diagnostics)
    {
        var input = new AntlrInputStream(source);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new Brash.Compiler.Diagnostics.DiagnosticErrorListener(diagnostics));

        return parser;
    }

    private sealed class ModuleFixture : IDisposable
    {
        private readonly string root;

        public ModuleFixture()
        {
            root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"brash-module-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
        }

        public string Path(string relative)
        {
            return System.IO.Path.Combine(root, relative);
        }

        public void Write(string relative, string content)
        {
            var path = Path(relative);
            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, content.Replace("\r\n", "\n"));
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
