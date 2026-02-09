using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Preprocessor;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests.Semantic;

public class MutabilityRulesTests
{
    [Fact]
    public void Parser_ParsesLetMutVariableDeclaration()
    {
        var program = ParseProgram(
            """
            let mut x: int = 100
            x = 200
            """);

        var varDecl = Assert.IsType<VariableDeclaration>(program.Statements[0]);
        Assert.Equal(VariableDeclaration.VarKind.Mut, varDecl.Kind);
    }

    [Fact]
    public void Parser_ParsesMutableFunctionParameter()
    {
        var program = ParseProgram(
            """
            fn bump(mut value: int): int
                value = value + 1
                return value
            end
            """);

        var fn = Assert.IsType<FunctionDeclaration>(Assert.Single(program.Statements));
        var param = Assert.Single(fn.Parameters);
        Assert.True(param.IsMutable);
    }

    [Fact]
    public void SemanticAnalyzer_RejectsReassigningImmutableParameter()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = ParseProgram(
            """
            fn bump(value: int): int
                value = value + 1
                return value
            end
            """);

        analyzer.Analyze(program);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("mark parameter as 'mut'"));
    }

    [Fact]
    public void SemanticAnalyzer_AllowsReassigningMutableParameter()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = ParseProgram(
            """
            fn bump(mut value: int): int
                value = value + 1
                return value
            end
            """);

        analyzer.Analyze(program);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsMutatingFieldOnImmutableStructBinding()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = new ProgramNode
        {
            Statements =
            {
                new StructDeclaration
                {
                    Name = "User",
                    Fields =
                    {
                        new FieldDeclaration
                        {
                            Name = "name",
                            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                        }
                    }
                },
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "user",
                    Type = new NamedType { Name = "User" },
                    Value = new StructLiteral
                    {
                        TypeName = "User",
                        Fields =
                        {
                            ("name", new LiteralExpression
                            {
                                Value = "Alice",
                                Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                            })
                        }
                    }
                },
                new Assignment
                {
                    Target = new MemberAccessExpression
                    {
                        Object = new IdentifierExpression { Name = "user" },
                        MemberName = "name"
                    },
                    Value = new LiteralExpression
                    {
                        Value = "Bob",
                        Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                    }
                }
            }
        };

        analyzer.Analyze(program);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("immutable variable 'user'"));
    }

    [Fact]
    public void SemanticAnalyzer_AllowsMutatingFieldOnMutableStructBinding()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = new ProgramNode
        {
            Statements =
            {
                new StructDeclaration
                {
                    Name = "User",
                    Fields =
                    {
                        new FieldDeclaration
                        {
                            Name = "name",
                            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                        }
                    }
                },
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Mut,
                    Name = "user",
                    Type = new NamedType { Name = "User" },
                    Value = new StructLiteral
                    {
                        TypeName = "User",
                        Fields =
                        {
                            ("name", new LiteralExpression
                            {
                                Value = "Alice",
                                Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                            })
                        }
                    }
                },
                new Assignment
                {
                    Target = new MemberAccessExpression
                    {
                        Object = new IdentifierExpression { Name = "user" },
                        MemberName = "name"
                    },
                    Value = new LiteralExpression
                    {
                        Value = "Bob",
                        Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                    }
                }
            }
        };

        analyzer.Analyze(program);

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));
    }

    [Fact]
    public void SemanticAnalyzer_RespectsTupleDestructuringElementMutability()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = ParseProgram(
            """
            let (mut thing, otherthing) = ("a", "b")
            thing = "updated"
            otherthing = "nope"
            """);

        analyzer.Analyze(program);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("immutable variable 'otherthing'"));
        Assert.DoesNotContain(diagnostics.GetErrors(), d => d.Message.Contains("immutable variable 'thing'"));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsTupleDestructuringArityMismatch()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = ParseProgram(
            """
            let (mut a, b) = ("x", "y", "z")
            """);

        analyzer.Analyze(program);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Tuple destructuring arity mismatch"));
    }

    [Fact]
    public void SemanticAnalyzer_RejectsTupleDestructuringFromNonTupleValue()
    {
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);

        var program = ParseProgram(
            """
            let (mut a, b) = 123
            """);

        analyzer.Analyze(program);

        Assert.Contains(diagnostics.GetErrors(), d => d.Message.Contains("Tuple destructuring requires a tuple value"));
    }

    private static ProgramNode ParseProgram(string source)
    {
        var diagnostics = new DiagnosticBag();
        var preprocessed = new BrashPreprocessor().Process(source, diagnostics);
        var input = new AntlrInputStream(preprocessed);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        parser.RemoveErrorListeners();
        parser.AddErrorListener(new Brash.Compiler.Diagnostics.DiagnosticErrorListener(diagnostics));

        var tree = parser.program();
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));

        var ast = new AstBuilder().VisitProgram(tree);
        return Assert.IsType<ProgramNode>(ast);
    }
}
