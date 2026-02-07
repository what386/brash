using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;
using Xunit;

namespace Brash.Compiler.Tests;

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

    private static ProgramNode ParseProgram(string source)
    {
        var input = new AntlrInputStream(source);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        var diagnostics = new DiagnosticBag();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new Brash.Compiler.Diagnostics.DiagnosticErrorListener(diagnostics));

        var tree = parser.program();
        Assert.False(diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.GetErrors()));

        var ast = new AstBuilder().VisitProgram(tree);
        return Assert.IsType<ProgramNode>(ast);
    }
}
