using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Optimization.Ast;
using Xunit;

namespace Brash.Compiler.Tests;

public class AstOptimizerTests
{
    [Fact]
    public void Optimize_PropagatesImmutableLiteralConstants()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new FunctionDeclaration
                {
                    Name = "main",
                    Body =
                    {
                        new VariableDeclaration
                        {
                            Kind = VariableDeclaration.VarKind.Let,
                            Name = "x",
                            Value = IntLiteral(5)
                        },
                        new VariableDeclaration
                        {
                            Kind = VariableDeclaration.VarKind.Let,
                            Name = "y",
                            Value = new IdentifierExpression { Name = "x" }
                        },
                        new ReturnStatement
                        {
                            Value = new BinaryExpression
                            {
                                Left = new IdentifierExpression { Name = "y" },
                                Operator = "+",
                                Right = new IdentifierExpression { Name = "x" }
                            }
                        }
                    }
                }
            }
        };

        new AstOptimizer().Optimize(program);

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(program.Statements));
        var returnStatement = Assert.IsType<ReturnStatement>(Assert.Single(function.Body));
        var literal = Assert.IsType<LiteralExpression>(returnStatement.Value);
        Assert.Equal(10, literal.Value);
    }

    [Fact]
    public void Optimize_DoesNotPropagateAcrossMutableAssignment()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new FunctionDeclaration
                {
                    Name = "main",
                    Body =
                    {
                        new VariableDeclaration
                        {
                            Kind = VariableDeclaration.VarKind.Mut,
                            Name = "x",
                            Value = IntLiteral(1)
                        },
                        new Assignment
                        {
                            Target = new IdentifierExpression { Name = "x" },
                            Value = IntLiteral(2)
                        },
                        new VariableDeclaration
                        {
                            Kind = VariableDeclaration.VarKind.Let,
                            Name = "y",
                            Value = new IdentifierExpression { Name = "x" }
                        },
                        new ReturnStatement
                        {
                            Value = new IdentifierExpression { Name = "y" }
                        }
                    }
                }
            }
        };

        new AstOptimizer().Optimize(program, new AstOptimizationOptions
        {
            EnableConstantPropagation = true,
            EnableDeadLocalElimination = false
        });

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(program.Statements));
        var yDeclaration = Assert.IsType<VariableDeclaration>(function.Body[2]);
        Assert.IsType<IdentifierExpression>(yDeclaration.Value);
    }

    [Fact]
    public void Optimize_RemovesUnusedPureLocalDeclarations()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new FunctionDeclaration
                {
                    Name = "main",
                    Body =
                    {
                        new VariableDeclaration
                        {
                            Kind = VariableDeclaration.VarKind.Let,
                            Name = "dead_local",
                            Value = IntLiteral(42)
                        },
                        new VariableDeclaration
                        {
                            Kind = VariableDeclaration.VarKind.Let,
                            Name = "alive",
                            Value = IntLiteral(7)
                        },
                        new ReturnStatement
                        {
                            Value = new IdentifierExpression { Name = "alive" }
                        }
                    }
                }
            }
        };

        new AstOptimizer().Optimize(program);

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(program.Statements));
        Assert.Single(function.Body);
        var returnStatement = Assert.IsType<ReturnStatement>(function.Body[0]);
        var returnLiteral = Assert.IsType<LiteralExpression>(returnStatement.Value);
        Assert.Equal(7, returnLiteral.Value);
    }

    [Fact]
    public void Optimize_KeepsUnusedDeclarationsWithImpureInitializers()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new FunctionDeclaration
                {
                    Name = "main",
                    Body =
                    {
                        new VariableDeclaration
                        {
                            Kind = VariableDeclaration.VarKind.Let,
                            Name = "capture",
                            Value = new FunctionCallExpression
                            {
                                FunctionName = "print",
                                Arguments = { StringLiteral("side effect") }
                            }
                        },
                        new ReturnStatement { Value = IntLiteral(0) }
                    }
                }
            }
        };

        new AstOptimizer().Optimize(program);

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(program.Statements));
        Assert.Equal(2, function.Body.Count);
        var declaration = Assert.IsType<VariableDeclaration>(function.Body[0]);
        Assert.Equal("capture", declaration.Name);
    }

    [Fact]
    public void Optimize_FoldsArithmeticAndStringConcatenation()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new FunctionDeclaration
                {
                    Name = "main",
                    Body =
                    {
                        new ReturnStatement
                        {
                            Value = new BinaryExpression
                            {
                                Left = new BinaryExpression
                                {
                                    Left = IntLiteral(2),
                                    Operator = "+",
                                    Right = IntLiteral(3)
                                },
                                Operator = "+",
                                Right = StringLiteral(" apples")
                            }
                        }
                    }
                }
            }
        };

        new AstOptimizer().Optimize(program);

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(program.Statements));
        var returnStatement = Assert.IsType<ReturnStatement>(Assert.Single(function.Body));
        var literal = Assert.IsType<LiteralExpression>(returnStatement.Value);
        Assert.Equal("5 apples", literal.Value);
    }

    [Fact]
    public void Optimize_SimplifiesConstantIfAndWhileFalse()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new FunctionDeclaration
                {
                    Name = "main",
                    Body =
                    {
                        new IfStatement
                        {
                            Condition = BoolLiteral(false),
                            ThenBlock = { new ExpressionStatement { Expression = StringLiteral("then") } },
                            ElseBlock = new List<Statement> { new ReturnStatement { Value = IntLiteral(11) } }
                        },
                        new WhileLoop
                        {
                            Condition = BoolLiteral(false),
                            Body = { new ReturnStatement { Value = IntLiteral(0) } }
                        }
                    }
                }
            }
        };

        new AstOptimizer().Optimize(program);

        var function = Assert.IsType<FunctionDeclaration>(Assert.Single(program.Statements));
        Assert.Single(function.Body);
        var returnStatement = Assert.IsType<ReturnStatement>(function.Body[0]);
        var literal = Assert.IsType<LiteralExpression>(returnStatement.Value);
        Assert.Equal(11, literal.Value);
    }

    private static LiteralExpression IntLiteral(int value)
    {
        return new LiteralExpression
        {
            Value = value,
            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int }
        };
    }

    private static LiteralExpression StringLiteral(string value)
    {
        return new LiteralExpression
        {
            Value = value,
            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
        };
    }

    private static LiteralExpression BoolLiteral(bool value)
    {
        return new LiteralExpression
        {
            Value = value,
            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Bool }
        };
    }
}
