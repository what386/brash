namespace Brash.Compiler.Optimization.Ast;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;

internal sealed class AstStatementRewriter
{
    private readonly AstExpressionRewriter expressionRewriter;
    private readonly AstConstantFolder constantFolder;
    private readonly AstDeadCodeEliminator deadCodeEliminator;

    public AstStatementRewriter(
        AstExpressionRewriter expressionRewriter,
        AstConstantFolder constantFolder,
        AstDeadCodeEliminator deadCodeEliminator)
    {
        this.expressionRewriter = expressionRewriter;
        this.constantFolder = constantFolder;
        this.deadCodeEliminator = deadCodeEliminator;
    }

    public List<Statement> RewriteStatementList(
        List<Statement> statements,
        AstOptimizationOptions options,
        Dictionary<string, LiteralExpression> constantsInScope,
        bool allowDeadLocalElimination)
    {
        var rewritten = new List<Statement>(statements.Count);
        var currentConstants = new Dictionary<string, LiteralExpression>(constantsInScope, StringComparer.Ordinal);

        foreach (var statement in statements)
        {
            var rewrittenStatement = RewriteStatement(statement, options, currentConstants);
            var simplifiedStatements = SimplifyStatement(rewrittenStatement, options);
            rewritten.AddRange(simplifiedStatements);
        }

        if (allowDeadLocalElimination && options.EnableDeadLocalElimination)
            return deadCodeEliminator.EliminateDeadLocals(rewritten);

        return rewritten;
    }

    private IEnumerable<Statement> SimplifyStatement(Statement statement, AstOptimizationOptions options)
    {
        if (!options.EnableControlFlowSimplification)
            return new[] { statement };

        if (statement is WhileLoop whileLoop &&
            constantFolder.TryEvaluateCondition(whileLoop.Condition, out var whileCondition) &&
            !whileCondition)
        {
            return Array.Empty<Statement>();
        }

        if (statement is not IfStatement ifStatement)
            return new[] { statement };

        if (!constantFolder.TryEvaluateCondition(ifStatement.Condition, out var condition))
            return new[] { statement };

        if (condition)
            return ifStatement.ThenBlock;

        for (int i = 0; i < ifStatement.ElifClauses.Count; i++)
        {
            var elif = ifStatement.ElifClauses[i];
            if (!constantFolder.TryEvaluateCondition(elif.Condition, out var elifCondition))
            {
                var rewrittenIf = new IfStatement
                {
                    Line = ifStatement.Line,
                    Column = ifStatement.Column,
                    Condition = elif.Condition,
                    ThenBlock = elif.Block,
                    ElifClauses = ifStatement.ElifClauses.Skip(i + 1).ToList(),
                    ElseBlock = ifStatement.ElseBlock
                };
                return new[] { rewrittenIf };
            }

            if (elifCondition)
                return elif.Block;
        }

        return ifStatement.ElseBlock?.AsEnumerable() ?? Enumerable.Empty<Statement>();
    }

    private Statement RewriteStatement(
        Statement statement,
        AstOptimizationOptions options,
        Dictionary<string, LiteralExpression> constantsInScope)
    {
        switch (statement)
        {
            case VariableDeclaration variableDeclaration:
                variableDeclaration.Value = expressionRewriter.RewriteExpression(variableDeclaration.Value, options, constantsInScope);
                TrackDeclaration(variableDeclaration, constantsInScope, options);
                return variableDeclaration;

            case TupleVariableDeclaration tupleVariableDeclaration:
                tupleVariableDeclaration.Value = expressionRewriter.RewriteExpression(tupleVariableDeclaration.Value, options, constantsInScope);
                foreach (var element in tupleVariableDeclaration.Elements)
                    constantsInScope.Remove(element.Name);
                return tupleVariableDeclaration;

            case Assignment assignment:
                assignment.Value = expressionRewriter.RewriteExpression(assignment.Value, options, constantsInScope);
                assignment.Target = expressionRewriter.RewriteAssignmentTarget(assignment.Target, options, constantsInScope);
                InvalidateAssignedTarget(assignment.Target, constantsInScope);
                return assignment;

            case IfStatement ifStatement:
                ifStatement.Condition = expressionRewriter.RewriteExpression(ifStatement.Condition, options, constantsInScope);
                ifStatement.ThenBlock = RewriteStatementList(
                    ifStatement.ThenBlock,
                    options,
                    new Dictionary<string, LiteralExpression>(constantsInScope, StringComparer.Ordinal),
                    allowDeadLocalElimination: true);

                foreach (var elif in ifStatement.ElifClauses)
                {
                    elif.Condition = expressionRewriter.RewriteExpression(elif.Condition, options, constantsInScope);
                    elif.Block = RewriteStatementList(
                        elif.Block,
                        options,
                        new Dictionary<string, LiteralExpression>(constantsInScope, StringComparer.Ordinal),
                        allowDeadLocalElimination: true);
                }

                if (ifStatement.ElseBlock is not null)
                {
                    ifStatement.ElseBlock = RewriteStatementList(
                        ifStatement.ElseBlock,
                        options,
                        new Dictionary<string, LiteralExpression>(constantsInScope, StringComparer.Ordinal),
                        allowDeadLocalElimination: true);
                }

                constantsInScope.Clear();
                return ifStatement;

            case WhileLoop whileLoop:
                whileLoop.Condition = expressionRewriter.RewriteExpression(whileLoop.Condition, options, constantsInScope);
                whileLoop.Body = RewriteStatementList(
                    whileLoop.Body,
                    options,
                    new Dictionary<string, LiteralExpression>(constantsInScope, StringComparer.Ordinal),
                    allowDeadLocalElimination: false);
                constantsInScope.Clear();
                return whileLoop;

            case ForLoop forLoop:
                forLoop.Range = expressionRewriter.RewriteExpression(forLoop.Range, options, constantsInScope);
                if (forLoop.Step is not null)
                    forLoop.Step = expressionRewriter.RewriteExpression(forLoop.Step, options, constantsInScope);

                var forScope = new Dictionary<string, LiteralExpression>(constantsInScope, StringComparer.Ordinal);
                forScope.Remove(forLoop.Variable);
                forLoop.Body = RewriteStatementList(forLoop.Body, options, forScope, allowDeadLocalElimination: false);
                constantsInScope.Clear();
                return forLoop;

            case ReturnStatement returnStatement:
                if (returnStatement.Value is not null)
                    returnStatement.Value = expressionRewriter.RewriteExpression(returnStatement.Value, options, constantsInScope);
                return returnStatement;

            case ExpressionStatement expressionStatement:
                expressionStatement.Expression = expressionRewriter.RewriteExpression(expressionStatement.Expression, options, constantsInScope);
                return expressionStatement;

            case TryStatement tryStatement:
                tryStatement.TryBlock = RewriteStatementList(
                    tryStatement.TryBlock,
                    options,
                    new Dictionary<string, LiteralExpression>(constantsInScope, StringComparer.Ordinal),
                    allowDeadLocalElimination: true);

                var catchScope = new Dictionary<string, LiteralExpression>(constantsInScope, StringComparer.Ordinal);
                catchScope.Remove(tryStatement.ErrorVariable);
                tryStatement.CatchBlock = RewriteStatementList(
                    tryStatement.CatchBlock,
                    options,
                    catchScope,
                    allowDeadLocalElimination: true);
                constantsInScope.Clear();
                return tryStatement;

            case ThrowStatement throwStatement:
                throwStatement.Value = expressionRewriter.RewriteExpression(throwStatement.Value, options, constantsInScope);
                return throwStatement;

            case FunctionDeclaration functionDeclaration:
                functionDeclaration.Body = RewriteStatementList(
                    functionDeclaration.Body,
                    options,
                    CreateFunctionScope(functionDeclaration.Parameters),
                    allowDeadLocalElimination: true);
                constantsInScope.Remove(functionDeclaration.Name);
                return functionDeclaration;

            case ImplBlock implBlock:
                foreach (var method in implBlock.Methods)
                {
                    method.Body = RewriteStatementList(
                        method.Body,
                        options,
                        CreateFunctionScope(method.Parameters),
                        allowDeadLocalElimination: true);
                }
                return implBlock;

            case ImportStatement:
            case StructDeclaration:
            case EnumDeclaration:
            case ShStatement:
            case BreakStatement:
            case ContinueStatement:
                return statement;

            default:
                constantsInScope.Clear();
                return statement;
        }
    }

    private static Dictionary<string, LiteralExpression> CreateFunctionScope(IEnumerable<Parameter> parameters)
    {
        return new Dictionary<string, LiteralExpression>(StringComparer.Ordinal);
    }

    private static void TrackDeclaration(
        VariableDeclaration declaration,
        Dictionary<string, LiteralExpression> constantsInScope,
        AstOptimizationOptions options)
    {
        if (!options.EnableConstantPropagation || declaration.Kind == VariableDeclaration.VarKind.Mut)
        {
            constantsInScope.Remove(declaration.Name);
            return;
        }

        if (declaration.Value is LiteralExpression literal)
            constantsInScope[declaration.Name] = AstLiteralFactory.CloneLiteral(literal);
        else
            constantsInScope.Remove(declaration.Name);
    }

    private static void InvalidateAssignedTarget(Expression target, Dictionary<string, LiteralExpression> constantsInScope)
    {
        if (target is IdentifierExpression identifier)
            constantsInScope.Remove(identifier.Name);
    }
}
