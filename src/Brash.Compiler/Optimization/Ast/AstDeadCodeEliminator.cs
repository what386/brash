namespace Brash.Compiler.Optimization.Ast;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;

internal sealed class AstDeadCodeEliminator
{
    public List<Statement> EliminateDeadLocals(List<Statement> statements)
    {
        var liveVariables = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Statement>(statements.Count);

        for (int i = statements.Count - 1; i >= 0; i--)
        {
            var statement = statements[i];
            if (CanElideStatement(statement, liveVariables))
                continue;

            UpdateLiveVariables(statement, liveVariables);
            result.Add(statement);
        }

        result.Reverse();
        return result;
    }

    private bool CanElideStatement(Statement statement, HashSet<string> liveVariables)
    {
        if (statement is VariableDeclaration variableDeclaration)
        {
            return !variableDeclaration.IsPublic &&
                !liveVariables.Contains(variableDeclaration.Name) &&
                IsPureExpression(variableDeclaration.Value);
        }

        if (statement is TupleVariableDeclaration tupleDeclaration)
        {
            bool anyLive = tupleDeclaration.Elements.Any(element => liveVariables.Contains(element.Name));
            return !anyLive && IsPureExpression(tupleDeclaration.Value);
        }

        if (statement is Assignment assignment &&
            assignment.Target is IdentifierExpression targetIdentifier &&
            !liveVariables.Contains(targetIdentifier.Name) &&
            IsPureExpression(assignment.Value))
        {
            return true;
        }

        if (statement is ExpressionStatement expressionStatement && IsPureExpression(expressionStatement.Expression))
        {
            return true;
        }

        return false;
    }

    private void UpdateLiveVariables(Statement statement, HashSet<string> liveVariables)
    {
        switch (statement)
        {
            case VariableDeclaration variableDeclaration:
                liveVariables.Remove(variableDeclaration.Name);
                CollectReadIdentifiers(variableDeclaration.Value, liveVariables);
                break;

            case TupleVariableDeclaration tupleDeclaration:
                foreach (var element in tupleDeclaration.Elements)
                {
                    liveVariables.Remove(element.Name);
                }
                CollectReadIdentifiers(tupleDeclaration.Value, liveVariables);
                break;

            case Assignment assignment when assignment.Target is IdentifierExpression targetIdentifier:
                liveVariables.Remove(targetIdentifier.Name);
                CollectReadIdentifiers(assignment.Value, liveVariables);
                break;

            case Assignment assignment:
                CollectReadIdentifiers(assignment.Target, liveVariables);
                CollectReadIdentifiers(assignment.Value, liveVariables);
                break;

            case ReturnStatement returnStatement:
                liveVariables.Clear();
                if (returnStatement.Value is not null)
                    CollectReadIdentifiers(returnStatement.Value, liveVariables);
                break;

            case IfStatement ifStatement:
                CollectReadIdentifiers(ifStatement.Condition, liveVariables);
                CollectReadsFromStatementList(ifStatement.ThenBlock, liveVariables);
                foreach (var elif in ifStatement.ElifClauses)
                {
                    CollectReadIdentifiers(elif.Condition, liveVariables);
                    CollectReadsFromStatementList(elif.Block, liveVariables);
                }
                if (ifStatement.ElseBlock is not null)
                    CollectReadsFromStatementList(ifStatement.ElseBlock, liveVariables);
                break;

            case WhileLoop whileLoop:
                CollectReadIdentifiers(whileLoop.Condition, liveVariables);
                CollectReadsFromStatementList(whileLoop.Body, liveVariables);
                break;

            case ForLoop forLoop:
                liveVariables.Remove(forLoop.Variable);
                CollectReadIdentifiers(forLoop.Range, liveVariables);
                if (forLoop.Step is not null)
                    CollectReadIdentifiers(forLoop.Step, liveVariables);
                CollectReadsFromStatementList(forLoop.Body, liveVariables);
                break;

            case TryStatement tryStatement:
                CollectReadsFromStatementList(tryStatement.TryBlock, liveVariables);
                CollectReadsFromStatementList(tryStatement.CatchBlock, liveVariables);
                liveVariables.Remove(tryStatement.ErrorVariable);
                break;

            case ThrowStatement throwStatement:
                CollectReadIdentifiers(throwStatement.Value, liveVariables);
                break;

            case ExpressionStatement expressionStatement:
                CollectReadIdentifiers(expressionStatement.Expression, liveVariables);
                break;

            case FunctionDeclaration functionDeclaration:
                liveVariables.Remove(functionDeclaration.Name);
                break;
        }
    }

    private void CollectReadsFromStatementList(IEnumerable<Statement> statements, HashSet<string> liveVariables)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case VariableDeclaration variableDeclaration:
                    CollectReadIdentifiers(variableDeclaration.Value, liveVariables);
                    break;
                case TupleVariableDeclaration tupleDeclaration:
                    CollectReadIdentifiers(tupleDeclaration.Value, liveVariables);
                    break;
                case Assignment assignment:
                    CollectReadIdentifiers(assignment.Target, liveVariables);
                    CollectReadIdentifiers(assignment.Value, liveVariables);
                    break;
                case IfStatement ifStatement:
                    CollectReadIdentifiers(ifStatement.Condition, liveVariables);
                    CollectReadsFromStatementList(ifStatement.ThenBlock, liveVariables);
                    foreach (var elif in ifStatement.ElifClauses)
                    {
                        CollectReadIdentifiers(elif.Condition, liveVariables);
                        CollectReadsFromStatementList(elif.Block, liveVariables);
                    }
                    if (ifStatement.ElseBlock is not null)
                        CollectReadsFromStatementList(ifStatement.ElseBlock, liveVariables);
                    break;
                case WhileLoop whileLoop:
                    CollectReadIdentifiers(whileLoop.Condition, liveVariables);
                    CollectReadsFromStatementList(whileLoop.Body, liveVariables);
                    break;
                case ForLoop forLoop:
                    CollectReadIdentifiers(forLoop.Range, liveVariables);
                    if (forLoop.Step is not null)
                        CollectReadIdentifiers(forLoop.Step, liveVariables);
                    CollectReadsFromStatementList(forLoop.Body, liveVariables);
                    break;
                case ReturnStatement returnStatement when returnStatement.Value is not null:
                    CollectReadIdentifiers(returnStatement.Value, liveVariables);
                    break;
                case ThrowStatement throwStatement:
                    CollectReadIdentifiers(throwStatement.Value, liveVariables);
                    break;
                case ExpressionStatement expressionStatement:
                    CollectReadIdentifiers(expressionStatement.Expression, liveVariables);
                    break;
            }
        }
    }

    private void CollectReadIdentifiers(Expression expression, HashSet<string> liveVariables)
    {
        switch (expression)
        {
            case IdentifierExpression identifier:
                liveVariables.Add(identifier.Name);
                break;

            case ParenthesizedExpression parenthesized:
                CollectReadIdentifiers(parenthesized.Expression, liveVariables);
                break;

            case BinaryExpression binary:
                CollectReadIdentifiers(binary.Left, liveVariables);
                CollectReadIdentifiers(binary.Right, liveVariables);
                break;

            case UnaryExpression unary:
                CollectReadIdentifiers(unary.Operand, liveVariables);
                break;

            case CastExpression cast:
                CollectReadIdentifiers(cast.Value, liveVariables);
                break;

            case RangeExpression range:
                CollectReadIdentifiers(range.Start, liveVariables);
                CollectReadIdentifiers(range.End, liveVariables);
                break;

            case PipeExpression pipe:
                CollectReadIdentifiers(pipe.Left, liveVariables);
                CollectReadIdentifiers(pipe.Right, liveVariables);
                break;

            case NullCoalesceExpression nullCoalesce:
                CollectReadIdentifiers(nullCoalesce.Left, liveVariables);
                CollectReadIdentifiers(nullCoalesce.Right, liveVariables);
                break;

            case FunctionCallExpression functionCall:
                foreach (var argument in functionCall.Arguments)
                    CollectReadIdentifiers(argument, liveVariables);
                break;

            case MethodCallExpression methodCall:
                CollectReadIdentifiers(methodCall.Object, liveVariables);
                foreach (var argument in methodCall.Arguments)
                    CollectReadIdentifiers(argument, liveVariables);
                break;

            case MemberAccessExpression memberAccess:
                CollectReadIdentifiers(memberAccess.Object, liveVariables);
                break;

            case IndexAccessExpression indexAccess:
                CollectReadIdentifiers(indexAccess.Array, liveVariables);
                CollectReadIdentifiers(indexAccess.Index, liveVariables);
                break;

            case SafeNavigationExpression safeNavigation:
                CollectReadIdentifiers(safeNavigation.Object, liveVariables);
                break;

            case ArrayLiteral arrayLiteral:
                foreach (var element in arrayLiteral.Elements)
                    CollectReadIdentifiers(element, liveVariables);
                break;

            case MapLiteral mapLiteral:
                foreach (var (key, value) in mapLiteral.Entries)
                {
                    CollectReadIdentifiers(key, liveVariables);
                    CollectReadIdentifiers(value, liveVariables);
                }
                break;

            case StructLiteral structLiteral:
                foreach (var (_, value) in structLiteral.Fields)
                    CollectReadIdentifiers(value, liveVariables);
                break;

            case TupleExpression tupleExpression:
                foreach (var element in tupleExpression.Elements)
                    CollectReadIdentifiers(element, liveVariables);
                break;

            case EnumLiteral enumLiteral:
                foreach (var value in enumLiteral.AssociatedValues)
                    CollectReadIdentifiers(value, liveVariables);
                break;

            case CommandExpression commandExpression:
                foreach (var argument in commandExpression.Arguments)
                    CollectReadIdentifiers(argument, liveVariables);
                break;

            case AwaitExpression awaitExpression:
                CollectReadIdentifiers(awaitExpression.Expression, liveVariables);
                break;
        }
    }

    private static bool IsPureExpression(Expression expression)
    {
        return expression switch
        {
            LiteralExpression => true,
            IdentifierExpression => true,
            NullLiteral => true,
            SelfExpression => true,
            ParenthesizedExpression parenthesized => IsPureExpression(parenthesized.Expression),
            UnaryExpression unary => IsPureExpression(unary.Operand),
            BinaryExpression binary => IsPureExpression(binary.Left) && IsPureExpression(binary.Right),
            CastExpression cast => IsPureExpression(cast.Value),
            RangeExpression range => IsPureExpression(range.Start) && IsPureExpression(range.End),
            NullCoalesceExpression nullCoalesce => IsPureExpression(nullCoalesce.Left) && IsPureExpression(nullCoalesce.Right),
            MemberAccessExpression memberAccess => IsPureExpression(memberAccess.Object),
            IndexAccessExpression indexAccess => IsPureExpression(indexAccess.Array) && IsPureExpression(indexAccess.Index),
            SafeNavigationExpression safeNavigation => IsPureExpression(safeNavigation.Object),
            ArrayLiteral arrayLiteral => arrayLiteral.Elements.All(IsPureExpression),
            MapLiteral mapLiteral => mapLiteral.Entries.All(entry => IsPureExpression(entry.Key) && IsPureExpression(entry.Value)),
            StructLiteral structLiteral => structLiteral.Fields.All(field => IsPureExpression(field.Value)),
            TupleExpression tupleExpression => tupleExpression.Elements.All(IsPureExpression),
            EnumLiteral enumLiteral => enumLiteral.AssociatedValues.All(IsPureExpression),
            _ => false
        };
    }
}
