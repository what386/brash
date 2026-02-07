namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Diagnostics;

/// <summary>
/// Resolves symbols (variables, functions, types) and attaches type information to expressions
/// </summary>
public class SymbolResolver
{
    private readonly DiagnosticBag diagnostics;
    private readonly SymbolTable symbolTable;
    private readonly TypeChecker typeChecker;
    private readonly NullabilityChecker nullabilityChecker;
    private readonly PipeChecker pipeChecker;
    private string? currentTypeName;

    public SymbolResolver(
        DiagnosticBag diagnostics,
        SymbolTable symbolTable,
        TypeChecker typeChecker,
        NullabilityChecker nullabilityChecker)
    {
        this.diagnostics = diagnostics;
        this.symbolTable = symbolTable;
        this.typeChecker = typeChecker;
        this.nullabilityChecker = nullabilityChecker;
        this.pipeChecker = new PipeChecker(diagnostics, typeChecker);
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
            CastExpression cast => ResolveCastExpression(cast),
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

    private TypeNode ResolveCastExpression(CastExpression cast)
    {
        var sourceType = ResolveExpressionType(cast.Value);
        var targetType = cast.TargetType;

        if (!typeChecker.CanExplicitlyCast(sourceType, targetType))
        {
            diagnostics.AddError(
                $"Cannot cast value of type '{sourceType}' to '{targetType}'",
                cast.Line,
                cast.Column);
            return new UnknownType();
        }

        return targetType;
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
        method.IsStaticDispatch = false;
        method.StaticTypeName = null;

        if (method.Object is IdentifierExpression typeIdent
            && symbolTable.LookupVariable(typeIdent.Name) == null
            && symbolTable.LookupType(typeIdent.Name) != null)
        {
            var staticMethod = symbolTable.LookupMethod(typeIdent.Name, method.MethodName);
            if (staticMethod == null)
            {
                diagnostics.AddError(
                    $"Type '{typeIdent.Name}' has no method '{method.MethodName}'",
                    method.Line,
                    method.Column);
                return new UnknownType();
            }

            if (!staticMethod.IsStatic)
            {
                diagnostics.AddError(
                    $"Method '{typeIdent.Name}.{method.MethodName}' is an instance method and must be called on a value",
                    method.Line,
                    method.Column);
                return new UnknownType();
            }

            var staticArgumentTypes = method.Arguments.Select(ResolveExpressionType).ToList();
            typeChecker.ValidateMethodCall(staticMethod, staticArgumentTypes, method.Line, method.Column);
            method.IsStaticDispatch = true;
            method.StaticTypeName = typeIdent.Name;
            return staticMethod.ReturnType;
        }

        var objectType = ResolveExpressionType(method.Object);
        objectType = nullabilityChecker.RequireNonNullable(
            objectType,
            method.Line,
            method.Column,
            $"method call '{method.MethodName}'");

        if (method.MethodName == "to_string")
        {
            if (method.Arguments.Count != 0)
            {
                diagnostics.AddError(
                    "Method 'to_string' expects 0 arguments",
                    method.Line,
                    method.Column);
                return new UnknownType();
            }

            if (!typeChecker.IsStringConvertible(objectType))
            {
                diagnostics.AddError(
                    $"Type '{objectType}' does not support to_string()",
                    method.Line,
                    method.Column);
                return new UnknownType();
            }

            return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String };
        }

        // Get the type name
        string typeName = objectType switch
        {
            NamedType named => named.Name,
            _ => objectType.ToString() ?? "<unknown>"
        };

        var methodSymbol = symbolTable.LookupMethod(typeName, method.MethodName);
        if (methodSymbol == null)
        {
            diagnostics.AddError(
                $"Type '{typeName}' has no method '{method.MethodName}'",
                method.Line, method.Column);
            return new UnknownType();
        }

        if (methodSymbol.IsStatic)
        {
            diagnostics.AddError(
                $"Method '{typeName}.{method.MethodName}' is static and must be called as '{typeName}.{method.MethodName}(...)'",
                method.Line,
                method.Column);
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
        objectType = nullabilityChecker.RequireNonNullable(
            objectType,
            member.Line,
            member.Column,
            $"member access '{member.MemberName}'");

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
        arrayType = nullabilityChecker.RequireNonNullable(
            arrayType,
            index.Line,
            index.Column,
            "index access");
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
        var leftType = ResolveExpressionType(pipe.Left);

        // Command pipelines preserve existing behavior.
        if (pipeChecker.IsCommandType(leftType))
        {
            var rightType = ResolveExpressionType(pipe.Right);
            pipeChecker.ValidateCommandPipe(leftType, rightType, pipe.Line, pipe.Column);
            return new NamedType { Name = "Command" };
        }

        // Value pipelines: right side must be a callable stage that accepts the
        // left value as implicit first argument and returns the same type.
        return pipe.Right switch
        {
            FunctionCallExpression fnCall => ResolveValuePipeFunctionStage(leftType, fnCall, pipe.Line, pipe.Column),
            MethodCallExpression methodCall => ResolveValuePipeMethodStage(leftType, methodCall, pipe.Line, pipe.Column),
            _ => ReportInvalidValuePipeStage(leftType, pipe)
        };
    }

    private TypeNode ResolveValuePipeFunctionStage(
        TypeNode inputType,
        FunctionCallExpression call,
        int line,
        int column)
    {
        var function = symbolTable.LookupFunction(call.FunctionName);
        if (function == null)
        {
            diagnostics.AddError($"Undefined function '{call.FunctionName}'", call.Line, call.Column);
            return new UnknownType();
        }

        var argumentTypes = new List<TypeNode> { inputType };
        argumentTypes.AddRange(call.Arguments.Select(ResolveExpressionType));
        typeChecker.ValidateFunctionCall(function, argumentTypes, call.Line, call.Column);

        pipeChecker.ValidateValuePipeTypeInvariant(inputType, function.ReturnType, line, column);
        return function.ReturnType;
    }

    private TypeNode ResolveValuePipeMethodStage(
        TypeNode inputType,
        MethodCallExpression call,
        int line,
        int column)
    {
        if (call.MethodName == "to_string")
        {
            if (call.Arguments.Count != 0)
            {
                diagnostics.AddError(
                    "Method 'to_string' expects 0 arguments",
                    call.Line,
                    call.Column);
                return new UnknownType();
            }

            var targetType = ResolveExpressionType(call.Object);
            if (!typeChecker.IsStringConvertible(targetType))
            {
                diagnostics.AddError(
                    $"Type '{targetType}' does not support to_string()",
                    call.Line,
                    call.Column);
                return new UnknownType();
            }

            var returnType = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String };
            pipeChecker.ValidateValuePipeTypeInvariant(inputType, returnType, line, column);
            return returnType;
        }

        if (call.Object is IdentifierExpression typeIdent
            && symbolTable.LookupVariable(typeIdent.Name) == null
            && symbolTable.LookupType(typeIdent.Name) != null)
        {
            var staticMethod = symbolTable.LookupMethod(typeIdent.Name, call.MethodName);
            if (staticMethod == null)
            {
                diagnostics.AddError(
                    $"Type '{typeIdent.Name}' has no method '{call.MethodName}'",
                    call.Line,
                    call.Column);
                return new UnknownType();
            }

            if (!staticMethod.IsStatic)
            {
                diagnostics.AddError(
                    $"Method '{typeIdent.Name}.{call.MethodName}' is an instance method and cannot be used as a static pipe stage",
                    call.Line,
                    call.Column);
                return new UnknownType();
            }

            var staticArgumentTypes = new List<TypeNode> { inputType };
            staticArgumentTypes.AddRange(call.Arguments.Select(ResolveExpressionType));
            typeChecker.ValidateMethodCall(staticMethod, staticArgumentTypes, call.Line, call.Column);

            pipeChecker.ValidateValuePipeTypeInvariant(inputType, staticMethod.ReturnType, line, column);
            return staticMethod.ReturnType;
        }

        var objectType = ResolveExpressionType(call.Object);
        objectType = nullabilityChecker.RequireNonNullable(
            objectType,
            call.Line,
            call.Column,
            $"method call '{call.MethodName}'");

        var typeName = objectType switch
        {
            NamedType named => named.Name,
            _ => objectType.ToString() ?? "<unknown>"
        };

        var method = symbolTable.LookupMethod(typeName, call.MethodName);
        if (method == null)
        {
            diagnostics.AddError(
                $"Type '{typeName}' has no method '{call.MethodName}'",
                call.Line,
                call.Column);
            return new UnknownType();
        }

        if (method.IsStatic)
        {
            diagnostics.AddError(
                $"Method '{typeName}.{call.MethodName}' is static and cannot be used as an instance pipe stage",
                call.Line,
                call.Column);
            return new UnknownType();
        }

        var argumentTypes = new List<TypeNode> { inputType };
        argumentTypes.AddRange(call.Arguments.Select(ResolveExpressionType));
        typeChecker.ValidateMethodCall(method, argumentTypes, call.Line, call.Column);

        pipeChecker.ValidateValuePipeTypeInvariant(inputType, method.ReturnType, line, column);
        return method.ReturnType;
    }

    private TypeNode ReportInvalidValuePipeStage(TypeNode leftType, PipeExpression pipe)
    {
        diagnostics.AddError(
            $"Pipe operator right operand must be a callable stage when piping '{leftType}' values",
            pipe.Line,
            pipe.Column);
        ResolveExpressionType(pipe.Right);
        return new UnknownType();
    }

    private TypeNode ResolveNullCoalesce(NullCoalesceExpression nullCoalesce)
    {
        var leftType = ResolveExpressionType(nullCoalesce.Left);
        var rightType = ResolveExpressionType(nullCoalesce.Right);
        nullabilityChecker.ValidateNullCoalesce(leftType, rightType, nullCoalesce.Line, nullCoalesce.Column);

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
        var argTypes = cmd.Arguments.Select(ResolveExpressionType).ToList();

        if (cmd.IsAsync)
        {
            return cmd.Kind switch
            {
                CommandKind.Exec => ResolveAsyncExecExpression(argTypes, cmd),
                CommandKind.Spawn => ResolveAsyncSpawnExpression(argTypes, cmd),
                _ => ReportUnsupportedAsyncCommand(cmd)
            };
        }

        return cmd.Kind switch
        {
            CommandKind.Cmd => ResolveCmdExpression(argTypes, cmd),
            CommandKind.Exec => ResolveExecExpression(argTypes, cmd),
            CommandKind.Spawn => ResolveSpawnExpression(argTypes, cmd),
            _ => new UnknownType()
        };
    }

    private TypeNode ResolveAwait(AwaitExpression await)
    {
        var awaitedType = ResolveExpressionType(await.Expression);
        if (awaitedType is NamedType { Name: "Process" })
            return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String };

        diagnostics.AddError(
            $"await expects a Process handle, got '{awaitedType}'",
            await.Line, await.Column);
        return new UnknownType();
    }

    private TypeNode ResolveCmdExpression(List<TypeNode> argTypes, CommandExpression cmd)
    {
        if (argTypes.Count == 1 && argTypes[0] is NamedType { Name: "Command" })
            return new NamedType { Name = "Command" };

        if (argTypes.Any(t => t is NamedType { Name: "Command" }))
        {
            diagnostics.AddError(
                "cmd(...) cannot mix Command values with positional arguments",
                cmd.Line, cmd.Column);
            return new UnknownType();
        }

        return new NamedType { Name = "Command" };
    }

    private TypeNode ResolveExecExpression(List<TypeNode> argTypes, CommandExpression cmd)
    {
        if (argTypes.Count == 1 && argTypes[0] is NamedType { Name: "Command" })
            return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String };

        if (argTypes.Any(t => t is NamedType { Name: "Command" }))
        {
            diagnostics.AddError(
                "exec(...) accepts either a single Command value or raw command arguments",
                cmd.Line, cmd.Column);
            return new UnknownType();
        }

        return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String };
    }

    private TypeNode ResolveSpawnExpression(List<TypeNode> argTypes, CommandExpression cmd)
    {
        if (argTypes.Count == 1 && argTypes[0] is NamedType { Name: "Command" })
            return new NamedType { Name = "Process" };

        if (argTypes.Any(t => t is NamedType { Name: "Command" }))
        {
            diagnostics.AddError(
                "spawn(...) accepts either a single Command value or raw command arguments",
                cmd.Line, cmd.Column);
            return new UnknownType();
        }

        return new NamedType { Name = "Process" };
    }

    private TypeNode ResolveAsyncExecExpression(List<TypeNode> argTypes, CommandExpression cmd)
    {
        if (argTypes.Count == 1 && argTypes[0] is NamedType { Name: "Command" })
            return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };

        if (argTypes.Any(t => t is NamedType { Name: "Command" }))
        {
            diagnostics.AddError(
                "async exec(...) accepts either a single Command value or raw command arguments",
                cmd.Line, cmd.Column);
            return new UnknownType();
        }

        return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };
    }

    private TypeNode ResolveAsyncSpawnExpression(List<TypeNode> argTypes, CommandExpression cmd)
    {
        if (argTypes.Count == 1 && argTypes[0] is NamedType { Name: "Command" })
            return new NamedType { Name = "Process" };

        if (argTypes.Any(t => t is NamedType { Name: "Command" }))
        {
            diagnostics.AddError(
                "async spawn(...) accepts either a single Command value or raw command arguments",
                cmd.Line, cmd.Column);
            return new UnknownType();
        }

        return new NamedType { Name = "Process" };
    }

    private TypeNode ReportUnsupportedAsyncCommand(CommandExpression cmd)
    {
        diagnostics.AddError(
            $"async {cmd.Kind.ToString().ToLowerInvariant()}(...) is not supported in this compiler version",
            cmd.Line, cmd.Column);
        return new UnknownType();
    }

    private TypeNode ResolveSelf()
    {
        if (currentTypeName == null)
        {
            diagnostics.AddError("Cannot use 'self' outside of an impl method", 0, 0);
            return new UnknownType();
        }

        return new NamedType { Name = currentTypeName };
    }

    public void SetCurrentTypeContext(string? typeName)
    {
        currentTypeName = typeName;
    }
}
