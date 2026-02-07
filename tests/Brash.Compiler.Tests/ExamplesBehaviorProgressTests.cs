using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests;

public class ExamplesBehaviorProgressTests
{
    public static IEnumerable<object[]> ExampleSyntaxSamples()
    {
        yield return new object[]
        {
            "basics_variables_nullable_and_coalesce",
            """
            let middle_name: string? = null
            let display_name = middle_name ?? "No middle name"
            """
        };

        yield return new object[]
        {
            "functions_optional_parameter_and_return",
            """
            fn greet_custom(name: string, greeting: string = "Hello"): string
                return greeting + ", " + name
            end
            """
        };

        yield return new object[]
        {
            "control_flow_if_elif_else",
            """
            if score >= 90
                print("Grade: A")
            elif score >= 80
                print("Grade: B")
            else
                print("Grade: F")
            end
            """
        };

        yield return new object[]
        {
            "control_flow_for_step_and_while",
            """
            for i in 0..10 step 2
                print(i)
            end

            let mut counter = 0
            while counter < 5
                counter = counter + 1
            end
            """
        };

        yield return new object[]
        {
            "data_structures_struct_record_enum_impl",
            """
            struct Person
                name: string
                age: int
            end

            record Config
                host: string
                port: int
            end

            enum BuildMode
                Debug,
                Release
            end

            impl Person
                fn is_adult(): bool
                    return self.age >= 18
                end
            end
            """
        };

        yield return new object[]
        {
            "collections_array_map_indexing",
            """
            let numbers: int[] = [1, 2, 3]
            let config: map<string, string> = {"host": "localhost"}
            let first = numbers[0]
            let host = config["host"]
            """
        };

        yield return new object[]
        {
            "shell_and_pipe_operator",
            """
            let result = exec("ls") | exec("grep", ".bsh") | exec("wc", "-l")
            """
        };

        yield return new object[]
        {
            "error_handling_try_catch_throw",
            """
            try
                throw "Division by zero"
            catch err
                print(err)
            end
            """
        };

        yield return new object[]
        {
            "import_syntax",
            """
            import "utils.bsh"
            import { helper_fn, CONFIG } from "lib/tools.bsh"
            import User from "models/user.bsh"
            """
        };

        yield return new object[]
        {
            "preprocessor_directives",
            """
            #define DEBUG 1
            #if DEBUG
                print("Debug mode")
            #endif
            """
        };

        yield return new object[]
        {
            "async_await_syntax",
            """
            async fn long_task(name: string): string
                return name
            end

            let proc = async("sleep", "1")
            let result = await proc
            """
        };
    }

    [Theory]
    [MemberData(nameof(ExampleSyntaxSamples))]
    public void ExampleBehaviorSamples_ParseWithoutSyntaxErrors(string behavior, string source)
    {
        var diagnostics = new DiagnosticBag();
        var parser = CreateParser(source, diagnostics);

        parser.program();

        Assert.False(diagnostics.HasErrors, $"Behavior '{behavior}' had syntax errors:{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics.GetErrors())}");
    }

    [Fact]
    public void SemanticAnalyzer_RejectsReassigningImmutableVariable()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = new ProgramNode
        {
            Statements =
            {
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "x",
                    Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int },
                    Value = new LiteralExpression
                    {
                        Value = 5,
                        Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int }
                    }
                },
                new Assignment
                {
                    Target = new IdentifierExpression { Name = "x" },
                    Value = new LiteralExpression
                    {
                        Value = 10,
                        Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int }
                    }
                }
            }
        };

        analyzer.Analyze(program);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Cannot assign to immutable variable 'x'"));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsBreakOutsideLoop()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = new ProgramNode
        {
            Statements = { new BreakStatement() }
        };

        analyzer.Analyze(program);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("BreakStatement outside of loop"));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsDuplicateTopLevelTypes()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = new ProgramNode
        {
            Statements =
            {
                new StructDeclaration
                {
                    Name = "Person",
                    Fields = { new FieldDeclaration { Name = "name", Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String } } }
                },
                new RecordDeclaration
                {
                    Name = "Person",
                    Fields = { new FieldDeclaration { Name = "name", Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String } } }
                }
            }
        };

        analyzer.Analyze(program);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Type 'Person' is already defined"));
    }

    [Fact]
    public void ExampleDataStructuresSnippet_BuildsEnumDeclarationInAst()
    {
        const string source =
            """
            enum JobLevel
                Intern,
                Junior,
                Senior
            end
            """;

        var diagnostics = new DiagnosticBag();
        var parser = CreateParser(source, diagnostics);
        var tree = parser.program();
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));

        var program = Assert.IsType<ProgramNode>(new AstBuilder().VisitProgram(tree));
        var enumDecl = Assert.IsType<EnumDeclaration>(Assert.Single(program.Statements));

        Assert.Equal("JobLevel", enumDecl.Name);
        Assert.Equal(new[] { "Intern", "Junior", "Senior" }, enumDecl.Variants.Select(v => v.Name));
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
