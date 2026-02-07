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

    [Fact]
    public void SemanticAnalyzer_RejectsRedefiningBuiltinPanic()
    {
        var diagnostics = Analyze(
            """
            fn panic(message: string)
                exec("printf", "%s\n", message)
            end
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("reserved as a builtin"));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsCallingInstanceMethodAsStatic()
    {
        var diagnostics = Analyze(
            """
            struct PathTools
                value: string
            end

            impl PathTools
                fn basename(): string
                    return self.value
                end
            end

            let name = PathTools.basename()
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("instance method"));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsCallingStaticMethodOnInstance()
    {
        var diagnostics = Analyze(
            """
            struct PathTools
                value: string
            end

            impl PathTools
                static fn cwd(): string
                    return "root"
                end
            end

            let p = PathTools { value: "x" }
            let out = p.cwd()
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("is static and must be called"));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsSelfInStaticMethod()
    {
        var diagnostics = Analyze(
            """
            struct PathTools
                value: string
            end

            impl PathTools
                static fn bad(): string
                    return self.value
                end
            end
            """);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Cannot use 'self' outside of an impl method"));
    }

    [Fact]
    public void ModuleLoader_ResolvesStdNamespaceModule()
    {
        using var fixture = new ModuleFixture();
        using var stdLibScope = new StdLibPathScope(FindStdLibRoot());

        fixture.Write(
            "main.bsh",
            """
            import { join } from "std/paths"
            let out = join("/tmp", "file.txt")
            exec("printf", "%s\n", out)
            """);

        var diagnostics = new DiagnosticBag();
        var ok = ModuleLoader.TryLoadProgram(fixture.Path("main.bsh"), diagnostics, out var program);

        Assert.True(ok, string.Join(Environment.NewLine, diagnostics.GetErrors()));
        Assert.NotNull(program);
        Assert.Contains(program!.Statements, s => s is FunctionDeclaration { Name: "join", IsPublic: true });
    }

    [Fact]
    public void ModuleLoader_ResolvesStdRootModule()
    {
        using var fixture = new ModuleFixture();
        using var stdLibScope = new StdLibPathScope(FindStdLibRoot());

        fixture.Write(
            "main.bsh",
            """
            import { STD_VERSION } from "std"
            exec("printf", "%s\n", STD_VERSION)
            """);

        var diagnostics = new DiagnosticBag();
        var ok = ModuleLoader.TryLoadProgram(fixture.Path("main.bsh"), diagnostics, out var program);

        Assert.True(ok, string.Join(Environment.NewLine, diagnostics.GetErrors()));
        Assert.NotNull(program);
        Assert.Contains(program!.Statements, s => s is VariableDeclaration { Name: "STD_VERSION", IsPublic: true });
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

    private static string FindStdLibRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current != null)
            {
                var candidate = System.IO.Path.Combine(current.FullName, "src", "Brash.StandardLibrary", "StdLib");
                if (Directory.Exists(candidate))
                    return candidate;

                var legacyCandidate = System.IO.Path.Combine(current.FullName, "src", "stdlib");
                if (Directory.Exists(legacyCandidate))
                    return legacyCandidate;

                candidate = System.IO.Path.Combine(current.FullName, "Brash.StandardLibrary", "StdLib");
                if (Directory.Exists(candidate))
                    return candidate;

                current = current.Parent;
            }
        }

        throw new InvalidOperationException("Unable to locate Brash standard library root for stdlib import tests.");
    }

    private sealed class StdLibPathScope : IDisposable
    {
        private readonly string? previous;

        public StdLibPathScope(string path)
        {
            previous = Environment.GetEnvironmentVariable("BRASH_STDLIB_PATH");
            Environment.SetEnvironmentVariable("BRASH_STDLIB_PATH", path);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("BRASH_STDLIB_PATH", previous);
        }
    }
}
