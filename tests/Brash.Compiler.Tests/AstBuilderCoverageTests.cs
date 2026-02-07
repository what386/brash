using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Xunit;

namespace Brash.Compiler.Tests;

public class AstBuilderCoverageTests
{
    [Fact]
    public void AstBuilder_ParsesImplBlockAndMethodBody()
    {
        var program = ParseProgram(
            """
            struct Person
                age: int
            end

            impl Person
                fn is_adult(): bool
                    return self.age >= 18
                end
            end
            """);

        var impl = Assert.IsType<ImplBlock>(program.Statements[1]);
        Assert.Equal("Person", impl.TypeName);

        var method = Assert.Single(impl.Methods);
        Assert.Equal("is_adult", method.Name);
        Assert.Single(method.Body);
        Assert.IsType<ReturnStatement>(method.Body[0]);
    }

    [Fact]
    public void AstBuilder_ParsesImportForms()
    {
        var program = ParseProgram(
            """
            import "utils.bsh"
            import { helper_fn, CONFIG } from "lib/tools.bsh"
            import User from "models/user.bsh"
            """);

        var moduleImport = Assert.IsType<ImportStatement>(program.Statements[0]);
        Assert.Equal("utils.bsh", moduleImport.Module);

        var namedImport = Assert.IsType<ImportStatement>(program.Statements[1]);
        Assert.Equal("lib/tools.bsh", namedImport.FromModule);
        Assert.Equal(new[] { "helper_fn", "CONFIG" }, namedImport.ImportedItems);

        var defaultImport = Assert.IsType<ImportStatement>(program.Statements[2]);
        Assert.Equal("models/user.bsh", defaultImport.FromModule);
        Assert.Equal(new[] { "User" }, defaultImport.ImportedItems);
    }

    [Fact]
    public void AstBuilder_ParsesPipeAwaitAndCommandExpressions()
    {
        var program = ParseProgram(
            """
            let piped = exec("ls") | exec("wc", "-l")
            let proc = await async exec("sleep", "1")
            let listing = cmd("ls", "-la")
            """);

        var piped = Assert.IsType<VariableDeclaration>(program.Statements[0]);
        var pipe = Assert.IsType<PipeExpression>(piped.Value);
        Assert.IsType<CommandExpression>(pipe.Left);
        Assert.IsType<CommandExpression>(pipe.Right);

        var awaited = Assert.IsType<VariableDeclaration>(program.Statements[1]);
        var awaitExpr = Assert.IsType<AwaitExpression>(awaited.Value);
        var asyncCmd = Assert.IsType<CommandExpression>(awaitExpr.Expression);
        Assert.Equal(CommandKind.Exec, asyncCmd.Kind);
        Assert.True(asyncCmd.IsAsync);

        var cmdDecl = Assert.IsType<VariableDeclaration>(program.Statements[2]);
        var cmdExpr = Assert.IsType<CommandExpression>(cmdDecl.Value);
        Assert.Equal(CommandKind.Cmd, cmdExpr.Kind);
        Assert.Equal(2, cmdExpr.Arguments.Count);
    }

    [Fact]
    public void AstBuilder_ParsesTryCatchThrow()
    {
        var program = ParseProgram(
            """
            try
                throw "boom"
            catch err
                err
            end
            """);

        var tryStmt = Assert.IsType<TryStatement>(Assert.Single(program.Statements));
        Assert.Equal("err", tryStmt.ErrorVariable);
        Assert.Single(tryStmt.TryBlock);
        Assert.Single(tryStmt.CatchBlock);
        Assert.IsType<ThrowStatement>(tryStmt.TryBlock[0]);
        Assert.IsType<ExpressionStatement>(tryStmt.CatchBlock[0]);
    }

    [Fact]
    public void AstBuilder_ParsesCollectionAndStructLiterals()
    {
        var program = ParseProgram(
            """
            let arr = [1, 2, 3]
            let m = {"k": 1}
            """);

        var arrDecl = Assert.IsType<VariableDeclaration>(program.Statements[0]);
        var arr = Assert.IsType<ArrayLiteral>(arrDecl.Value);
        Assert.Equal(3, arr.Elements.Count);

        var mapDecl = Assert.IsType<VariableDeclaration>(program.Statements[1]);
        var map = Assert.IsType<MapLiteral>(mapDecl.Value);
        Assert.Single(map.Entries);

    }

    [Fact]
    public void AstBuilder_ParsesMapAndTupleTypes()
    {
        var program = ParseProgram(
            """
            fn pairify(): (int, string)
                return (1, "x")
            end

            let cfg: map<string, int?> = {"port": 8080}
            """);

        var fn = Assert.IsType<FunctionDeclaration>(program.Statements[0]);
        Assert.IsType<TupleType>(fn.ReturnType);

        var cfg = Assert.IsType<VariableDeclaration>(program.Statements[1]);
        var mapType = Assert.IsType<MapType>(cfg.Type);
        Assert.IsType<PrimitiveType>(mapType.KeyType);
        Assert.IsType<NullableType>(mapType.ValueType);
    }

    private static ProgramNode ParseProgram(string source)
    {
        var diagnostics = new DiagnosticBag();
        var parser = CreateParser(source, diagnostics);
        var tree = parser.program();

        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));

        var ast = new AstBuilder().VisitProgram(tree);
        return Assert.IsType<ProgramNode>(ast);
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
