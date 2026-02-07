using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.CodeGen;
using Xunit;

namespace Brash.Compiler.Tests;

public class BashGeneratorReadinessTests
{
    [Fact]
    public void BashGenerator_UsesPlainVariableNameForAssignmentTarget()
    {
        var program = new ProgramNode
        {
            Statements =
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
                    Value = new BinaryExpression
                    {
                        Left = new IdentifierExpression { Name = "x" },
                        Operator = "+",
                        Right = IntLiteral(1)
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);

        Assert.Contains("x=1", bash);
        Assert.Contains("x=$(( ${x} + 1 ))", bash);
        Assert.DoesNotContain("${x}=", bash);
    }

    [Fact]
    public void BashGenerator_EmitsFunctionCallExpression()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "y",
                    Value = new FunctionCallExpression
                    {
                        FunctionName = "inc",
                        Arguments = { new IdentifierExpression { Name = "x" } }
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);
        Assert.Contains("y=$(inc ${x})", bash);
    }

    [Fact]
    public void BashGenerator_EmitsMemberAccessAndMemberAssignment()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "name",
                    Value = new MemberAccessExpression
                    {
                        Object = new IdentifierExpression { Name = "user" },
                        MemberName = "name"
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
                        Value = "Alice",
                        Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);

        Assert.Contains("name=${user_name}", bash);
        Assert.Contains("user_name=\"Alice\"", bash);
    }

    [Fact]
    public void BashGenerator_EmitsNullCoalesceForIdentifiers()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "display",
                    Value = new NullCoalesceExpression
                    {
                        Left = new IdentifierExpression { Name = "name" },
                        Right = new LiteralExpression
                        {
                            Value = "unknown",
                            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                        }
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);
        Assert.Contains("display=${name:-\"unknown\"}", bash);
    }

    [Fact]
    public void BashGenerator_EmitsStructLiteralFieldStorage()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "user",
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
                }
            }
        };

        var bash = new BashGenerator().Generate(program);

        Assert.Contains("user=\"user\"", bash);
        Assert.Contains("user__type=\"User\"", bash);
        Assert.Contains("user_name=\"Alice\"", bash);
    }

    [Fact]
    public void BashGenerator_EmitsNestedMemberAccessPath()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "city",
                    Value = new MemberAccessExpression
                    {
                        Object = new MemberAccessExpression
                        {
                            Object = new IdentifierExpression { Name = "user" },
                            MemberName = "address"
                        },
                        MemberName = "city"
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);
        Assert.Contains("city=${user_address_city}", bash);
    }

    [Fact]
    public void BashGenerator_EmitsTryCatchWithErrorCapture()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new TryStatement
                {
                    ErrorVariable = "err",
                    TryBlock =
                    {
                        new ThrowStatement
                        {
                            Value = new LiteralExpression
                            {
                                Value = "boom",
                                Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                            }
                        }
                    },
                    CatchBlock =
                    {
                        new ExpressionStatement
                        {
                            Expression = new FunctionCallExpression
                            {
                                FunctionName = "print",
                                Arguments = { new IdentifierExpression { Name = "err" } }
                            }
                        }
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);
        Assert.Contains("if {", bash);
        Assert.Contains("2>\"${__brash_err_file_", bash);
        Assert.Contains("err=$(cat \"${__brash_err_file_", bash);
        Assert.Contains("brash_throw \"boom\"", bash);
    }

    [Fact]
    public void BashGenerator_EmitsThrowRuntimeCall()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new ThrowStatement
                {
                    Value = new LiteralExpression
                    {
                        Value = "fatal",
                        Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);
        Assert.Contains("brash_throw() {", bash);
        Assert.Contains("brash_throw \"fatal\"", bash);
    }

    [Fact]
    public void BashGenerator_EmitsTupleDestructuringRead()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new TupleVariableDeclaration
                {
                    Elements =
                    {
                        new TupleBindingElement { Name = "thing", IsMutable = true },
                        new TupleBindingElement { Name = "otherthing", IsMutable = false }
                    },
                    Value = new TupleExpression
                    {
                        Elements =
                        {
                            new LiteralExpression
                            {
                                Value = "a",
                                Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                            },
                            new LiteralExpression
                            {
                                Value = "b",
                                Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                            }
                        }
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);
        Assert.Contains("read -r thing otherthing <<<", bash);
    }

    private static LiteralExpression IntLiteral(int value)
    {
        return new LiteralExpression
        {
            Value = value,
            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int }
        };
    }
}
