namespace Brash.Compiler.Diagnostics;

using Brash.Compiler.Ast;

/// <summary>
/// Extension methods for adding common diagnostics with proper error codes
/// </summary>
public static class DiagnosticExtensions
{
    // ============================================
    // Type Errors
    // ============================================

    public static void ReportTypeMismatch(this DiagnosticBag diagnostics,
        TypeNode expected, TypeNode actual, int line, int column)
    {
        diagnostics.AddError(
            $"Type mismatch: expected '{expected}', got '{actual}'",
            line, column, DiagnosticCodes.TypeMismatch);
    }

    public static void ReportUndefinedType(this DiagnosticBag diagnostics,
        string typeName, int line, int column)
    {
        diagnostics.AddError(
            $"Undefined type '{typeName}'",
            line, column, DiagnosticCodes.UndefinedType);
    }

    public static void ReportInvalidOperandType(this DiagnosticBag diagnostics,
        string op, TypeNode type, int line, int column)
    {
        diagnostics.AddError(
            $"Operator '{op}' cannot be applied to operand of type '{type}'",
            line, column, DiagnosticCodes.InvalidOperandType);
    }

    // ============================================
    // Symbol Errors
    // ============================================

    public static void ReportUndefinedVariable(this DiagnosticBag diagnostics,
        string name, int line, int column)
    {
        diagnostics.AddError(
            $"Undefined variable '{name}'",
            line, column, DiagnosticCodes.UndefinedVariable);
    }

    public static void ReportUndefinedFunction(this DiagnosticBag diagnostics,
        string name, int line, int column)
    {
        diagnostics.AddError(
            $"Undefined function '{name}'",
            line, column, DiagnosticCodes.UndefinedFunction);
    }

    public static void ReportUndefinedMethod(this DiagnosticBag diagnostics,
        string typeName, string methodName, int line, int column)
    {
        diagnostics.AddError(
            $"Type '{typeName}' has no method '{methodName}'",
            line, column, DiagnosticCodes.UndefinedMethod);
    }

    public static void ReportUndefinedField(this DiagnosticBag diagnostics,
        string typeName, string fieldName, int line, int column)
    {
        diagnostics.AddError(
            $"Type '{typeName}' has no field '{fieldName}'",
            line, column, DiagnosticCodes.UndefinedField);
    }

    public static void ReportDuplicateDeclaration(this DiagnosticBag diagnostics,
        string name, string kind, int line, int column)
    {
        diagnostics.AddError(
            $"{kind} '{name}' is already declared",
            line, column, DiagnosticCodes.DuplicateDeclaration);
    }

    // ============================================
    // Mutability Errors
    // ============================================

    public static void ReportAssignToImmutable(this DiagnosticBag diagnostics,
        string name, int line, int column)
    {
        diagnostics.AddError(
            $"Cannot assign to immutable variable '{name}'",
            line, column, DiagnosticCodes.AssignToImmutable);
    }

    // ============================================
    // Function/Method Errors
    // ============================================

    public static void ReportWrongArgumentCount(this DiagnosticBag diagnostics,
        string name, int expected, int actual, int line, int column)
    {
        diagnostics.AddError(
            $"Function '{name}' expects {expected} argument{(expected != 1 ? "s" : "")}, got {actual}",
            line, column, DiagnosticCodes.WrongArgumentCount);
    }

    public static void ReportInvalidArgument(this DiagnosticBag diagnostics,
        string functionName, int paramIndex, TypeNode expected, TypeNode actual, int line, int column)
    {
        diagnostics.AddError(
            $"Argument {paramIndex + 1} to '{functionName}': expected '{expected}', got '{actual}'",
            line, column, DiagnosticCodes.InvalidArgument);
    }

    public static void ReportInvalidReturnType(this DiagnosticBag diagnostics,
        TypeNode expected, TypeNode actual, int line, int column)
    {
        diagnostics.AddError(
            $"Cannot return value of type '{actual}' from function expecting '{expected}'",
            line, column, DiagnosticCodes.InvalidReturnType);
    }

    public static void ReportReturnOutsideFunction(this DiagnosticBag diagnostics,
        int line, int column)
    {
        diagnostics.AddError(
            "Return statement outside of function",
            line, column, DiagnosticCodes.ReturnOutsideFunction);
    }

    // ============================================
    // Control Flow Errors
    // ============================================

    public static void ReportBreakOutsideLoop(this DiagnosticBag diagnostics,
        int line, int column)
    {
        diagnostics.AddError(
            "Break statement outside of loop",
            line, column, DiagnosticCodes.BreakOutsideLoop);
    }

    public static void ReportContinueOutsideLoop(this DiagnosticBag diagnostics,
        int line, int column)
    {
        diagnostics.AddError(
            "Continue statement outside of loop",
            line, column, DiagnosticCodes.ContinueOutsideLoop);
    }

    // ============================================
    // Nullability Warnings
    // ============================================

    public static void ReportPossibleNullReference(this DiagnosticBag diagnostics,
        TypeNode type, int line, int column, string? context = null)
    {
        var message = context != null
            ? $"Possible null reference in {context}: type '{type}' may be null"
            : $"Possible null reference: type '{type}' may be null";

        diagnostics.AddWarning(message, line, column, DiagnosticCodes.PossibleNullReference);
    }

    // ============================================
    // Struct Errors
    // ============================================

    public static void ReportMissingStructField(this DiagnosticBag diagnostics,
        string typeName, IEnumerable<string> missingFields, int line, int column)
    {
        diagnostics.AddError(
            $"Missing fields in '{typeName}' initialization: {string.Join(", ", missingFields)}",
            line, column, DiagnosticCodes.MissingStructField);
    }

    public static void ReportUnknownStructField(this DiagnosticBag diagnostics,
        string typeName, string fieldName, int line, int column)
    {
        diagnostics.AddError(
            $"Type '{typeName}' has no field '{fieldName}'",
            line, column, DiagnosticCodes.UnknownStructField);
    }

    // ============================================
    // General Warnings
    // ============================================

    public static void ReportUnusedVariable(this DiagnosticBag diagnostics,
        string name, int line, int column)
    {
        diagnostics.AddWarning(
            $"Variable '{name}' is declared but never used",
            line, column, DiagnosticCodes.UnusedVariable);
    }

    public static void ReportShadowedVariable(this DiagnosticBag diagnostics,
        string name, int line, int column)
    {
        diagnostics.AddWarning(
            $"Variable '{name}' shadows a variable from an outer scope",
            line, column, DiagnosticCodes.ShadowedVariable);
    }

    public static void ReportImplicitConversion(this DiagnosticBag diagnostics,
        TypeNode from, TypeNode to, int line, int column)
    {
        diagnostics.AddWarning(
            $"Implicit conversion from '{from}' to '{to}'",
            line, column, DiagnosticCodes.ImplicitTypeConversion);
    }
}
