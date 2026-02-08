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
        Assert.Contains("y=$(inc \"${x}\")", bash);
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
    public void BashGenerator_EmitsMapLiteralAndIndexAccess()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "m",
                    Value = new MapLiteral
                    {
                        Entries =
                        {
                            (
                                new LiteralExpression
                                {
                                    Value = "port",
                                    Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                                },
                                new LiteralExpression
                                {
                                    Value = 8080,
                                    Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int }
                                }
                            )
                        }
                    }
                },
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "port",
                    Value = new IndexAccessExpression
                    {
                        Array = new IdentifierExpression { Name = "m" },
                        Index = new LiteralExpression
                        {
                            Value = "port",
                            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                        }
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);
        Assert.Contains("m=$(brash_map_literal \"port\"", bash);
        Assert.Contains("port=$(brash_index_get \"m\" \"port\")", bash);
    }

    [Fact]
    public void BashGenerator_EmitsMapIndexAssignment()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "m",
                    Value = new MapLiteral()
                },
                new Assignment
                {
                    Target = new IndexAccessExpression
                    {
                        Array = new IdentifierExpression { Name = "m" },
                        Index = new LiteralExpression
                        {
                            Value = "name",
                            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                        }
                    },
                    Value = new LiteralExpression
                    {
                        Value = "brash",
                        Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);
        Assert.Contains("m=$(brash_map_literal)", bash);
        Assert.Contains("brash_index_set \"m\" \"name\" \"brash\"", bash);
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
        Assert.Contains("printf '%s\\n' \"boom\" >&2; false", bash);
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
        Assert.DoesNotContain("brash_throw() {", bash);
        Assert.Contains("printf '%s\\n' \"fatal\" >&2; false", bash);
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

    [Fact]
    public void BashGenerator_InlinesPipeSpawnAndAsyncExecHelpers()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "pipeline",
                    Value = new PipeExpression
                    {
                        Left = new CommandExpression
                        {
                            Kind = CommandKind.Cmd,
                            Arguments = { StringLiteral("echo hi") }
                        },
                        Right = new CommandExpression
                        {
                            Kind = CommandKind.Cmd,
                            Arguments = { StringLiteral("tr a-z A-Z") }
                        }
                    }
                },
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "pid",
                    Value = new CommandExpression
                    {
                        Kind = CommandKind.Spawn,
                        Arguments = { new IdentifierExpression { Name = "pipeline" } }
                    }
                },
                new ExpressionStatement
                {
                    Expression = new CommandExpression
                    {
                        Kind = CommandKind.Exec,
                        IsAsync = true,
                        Arguments = { StringLiteral("echo async") }
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);

        Assert.Contains("pipeline=$(printf '%s | %s'", bash);
        Assert.Contains("pid=$(bash -lc", bash);
        Assert.Contains("bash -lc", bash);
        Assert.DoesNotContain("brash_pipe_cmd()", bash);
        Assert.DoesNotContain("brash_spawn_cmd()", bash);
        Assert.DoesNotContain("brash_async_exec_cmd()", bash);
    }

    [Fact]
    public void BashGenerator_FoldsConstantArithmeticAndStringConcat()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "a",
                    Value = new BinaryExpression
                    {
                        Left = IntLiteral(2),
                        Operator = "+",
                        Right = IntLiteral(3)
                    }
                },
                new VariableDeclaration
                {
                    Kind = VariableDeclaration.VarKind.Let,
                    Name = "s",
                    Value = new BinaryExpression
                    {
                        Left = StringLiteral("ab"),
                        Operator = "+",
                        Right = StringLiteral("cd")
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);
        Assert.Contains("a=5", bash);
        Assert.Contains("s=\"abcd\"", bash);
    }

    [Fact]
    public void BashGenerator_PrunesUnusedHelpers_AndUsesExecFastPathForLiteral()
    {
        var program = new ProgramNode
        {
            Statements =
            {
                new ExpressionStatement
                {
                    Expression = new CommandExpression
                    {
                        Kind = CommandKind.Exec,
                        Arguments = { StringLiteral("echo hi") }
                    }
                }
            }
        };

        var bash = new BashGenerator().Generate(program);
        Assert.Contains("bash -lc", bash, StringComparison.Ordinal);
        Assert.DoesNotContain("brash_exec_cmd()", bash, StringComparison.Ordinal);
        Assert.DoesNotContain("brash_map_literal()", bash, StringComparison.Ordinal);
        Assert.DoesNotContain("brash_index_get()", bash, StringComparison.Ordinal);
    }

    private static LiteralExpression StringLiteral(string value)
    {
        return new LiteralExpression
        {
            Value = value,
            Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
        };
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
