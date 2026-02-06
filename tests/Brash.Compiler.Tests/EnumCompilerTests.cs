using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests;

public class EnumCompilerTests
{
    [Fact]
    public void AstBuilder_ParsesSimpleEnumDeclaration()
    {
        var program = ParseProgram(
            """
            enum Status
                Pending,
                Active,
                Done
            end
            """);

        var enumDecl = Assert.IsType<EnumDeclaration>(Assert.Single(program.Statements));
        Assert.Equal("Status", enumDecl.Name);

        var names = enumDecl.Variants.Select(v => v.Name).ToArray();
        Assert.Equal(new[] { "Pending", "Active", "Done" }, names);
    }

    [Fact]
    public void SemanticAnalyzer_AllowsAssigningEnumVariantToMatchingEnumType()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = new ProgramNode
        {
            Statements =
            {
                new EnumDeclaration
                {
                    Name = "Status",
                    Variants =
                    {
                        new EnumVariant { Name = "Pending" },
                        new EnumVariant { Name = "Active" }
                    }
                },
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "current",
                    Type = new NamedType { Name = "Status" },
                    Value = new MemberAccessExpression
                    {
                        Object = new IdentifierExpression { Name = "Status" },
                        MemberName = "Active"
                    }
                }
            }
        };

        analyzer.Analyze(program);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void SemanticAnalyzer_ReportsUnknownEnumVariant()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = new ProgramNode
        {
            Statements =
            {
                new EnumDeclaration
                {
                    Name = "Status",
                    Variants =
                    {
                        new EnumVariant { Name = "Pending" },
                        new EnumVariant { Name = "Active" }
                    }
                },
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "current",
                    Type = new NamedType { Name = "Status" },
                    Value = new MemberAccessExpression
                    {
                        Object = new IdentifierExpression { Name = "Status" },
                        MemberName = "Missing"
                    }
                }
            }
        };

        analyzer.Analyze(program);

        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("has no variant 'Missing'"));
    }

    [Fact]
    public void SemanticAnalyzer_ReportsDuplicateEnumVariants()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = new ProgramNode
        {
            Statements =
            {
                new EnumDeclaration
                {
                    Name = "Status",
                    Variants =
                    {
                        new EnumVariant { Name = "Active", Line = 2, Column = 4 },
                        new EnumVariant { Name = "Active", Line = 3, Column = 4 }
                    }
                }
            }
        };

        analyzer.Analyze(program);

        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("contains duplicate variant 'Active'"));
    }

    private static ProgramNode ParseProgram(string source)
    {
        var input = new AntlrInputStream(source);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        var diagnostics = new DiagnosticBag();
        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new Brash.Compiler.Diagnostics.DiagnosticErrorListener(diagnostics));

        var tree = parser.program();
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));

        var ast = new AstBuilder().VisitProgram(tree);
        return Assert.IsType<ProgramNode>(ast);
    }
}
