namespace Brash.Compiler.Optimization.Ast;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;

internal sealed class AstExpressionRewriter
{
    private readonly AstConstantFolder constantFolder;

    public AstExpressionRewriter(AstConstantFolder constantFolder)
    {
        this.constantFolder = constantFolder;
    }

    public Expression RewriteExpression(
        Expression expression,
        AstOptimizationOptions options,
        Dictionary<string, LiteralExpression> constantsInScope)
    {
        Expression rewritten = expression switch
        {
            IdentifierExpression identifier when
                options.EnableConstantPropagation &&
                constantsInScope.TryGetValue(identifier.Name, out var literal)
                    => AstLiteralFactory.CloneLiteral(literal),

            ParenthesizedExpression parenthesized => new ParenthesizedExpression
            {
                Line = parenthesized.Line,
                Column = parenthesized.Column,
                Expression = RewriteExpression(parenthesized.Expression, options, constantsInScope)
            },

            BinaryExpression binary => new BinaryExpression
            {
                Line = binary.Line,
                Column = binary.Column,
                Operator = binary.Operator,
                Left = RewriteExpression(binary.Left, options, constantsInScope),
                Right = RewriteExpression(binary.Right, options, constantsInScope)
            },

            UnaryExpression unary => new UnaryExpression
            {
                Line = unary.Line,
                Column = unary.Column,
                Operator = unary.Operator,
                Operand = RewriteExpression(unary.Operand, options, constantsInScope)
            },

            CastExpression cast => new CastExpression
            {
                Line = cast.Line,
                Column = cast.Column,
                TargetType = cast.TargetType,
                Value = RewriteExpression(cast.Value, options, constantsInScope)
            },

            RangeExpression range => new RangeExpression
            {
                Line = range.Line,
                Column = range.Column,
                Start = RewriteExpression(range.Start, options, constantsInScope),
                End = RewriteExpression(range.End, options, constantsInScope)
            },

            PipeExpression pipe => new PipeExpression
            {
                Line = pipe.Line,
                Column = pipe.Column,
                Left = RewriteExpression(pipe.Left, options, constantsInScope),
                Right = RewriteExpression(pipe.Right, options, constantsInScope)
            },

            NullCoalesceExpression nullCoalesce => new NullCoalesceExpression
            {
                Line = nullCoalesce.Line,
                Column = nullCoalesce.Column,
                Left = RewriteExpression(nullCoalesce.Left, options, constantsInScope),
                Right = RewriteExpression(nullCoalesce.Right, options, constantsInScope)
            },

            FunctionCallExpression call => new FunctionCallExpression
            {
                Line = call.Line,
                Column = call.Column,
                FunctionName = call.FunctionName,
                Arguments = call.Arguments.Select(argument => RewriteExpression(argument, options, constantsInScope)).ToList()
            },

            MethodCallExpression methodCall => new MethodCallExpression
            {
                Line = methodCall.Line,
                Column = methodCall.Column,
                Object = RewriteExpression(methodCall.Object, options, constantsInScope),
                MethodName = methodCall.MethodName,
                Arguments = methodCall.Arguments.Select(argument => RewriteExpression(argument, options, constantsInScope)).ToList(),
                IsStaticDispatch = methodCall.IsStaticDispatch,
                StaticTypeName = methodCall.StaticTypeName
            },

            MemberAccessExpression memberAccess => new MemberAccessExpression
            {
                Line = memberAccess.Line,
                Column = memberAccess.Column,
                Object = RewriteExpression(memberAccess.Object, options, constantsInScope),
                MemberName = memberAccess.MemberName
            },

            IndexAccessExpression indexAccess => new IndexAccessExpression
            {
                Line = indexAccess.Line,
                Column = indexAccess.Column,
                Array = RewriteExpression(indexAccess.Array, options, constantsInScope),
                Index = RewriteExpression(indexAccess.Index, options, constantsInScope)
            },

            SafeNavigationExpression safeNavigation => new SafeNavigationExpression
            {
                Line = safeNavigation.Line,
                Column = safeNavigation.Column,
                Object = RewriteExpression(safeNavigation.Object, options, constantsInScope),
                MemberName = safeNavigation.MemberName
            },

            ArrayLiteral arrayLiteral => new ArrayLiteral
            {
                Line = arrayLiteral.Line,
                Column = arrayLiteral.Column,
                Elements = arrayLiteral.Elements.Select(element => RewriteExpression(element, options, constantsInScope)).ToList()
            },

            MapLiteral mapLiteral => new MapLiteral
            {
                Line = mapLiteral.Line,
                Column = mapLiteral.Column,
                Entries = mapLiteral.Entries
                    .Select(entry => (
                        RewriteExpression(entry.Key, options, constantsInScope),
                        RewriteExpression(entry.Value, options, constantsInScope)))
                    .ToList()
            },

            StructLiteral structLiteral => new StructLiteral
            {
                Line = structLiteral.Line,
                Column = structLiteral.Column,
                TypeName = structLiteral.TypeName,
                Fields = structLiteral.Fields
                    .Select(field => (field.Field, RewriteExpression(field.Value, options, constantsInScope)))
                    .ToList()
            },

            TupleExpression tupleExpression => new TupleExpression
            {
                Line = tupleExpression.Line,
                Column = tupleExpression.Column,
                Elements = tupleExpression.Elements.Select(element => RewriteExpression(element, options, constantsInScope)).ToList()
            },

            EnumLiteral enumLiteral => new EnumLiteral
            {
                Line = enumLiteral.Line,
                Column = enumLiteral.Column,
                EnumName = enumLiteral.EnumName,
                VariantName = enumLiteral.VariantName,
                AssociatedValues = enumLiteral.AssociatedValues.Select(value => RewriteExpression(value, options, constantsInScope)).ToList()
            },

            CommandExpression commandExpression => new CommandExpression
            {
                Line = commandExpression.Line,
                Column = commandExpression.Column,
                Kind = commandExpression.Kind,
                IsAsync = commandExpression.IsAsync,
                Arguments = commandExpression.Arguments.Select(argument => RewriteExpression(argument, options, constantsInScope)).ToList()
            },

            AwaitExpression awaitExpression => new AwaitExpression
            {
                Line = awaitExpression.Line,
                Column = awaitExpression.Column,
                Expression = RewriteExpression(awaitExpression.Expression, options, constantsInScope)
            },

            _ => expression
        };

        if (options.EnableConstantFolding && constantFolder.TryFoldExpression(rewritten, out var folded))
            return folded;

        return rewritten;
    }

    public Expression RewriteAssignmentTarget(
        Expression target,
        AstOptimizationOptions options,
        Dictionary<string, LiteralExpression> constantsInScope)
    {
        return target switch
        {
            MemberAccessExpression memberAccess => new MemberAccessExpression
            {
                Line = memberAccess.Line,
                Column = memberAccess.Column,
                Object = RewriteAssignmentTarget(memberAccess.Object, options, constantsInScope),
                MemberName = memberAccess.MemberName
            },
            IndexAccessExpression indexAccess => new IndexAccessExpression
            {
                Line = indexAccess.Line,
                Column = indexAccess.Column,
                Array = RewriteAssignmentTarget(indexAccess.Array, options, constantsInScope),
                Index = RewriteExpression(indexAccess.Index, options, constantsInScope)
            },
            ParenthesizedExpression parenthesized => new ParenthesizedExpression
            {
                Line = parenthesized.Line,
                Column = parenthesized.Column,
                Expression = RewriteAssignmentTarget(parenthesized.Expression, options, constantsInScope)
            },
            _ => target
        };
    }
}
