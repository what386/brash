namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;

/// <summary>
/// Resolves symbols (variables, functions, types) and attaches type information to expressions
/// </summary>
public class SymbolResolver
{
    private readonly DiagnosticBag diagnostics;
    private readonly SymbolTable symbolTable;
    private readonly TypeChecker typeChecker;

    public SymbolResolver(DiagnosticBag diagnostics, SymbolTable symbolTable, TypeChecker typeChecker)
    {
        this.diagnostics = diagnostics;
        this.symbolTable = symbolTable;
        this.typeChecker = typeChecker;
    }

    // ============================================
    // Expression Type Resolution
    // ============================================

    public TypeNode ResolveExpressionType(Expression expr)
    {
        return expr switch
        {
            LiteralExpression lit => lit.Type,
            NullLiteral => new NullableType { BaseType = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void } },
            IdentifierExpression ident => ResolveIdentifier(ident),
            BinaryExpression bin => ResolveBinaryExpression(bin),
            UnaryExpression unary => ResolveUnaryExpression(unary),
            FunctionCallExpression call => ResolveFunctionCall(call),
            MethodCallExpression method => ResolveMethodCall(method),
            MemberAccessExpression member => ResolveMemberAccess(member),
            IndexAccessExpression index => ResolveIndexAccess(index),
            ArrayLiteral array => ResolveArrayLiteral(array),
            MapLiteral map => ResolveMapLiteral(map),
            StructLiteral structLit => ResolveStructLiteral(structLit),
            TupleExpression tuple => ResolveTupleExpression(tuple),
            RangeExpression range => ResolveRangeExpression(range),
            PipeExpression pipe => ResolvePipeExpression(pipe),
            NullCoalesceExpression nullCoalesce => ResolveNullCoalesce(nullCoalesce),
            SafeNavigationExpression safeNav => ResolveSafeNavigation(safeNav),
            CommandExpression cmd => ResolveCommand(cmd),
            AwaitExpression await => ResolveAwait(await),
            SelfExpression => ResolveSelf(),
            ParenthesizedExpression paren => ResolveExpressionType(paren.Expression),
            _ => new UnknownType()
        };
    }

    private TypeNode ResolveIdentifier(IdentifierExpression ident)
    {
        var symbol = symbolTable.LookupVariable(ident.Name);
        if (symbol == null)
        {
            diagnostics.AddError($"Undefined variable '{ident.Name}'", ident.Line, ident.Column);
            return new UnknownType();
        }

        return symbol.Type;
    }

    private TypeNode ResolveBinaryExpression(BinaryExpression bin)
    {
        var leftType = ResolveExpressionType(bin.Left);
        var rightType = ResolveExpressionType(bin.Right);

        // Validate the operation
        if (bin.Operator is "+" or "-" or "*" or "/" or "%")
        {
            typeChecker.ValidateArithmeticOperation(bin.Operator, leftType, rightType, bin.Line, bin.Column);
        }
        else if (bin.Operator is "&&" or "||")
        {
            typeChecker.ValidateLogicalOperation(bin.Operator, leftType, rightType, bin.Line, bin.Column);
        }

        return typeChecker.InferBinaryExpressionType(bin.Operator, leftType, rightType);
    }

    private TypeNode ResolveUnaryExpression(UnaryExpression unary)
    {
        var operandType = ResolveExpressionType(unary.Operand);
        return typeChecker.InferUnaryExpressionType(unary.Operator, operandType);
    }

    private TypeNode ResolveFunctionCall(FunctionCallExpression call)
    {
        var function = symbolTable.LookupFunction(call.FunctionName);
        if (function == null)
        {
            diagnostics.AddError($"Undefined function '{call.FunctionName}'", call.Line, call.Column);
            return new UnknownType();
        }

        // Resolve argument types
        var argumentTypes = call.Arguments.Select(ResolveExpressionType).ToList();

        // Validate the call
        typeChecker.ValidateFunctionCall(function, argumentTypes, call.Line, call.Column);

        return function.ReturnType;
    }

    private TypeNode ResolveMethodCall(MethodCallExpression method)
    {
        var objectType = ResolveExpressionType(method.Object);

        // Get the type name
        string typeName = objectType switch
        {
            NamedType named => named.Name,
            _ => objectType.ToString()
        };

        var methodSymbol = symbolTable.LookupMethod(typeName, method.MethodName);
        if (methodSymbol == null)
        {
            diagnostics.AddError(
                $"Type '{typeName}' has no method '{method.MethodName}'",
                method.Line, method.Column);
            return new UnknownType();
        }

        // Resolve argument types
        var argumentTypes = method.Arguments.Select(ResolveExpressionType).ToList();

        // Validate the call
        typeChecker.ValidateMethodCall(methodSymbol, argumentTypes, method.Line, method.Column);

        return methodSymbol.ReturnType;
    }

    private TypeNode ResolveMemberAccess(MemberAccessExpression member)
    {
        if (member.Object is IdentifierExpression typeIdent)
        {
            var typeSymbol = symbolTable.LookupType(typeIdent.Name);
            if (typeSymbol?.IsEnum == true)
            {
                if (!typeSymbol.EnumVariants.Contains(member.MemberName))
                {
                    diagnostics.AddError(
                        $"Enum '{typeIdent.Name}' has no variant '{member.MemberName}'",
                        member.Line, member.Column);
                    return new UnknownType();
                }

                return new NamedType { Name = typeIdent.Name };
            }
        }

        var objectType = ResolveExpressionType(member.Object);

        if (objectType is NamedType namedType)
        {
            var typeSymbol = symbolTable.LookupType(namedType.Name);
            if (typeSymbol == null)
            {
                diagnostics.AddError($"Unknown type '{namedType.Name}'", member.Line, member.Column);
                return new UnknownType();
            }

            if (!typeSymbol.Fields.TryGetValue(member.MemberName, out var fieldType))
            {
                diagnostics.AddError(
                    $"Type '{namedType.Name}' has no field '{member.MemberName}'",
                    member.Line, member.Column);
                return new UnknownType();
            }

            return fieldType;
        }

        diagnostics.AddError(
            $"Cannot access member '{member.MemberName}' on type '{objectType}'",
            member.Line, member.Column);
        return new UnknownType();
    }

    private TypeNode ResolveIndexAccess(IndexAccessExpression index)
    {
        var arrayType = ResolveExpressionType(index.Array);
        var indexType = ResolveExpressionType(index.Index);

        typeChecker.ValidateIndexAccess(arrayType, indexType, index.Line, index.Column);

        if (arrayType is ArrayType array)
            return array.ElementType;

        if (arrayType is MapType map)
            return map.ValueType;

        return new UnknownType();
    }

    private TypeNode ResolveArrayLiteral(ArrayLiteral array)
    {
        if (array.Elements.Count == 0)
        {
            // Empty array - could infer as array of unknown or use context
            return new ArrayType { ElementType = new UnknownType() };
        }

        // Infer element type from first element
        var elementType = ResolveExpressionType(array.Elements[0]);

        // Validate all elements have compatible types
        for (int i = 1; i < array.Elements.Count; i++)
        {
            var elemType = ResolveExpressionType(array.Elements[i]);
            if (!typeChecker.AreTypesCompatible(elementType, elemType))
            {
                diagnostics.AddWarning(
                    $"Array element {i} has type '{elemType}' but expected '{elementType}'",
                    array.Line, array.Column);
            }
        }

        return new ArrayType { ElementType = elementType };
    }

    private TypeNode ResolveMapLiteral(MapLiteral map)
    {
        if (map.Entries.Count == 0)
        {
            return new MapType
            {
                KeyType = new UnknownType(),
                ValueType = new UnknownType()
            };
        }

        var firstEntry = map.Entries[0];
        var keyType = ResolveExpressionType(firstEntry.Key);
        var valueType = ResolveExpressionType(firstEntry.Value);

        // Validate all entries
        foreach (var entry in map.Entries.Skip(1))
        {
            var entryKeyType = ResolveExpressionType(entry.Key);
            var entryValueType = ResolveExpressionType(entry.Value);

            if (!typeChecker.AreTypesCompatible(keyType, entryKeyType))
            {
                diagnostics.AddWarning($"Map key type mismatch: expected '{keyType}', got '{entryKeyType}'",
                    map.Line, map.Column);
            }

            if (!typeChecker.AreTypesCompatible(valueType, entryValueType))
            {
                diagnostics.AddWarning($"Map value type mismatch: expected '{valueType}', got '{entryValueType}'",
                    map.Line, map.Column);
            }
        }

        return new MapType { KeyType = keyType, ValueType = valueType };
    }

    private TypeNode ResolveStructLiteral(StructLiteral structLit)
    {
        var typeSymbol = symbolTable.LookupType(structLit.TypeName);
        if (typeSymbol == null)
        {
            diagnostics.AddError($"Unknown type '{structLit.TypeName}'", structLit.Line, structLit.Column);
            return new UnknownType();
        }

        // Validate all fields are initialized
        var providedFields = new HashSet<string>(structLit.Fields.Select(f => f.Field));
        var missingFields = typeSymbol.Fields.Keys.Except(providedFields).ToList();

        if (missingFields.Any())
        {
            diagnostics.AddError(
                $"Missing fields in struct initialization: {string.Join(", ", missingFields)}",
                structLit.Line, structLit.Column);
        }

        // Validate field types
        foreach (var (fieldName, fieldValue) in structLit.Fields)
        {
            if (!typeSymbol.Fields.TryGetValue(fieldName, out var expectedType))
            {
                diagnostics.AddError(
                    $"Type '{structLit.TypeName}' has no field '{fieldName}'",
                    structLit.Line, structLit.Column);
                continue;
            }

            var actualType = ResolveExpressionType(fieldValue);
            if (!typeChecker.AreTypesCompatible(expectedType, actualType))
            {
                diagnostics.AddError(
                    $"Field '{fieldName}': expected '{expectedType}', got '{actualType}'",
                    structLit.Line, structLit.Column);
            }
        }

        return new NamedType { Name = structLit.TypeName };
    }

    private TypeNode ResolveTupleExpression(TupleExpression tuple)
    {
        var elementTypes = tuple.Elements.Select(ResolveExpressionType).ToList();
        return new TupleType { ElementTypes = elementTypes };
    }

    private TypeNode ResolveRangeExpression(RangeExpression range)
    {
        var startType = ResolveExpressionType(range.Start);
        var endType = ResolveExpressionType(range.End);

        // Both should be numeric
        if (!typeChecker.IsNumericType(startType))
        {
            diagnostics.AddError($"Range start must be numeric, got '{startType}'",
                range.Line, range.Column);
        }

        if (!typeChecker.IsNumericType(endType))
        {
            diagnostics.AddError($"Range end must be numeric, got '{endType}'",
                range.Line, range.Column);
        }

        // Return a tuple type representing the range
        return new TupleType
        {
            ElementTypes = new List<TypeNode> { startType, endType }
        };
    }

    private TypeNode ResolvePipeExpression(PipeExpression pipe)
    {
        // The left side is piped to the right side
        // Right side should be a function call that takes the left's output
        var leftType = ResolveExpressionType(pipe.Left);
        var rightType = ResolveExpressionType(pipe.Right);

        // Return the right side's type (the result of the pipeline)
        return rightType;
    }

    private TypeNode ResolveNullCoalesce(NullCoalesceExpression nullCoalesce)
    {
        var leftType = ResolveExpressionType(nullCoalesce.Left);
        var rightType = ResolveExpressionType(nullCoalesce.Right);

        // Result is the non-nullable version of left, or right's type
        if (leftType is NullableType nullable)
            return nullable.BaseType;

        return leftType;
    }

    private TypeNode ResolveSafeNavigation(SafeNavigationExpression safeNav)
    {
        var objectType = ResolveExpressionType(safeNav.Object);

        // Unwrap nullable if needed
        var baseType = typeChecker.GetBaseType(objectType);

        if (baseType is NamedType namedType)
        {
            var typeSymbol = symbolTable.LookupType(namedType.Name);
            if (typeSymbol != null && typeSymbol.Fields.TryGetValue(safeNav.MemberName, out var fieldType))
            {
                // Result is always nullable
                return new NullableType { BaseType = fieldType };
            }
        }

        diagnostics.AddError(
            $"Cannot access member '{safeNav.MemberName}' on type '{objectType}'",
            safeNav.Line, safeNav.Column);
        return new UnknownType();
    }

    private TypeNode ResolveCommand(CommandExpression cmd)
    {
        // Commands return a Process type (or string for stdout)
        // For now, return a special process type
        return new NamedType { Name = "Process" };
    }

    private TypeNode ResolveAwait(AwaitExpression await)
    {
        var exprType = ResolveExpressionType(await.Expression);

        // Awaiting a process returns its result
        // For simplicity, return the inner type
        return exprType;
    }

    private TypeNode ResolveSelf()
    {
        // 'self' type depends on context (current method's type)
        // For now, return unknown - this needs context tracking
        return new UnknownType();
    }
}
