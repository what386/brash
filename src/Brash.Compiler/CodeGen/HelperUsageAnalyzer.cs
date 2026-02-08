namespace Brash.Compiler.CodeGen;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;

internal static class HelperUsageAnalyzer
{
    public static HelperUsage Analyze(ProgramNode program)
    {
        var usage = new HelperUsage();
        foreach (var statement in program.Statements)
            VisitStatement(statement, usage);

        // Dependencies
        if (usage.NeedsMapLiteral)
        {
            usage.NeedsMapNew = true;
            usage.NeedsMapSet = true;
        }

        if (usage.NeedsIndexGet)
            usage.NeedsMapGet = true;

        if (usage.NeedsIndexSet)
            usage.NeedsMapSet = true;

        return usage;
    }

    private static void VisitStatement(Statement statement, HelperUsage usage)
    {
        switch (statement)
        {
            case VariableDeclaration varDecl:
                VisitExpression(varDecl.Value, usage);
                break;
            case TupleVariableDeclaration tupleDecl:
                VisitExpression(tupleDecl.Value, usage);
                break;
            case Assignment assignment:
                VisitExpression(assignment.Target, usage);
                VisitExpression(assignment.Value, usage);
                if (assignment.Target is MemberAccessExpression)
                    usage.NeedsSetField = true;
                if (assignment.Target is IndexAccessExpression)
                    usage.NeedsIndexSet = true;
                break;
            case FunctionDeclaration funcDecl:
                foreach (var stmt in funcDecl.Body)
                    VisitStatement(stmt, usage);
                break;
            case IfStatement ifStmt:
                VisitExpression(ifStmt.Condition, usage);
                foreach (var stmt in ifStmt.ThenBlock)
                    VisitStatement(stmt, usage);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    VisitExpression(elif.Condition, usage);
                    foreach (var stmt in elif.Block)
                        VisitStatement(stmt, usage);
                }
                if (ifStmt.ElseBlock != null)
                {
                    foreach (var stmt in ifStmt.ElseBlock)
                        VisitStatement(stmt, usage);
                }
                break;
            case ForLoop forLoop:
                VisitExpression(forLoop.Range, usage);
                if (forLoop.Step != null)
                    VisitExpression(forLoop.Step, usage);
                foreach (var stmt in forLoop.Body)
                    VisitStatement(stmt, usage);
                break;
            case WhileLoop whileLoop:
                VisitExpression(whileLoop.Condition, usage);
                foreach (var stmt in whileLoop.Body)
                    VisitStatement(stmt, usage);
                break;
            case ReturnStatement ret:
                if (ret.Value != null)
                    VisitExpression(ret.Value, usage);
                break;
            case ThrowStatement thr:
                VisitExpression(thr.Value, usage);
                break;
            case TryStatement tr:
                foreach (var stmt in tr.TryBlock)
                    VisitStatement(stmt, usage);
                foreach (var stmt in tr.CatchBlock)
                    VisitStatement(stmt, usage);
                break;
            case ExpressionStatement exprStmt:
                VisitExpression(exprStmt.Expression, usage);
                break;
            case ImplBlock impl:
                foreach (var method in impl.Methods)
                {
                    foreach (var stmt in method.Body)
                        VisitStatement(stmt, usage);
                }
                break;
        }
    }

    private static void VisitExpression(Expression expression, HelperUsage usage)
    {
        switch (expression)
        {
            case BinaryExpression bin:
                VisitExpression(bin.Left, usage);
                VisitExpression(bin.Right, usage);
                break;
            case UnaryExpression unary:
                VisitExpression(unary.Operand, usage);
                break;
            case CastExpression cast:
                VisitExpression(cast.Value, usage);
                break;
            case FunctionCallExpression fnCall:
                foreach (var arg in fnCall.Arguments)
                    VisitExpression(arg, usage);
                break;
            case MethodCallExpression methodCall:
                usage.NeedsCallMethod = true;
                VisitExpression(methodCall.Object, usage);
                foreach (var arg in methodCall.Arguments)
                    VisitExpression(arg, usage);
                break;
            case MemberAccessExpression member:
                usage.NeedsGetField = true;
                VisitExpression(member.Object, usage);
                break;
            case SafeNavigationExpression safe:
                usage.NeedsGetField = true;
                VisitExpression(safe.Object, usage);
                break;
            case IndexAccessExpression index:
                usage.NeedsIndexGet = true;
                VisitExpression(index.Array, usage);
                VisitExpression(index.Index, usage);
                break;
            case ArrayLiteral array:
                foreach (var item in array.Elements)
                    VisitExpression(item, usage);
                break;
            case MapLiteral map:
                usage.NeedsMapLiteral = true;
                foreach (var (key, value) in map.Entries)
                {
                    VisitExpression(key, usage);
                    VisitExpression(value, usage);
                }
                break;
            case StructLiteral str:
                foreach (var (_, value) in str.Fields)
                    VisitExpression(value, usage);
                break;
            case TupleExpression tuple:
                foreach (var item in tuple.Elements)
                    VisitExpression(item, usage);
                break;
            case PipeExpression pipe:
                VisitExpression(pipe.Left, usage);
                VisitExpression(pipe.Right, usage);
                break;
            case NullCoalesceExpression nullCoalesce:
                VisitExpression(nullCoalesce.Left, usage);
                VisitExpression(nullCoalesce.Right, usage);
                break;
            case CommandExpression command:
                if (command.Kind == CommandKind.Exec && !CanUseDirectExec(command))
                    usage.NeedsExecCmd = true;
                if (command.IsAsync && command.Kind == CommandKind.Spawn)
                    usage.NeedsAsyncSpawnCmd = true;
                foreach (var arg in command.Arguments)
                    VisitExpression(arg, usage);
                break;
            case AwaitExpression awaitExpr:
                usage.NeedsAwait = true;
                VisitExpression(awaitExpr.Expression, usage);
                break;
        }
    }

    private static bool CanUseDirectExec(CommandExpression command)
    {
        return command.Arguments.Count == 1 &&
               command.Arguments[0] is LiteralExpression
               {
                   Type: PrimitiveType { PrimitiveKind: PrimitiveType.Kind.String }
               };
    }
}
