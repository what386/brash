namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Diagnostics;

/// <summary>
/// Enforces fail-fast diagnostics for AST constructs not yet supported by Bash transpilation.
/// </summary>
public class TranspileReadinessChecker
{
    private readonly DiagnosticBag diagnostics;

    public TranspileReadinessChecker(DiagnosticBag diagnostics)
    {
        this.diagnostics = diagnostics;
    }

    public void ValidateStatement(Statement statement)
    {
        switch (statement)
        {
            case TryStatement tryStmt:
                ValidateStatements(tryStmt.TryBlock);
                ValidateStatements(tryStmt.CatchBlock);
                break;

            case ThrowStatement throwStmt:
                ValidateExpression(throwStmt.Value);
                break;

            case ImportStatement importStmt:
                ReportUnsupported("import", importStmt.Line, importStmt.Column);
                break;

            case VariableDeclaration varDecl:
                ValidateExpression(varDecl.Value);
                break;

            case TupleVariableDeclaration tupleDecl:
                ValidateExpression(tupleDecl.Value);
                break;

            case Assignment assignment:
                ValidateExpression(assignment.Target);
                ValidateExpression(assignment.Value);
                break;

            case FunctionDeclaration fn:
                ValidateStatements(fn.Body);
                break;

            case IfStatement ifStmt:
                ValidateExpression(ifStmt.Condition);
                ValidateStatements(ifStmt.ThenBlock);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    ValidateExpression(elif.Condition);
                    ValidateStatements(elif.Block);
                }
                if (ifStmt.ElseBlock != null)
                    ValidateStatements(ifStmt.ElseBlock);
                break;

            case ForLoop forLoop:
                ValidateForLoopRange(forLoop.Range);
                if (forLoop.Step != null)
                    ValidateExpression(forLoop.Step);
                ValidateStatements(forLoop.Body);
                break;

            case WhileLoop whileLoop:
                ValidateExpression(whileLoop.Condition);
                ValidateStatements(whileLoop.Body);
                break;

            case ReturnStatement returnStmt:
                if (returnStmt.Value != null)
                    ValidateExpression(returnStmt.Value);
                break;

            case ExpressionStatement exprStmt:
                ValidateExpression(exprStmt.Expression);
                break;

            case ImplBlock implBlock:
                foreach (var method in implBlock.Methods)
                    ValidateStatements(method.Body);
                break;
        }
    }

    private void ValidateStatements(IEnumerable<Statement> statements)
    {
        foreach (var statement in statements)
            ValidateStatement(statement);
    }

    private void ValidateExpression(Expression expression)
    {
        switch (expression)
        {
            case AwaitExpression awaitExpr:
                ReportUnsupported("await", awaitExpr.Line, awaitExpr.Column);
                ValidateExpression(awaitExpr.Expression);
                break;

            case CommandExpression cmd when cmd.IsAsync:
                ReportUnsupported($"async {cmd.Kind.ToString().ToLowerInvariant()}(...)", cmd.Line, cmd.Column);
                foreach (var arg in cmd.Arguments)
                    ValidateExpression(arg);
                break;

            case MapLiteral mapLiteral:
                ReportUnsupported("map literal code generation", mapLiteral.Line, mapLiteral.Column);
                foreach (var (key, value) in mapLiteral.Entries)
                {
                    ValidateExpression(key);
                    ValidateExpression(value);
                }
                break;

            case TupleExpression tupleExpr:
                foreach (var element in tupleExpr.Elements)
                    ValidateExpression(element);
                break;

            case RangeExpression range:
                ReportUnsupported("range value code generation", range.Line, range.Column);
                ValidateExpression(range.Start);
                ValidateExpression(range.End);
                break;

            case PipeExpression pipe:
                ValidateExpression(pipe.Left);
                ValidateExpression(pipe.Right);
                break;

            case BinaryExpression binary:
                ValidateExpression(binary.Left);
                ValidateExpression(binary.Right);
                break;

            case UnaryExpression unary:
                ValidateExpression(unary.Operand);
                break;

            case FunctionCallExpression functionCall:
                foreach (var arg in functionCall.Arguments)
                    ValidateExpression(arg);
                break;

            case MethodCallExpression methodCall:
                ValidateExpression(methodCall.Object);
                foreach (var arg in methodCall.Arguments)
                    ValidateExpression(arg);
                break;

            case MemberAccessExpression member:
                ValidateExpression(member.Object);
                break;

            case SafeNavigationExpression safe:
                ValidateExpression(safe.Object);
                break;

            case IndexAccessExpression index:
                ValidateExpression(index.Array);
                ValidateExpression(index.Index);
                break;

            case ArrayLiteral array:
                foreach (var element in array.Elements)
                    ValidateExpression(element);
                break;

            case StructLiteral structLiteral:
                foreach (var (_, value) in structLiteral.Fields)
                    ValidateExpression(value);
                break;
        }
    }

    private void ValidateForLoopRange(Expression expression)
    {
        if (expression is RangeExpression range)
        {
            // Range syntax is supported specifically for for-loop iteration.
            ValidateExpression(range.Start);
            ValidateExpression(range.End);
            return;
        }

        ValidateExpression(expression);
    }

    private void ReportUnsupported(string feature, int line, int column)
    {
        diagnostics.AddError(
            $"Feature '{feature}' is not supported in transpilation yet",
            line,
            column);
    }
}
