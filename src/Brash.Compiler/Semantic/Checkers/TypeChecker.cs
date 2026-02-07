namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;

public class TypeChecker
{
    private readonly DiagnosticBag diagnostics;
    private readonly SymbolTable symbolTable;

    public TypeChecker(DiagnosticBag diagnostics, SymbolTable symbolTable)
    {
        this.diagnostics = diagnostics;
        this.symbolTable = symbolTable;
    }

    // ============================================
    // Type Compatibility Checking
    // ============================================

    public bool AreTypesCompatible(TypeNode expected, TypeNode actual, bool allowNullToNullable = true)
    {
        // Same type
        if (expected.Equals(actual))
            return true;

        // null can be assigned to nullable types (null literal currently resolves as nullable void)
        if (allowNullToNullable &&
            expected is NullableType &&
            actual is NullableType { BaseType: PrimitiveType { PrimitiveKind: PrimitiveType.Kind.Void } })
            return true;

        // Nullable type compatibility
        if (expected is NullableType expectedNullable && actual is NullableType actualNullable)
            return AreTypesCompatible(expectedNullable.BaseType, actualNullable.BaseType, false);

        // Non-nullable to nullable (implicit conversion)
        if (expected is NullableType nullableExpected)
            return AreTypesCompatible(nullableExpected.BaseType, actual, false);

        // Array type compatibility
        if (expected is ArrayType expectedArray && actual is ArrayType actualArray)
            return AreTypesCompatible(expectedArray.ElementType, actualArray.ElementType, false);

        // Map type compatibility
        if (expected is MapType expectedMap && actual is MapType actualMap)
            return AreTypesCompatible(expectedMap.KeyType, actualMap.KeyType, false) &&
                   AreTypesCompatible(expectedMap.ValueType, actualMap.ValueType, false);

        // Tuple type compatibility
        if (expected is TupleType expectedTuple && actual is TupleType actualTuple)
        {
            if (expectedTuple.ElementTypes.Count != actualTuple.ElementTypes.Count)
                return false;

            for (int i = 0; i < expectedTuple.ElementTypes.Count; i++)
            {
                if (!AreTypesCompatible(expectedTuple.ElementTypes[i], actualTuple.ElementTypes[i], false))
                    return false;
            }

            return true;
        }

        // Named type compatibility (structs/enums)
        if (expected is NamedType expectedNamed && actual is NamedType actualNamed)
            return expectedNamed.Name == actualNamed.Name;

        return false;
    }

    public bool IsNumericType(TypeNode type)
    {
        if (type is PrimitiveType prim)
            return prim.PrimitiveKind == PrimitiveType.Kind.Int ||
                   prim.PrimitiveKind == PrimitiveType.Kind.Float;

        return false;
    }

    public bool IsBooleanType(TypeNode type)
    {
        return type is PrimitiveType prim && prim.PrimitiveKind == PrimitiveType.Kind.Bool;
    }

    public bool IsStringType(TypeNode type)
    {
        return type is PrimitiveType prim && prim.PrimitiveKind == PrimitiveType.Kind.String;
    }

    public bool IsNullable(TypeNode type)
    {
        return type is NullableType;
    }

    public TypeNode GetBaseType(TypeNode type)
    {
        return type is NullableType nullable ? nullable.BaseType : type;
    }

    // ============================================
    // Type Inference
    // ============================================

    public TypeNode InferBinaryExpressionType(string op, TypeNode leftType, TypeNode rightType)
    {
        // Arithmetic operators
        if (op is "+" or "-" or "*" or "/" or "%")
        {
            if (IsNumericType(leftType) && IsNumericType(rightType))
            {
                // If either is float, result is float
                if (leftType is PrimitiveType { PrimitiveKind: PrimitiveType.Kind.Float } ||
                    rightType is PrimitiveType { PrimitiveKind: PrimitiveType.Kind.Float })
                {
                    return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Float };
                }
                return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int };
            }

            // String concatenation with +
            if (op == "+" && (IsStringType(leftType) || IsStringType(rightType)))
            {
                return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String };
            }
        }

        // Comparison operators
        if (op is "==" or "!=" or "<" or ">" or "<=" or ">=")
        {
            return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Bool };
        }

        // Logical operators
        if (op is "&&" or "||")
        {
            return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Bool };
        }

        // Range operator
        if (op == "..")
        {
            // Returns a special range type (could be represented as a tuple or custom type)
            return new TupleType
            {
                ElementTypes = new List<TypeNode> { leftType, rightType }
            };
        }

        // Default: return left type
        return leftType;
    }

    public TypeNode InferUnaryExpressionType(string op, TypeNode operandType)
    {
        return op switch
        {
            "!" => new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Bool },
            "-" or "+" => operandType, // Numeric negation/positive
            _ => operandType
        };
    }

    // ============================================
    // Type Validation
    // ============================================

    public void ValidateArithmeticOperation(string op, TypeNode leftType, TypeNode rightType, int line, int column)
    {
        if (!IsNumericType(leftType))
        {
            diagnostics.AddError(
                $"Operator '{op}' cannot be applied to operand of type '{leftType}'",
                line, column);
        }

        if (!IsNumericType(rightType))
        {
            diagnostics.AddError(
                $"Operator '{op}' cannot be applied to operand of type '{rightType}'",
                line, column);
        }
    }

    public void ValidateLogicalOperation(string op, TypeNode leftType, TypeNode rightType, int line, int column)
    {
        if (!IsBooleanType(leftType))
        {
            diagnostics.AddWarning(
                $"Logical operator '{op}' expects boolean operand, got '{leftType}'",
                line, column);
        }

        if (!IsBooleanType(rightType))
        {
            diagnostics.AddWarning(
                $"Logical operator '{op}' expects boolean operand, got '{rightType}'",
                line, column);
        }
    }

    public void ValidateCondition(TypeNode conditionType, int line, int column)
    {
        if (!IsBooleanType(conditionType))
        {
            diagnostics.AddWarning(
                $"Condition should be boolean, got '{conditionType}'",
                line, column);
        }
    }

    public void ValidateAssignment(TypeNode targetType, TypeNode valueType, int line, int column)
    {
        if (!AreTypesCompatible(targetType, valueType))
        {
            diagnostics.AddError(
                $"Cannot assign value of type '{valueType}' to variable of type '{targetType}'",
                line, column);
        }
    }

    public void ValidateFunctionCall(FunctionSymbol function, List<TypeNode> argumentTypes, int line, int column)
    {
        if (argumentTypes.Count != function.ParameterTypes.Count)
        {
            diagnostics.AddError(
                $"Function '{function.Name}' expects {function.ParameterTypes.Count} arguments, got {argumentTypes.Count}",
                line, column);
            return;
        }

        for (int i = 0; i < argumentTypes.Count; i++)
        {
            if (!AreTypesCompatible(function.ParameterTypes[i], argumentTypes[i]))
            {
                diagnostics.AddError(
                    $"Argument {i + 1} to '{function.Name}': expected '{function.ParameterTypes[i]}', got '{argumentTypes[i]}'",
                    line, column);
            }
        }
    }

    public void ValidateMethodCall(MethodSymbol method, List<TypeNode> argumentTypes, int line, int column)
    {
        if (argumentTypes.Count != method.ParameterTypes.Count)
        {
            diagnostics.AddError(
                $"Method '{method.Name}' expects {method.ParameterTypes.Count} arguments, got {argumentTypes.Count}",
                line, column);
            return;
        }

        for (int i = 0; i < argumentTypes.Count; i++)
        {
            if (!AreTypesCompatible(method.ParameterTypes[i], argumentTypes[i]))
            {
                diagnostics.AddError(
                    $"Argument {i + 1} to '{method.Name}': expected '{method.ParameterTypes[i]}', got '{argumentTypes[i]}'",
                    line, column);
            }
        }
    }

    public void ValidateReturnType(TypeNode expectedType, TypeNode actualType, int line, int column)
    {
        if (!AreTypesCompatible(expectedType, actualType))
        {
            diagnostics.AddError(
                $"Cannot return value of type '{actualType}' from function expecting '{expectedType}'",
                line, column);
        }
    }

    // ============================================
    // Special Type Checks
    // ============================================

    public void ValidateIndexAccess(TypeNode arrayType, TypeNode indexType, int line, int column)
    {
        if (arrayType is not ArrayType && arrayType is not MapType)
        {
            diagnostics.AddError(
                $"Cannot index into type '{arrayType}'",
                line, column);
            return;
        }

        if (arrayType is ArrayType)
        {
            if (!IsNumericType(indexType))
            {
                diagnostics.AddError(
                    $"Array index must be numeric, got '{indexType}'",
                    line, column);
            }
        }
        else if (arrayType is MapType mapType)
        {
            if (!AreTypesCompatible(mapType.KeyType, indexType))
            {
                diagnostics.AddError(
                    $"Map key type is '{mapType.KeyType}', got '{indexType}'",
                    line, column);
            }
        }
    }

    public void ValidateMemberAccess(TypeNode objectType, string memberName, int line, int column)
    {
        if (objectType is NamedType namedType)
        {
            var typeSymbol = symbolTable.LookupType(namedType.Name);
            if (typeSymbol == null)
            {
                diagnostics.AddError(
                    $"Unknown type '{namedType.Name}'",
                    line, column);
                return;
            }

            if (!typeSymbol.Fields.ContainsKey(memberName))
            {
                diagnostics.AddError(
                    $"Type '{namedType.Name}' has no field '{memberName}'",
                    line, column);
            }
        }
        else
        {
            diagnostics.AddError(
                $"Cannot access member '{memberName}' on type '{objectType}'",
                line, column);
        }
    }

    // ============================================
    // Nullability Checks
    // ============================================

    public void CheckNullSafety(TypeNode type, int line, int column, string context = "")
    {
        if (type is not NullableType)
            return;

        string message = string.IsNullOrEmpty(context)
            ? $"Possible null reference: type '{type}' may be null"
            : $"Possible null reference in {context}: type '{type}' may be null";

        diagnostics.AddWarning(message, line, column);
    }

    public TypeNode UnwrapNullable(TypeNode type)
    {
        return type is NullableType nullable ? nullable.BaseType : type;
    }
}
