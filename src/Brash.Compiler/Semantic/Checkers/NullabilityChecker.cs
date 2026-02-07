namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Diagnostics;

/// <summary>
/// Centralized nullability validation and diagnostics.
/// </summary>
public class NullabilityChecker
{
    private readonly DiagnosticBag diagnostics;
    private readonly TypeChecker typeChecker;

    public NullabilityChecker(DiagnosticBag diagnostics, TypeChecker typeChecker)
    {
        this.diagnostics = diagnostics;
        this.typeChecker = typeChecker;
    }

    public bool IsNullLiteralType(TypeNode type)
    {
        return type is NullableType
        {
            BaseType: PrimitiveType { PrimitiveKind: PrimitiveType.Kind.Void }
        };
    }

    public TypeNode RequireNonNullable(TypeNode type, int line, int column, string context)
    {
        if (type is NullableType nullable)
        {
            diagnostics.AddWarning(
                $"Possible null reference in {context}: type '{type}' may be null",
                line, column);
            return nullable.BaseType;
        }

        return type;
    }

    public void ValidateNullCoalesce(TypeNode leftType, TypeNode rightType, int line, int column)
    {
        if (leftType is not NullableType && !IsNullLiteralType(leftType))
        {
            diagnostics.AddWarning(
                "Left side of '??' is not nullable; expression is redundant",
                line, column);
            return;
        }

        if (leftType is NullableType nullableLeft &&
            !typeChecker.AreTypesCompatible(nullableLeft.BaseType, rightType))
        {
            diagnostics.AddError(
                $"Right side of '??' must be compatible with '{nullableLeft.BaseType}', got '{rightType}'",
                line, column);
        }
    }
}
