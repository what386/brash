namespace Brash.Compiler.Optimization.Ast;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;

public sealed class AstOptimizer
{
    private readonly AstConstantFolder constantFolder = new();
    private readonly AstDeadCodeEliminator deadCodeEliminator = new();
    private readonly AstExpressionRewriter expressionRewriter;
    private readonly AstStatementRewriter statementRewriter;

    public AstOptimizer()
    {
        expressionRewriter = new AstExpressionRewriter(constantFolder);
        statementRewriter = new AstStatementRewriter(expressionRewriter, constantFolder, deadCodeEliminator);
    }

    public ProgramNode Optimize(ProgramNode program, AstOptimizationOptions? options = null)
    {
        options ??= new AstOptimizationOptions();
        if (!options.Enable)
            return program;

        var propagationState = new Dictionary<string, LiteralExpression>(StringComparer.Ordinal);
        program.Statements = statementRewriter.RewriteStatementList(
            program.Statements,
            options,
            propagationState,
            allowDeadLocalElimination: false);

        return program;
    }
}
