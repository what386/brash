namespace Brash.Compiler.Diagnostics;

/// <summary>
/// Standard diagnostic codes for the Brash compiler
/// </summary>
public static class DiagnosticCodes
{
    // ============================================
    // Syntax Errors (E001-E099)
    // ============================================

    public const string SyntaxError = "E001";
    public const string UnexpectedToken = "E002";
    public const string MissingToken = "E003";

    // ============================================
    // Type Errors (E100-E199)
    // ============================================

    public const string TypeMismatch = "E100";
    public const string UndefinedType = "E101";
    public const string InvalidTypeConversion = "E102";
    public const string IncompatibleTypes = "E103";
    public const string CannotInferType = "E104";
    public const string InvalidOperandType = "E105";
    public const string InvalidIndexType = "E106";

    // ============================================
    // Symbol Errors (E200-E299)
    // ============================================

    public const string UndefinedVariable = "E200";
    public const string UndefinedFunction = "E201";
    public const string UndefinedMethod = "E202";
    public const string UndefinedField = "E203";
    public const string DuplicateDeclaration = "E204";
    public const string DuplicateParameter = "E205";
    public const string DuplicateField = "E206";

    // ============================================
    // Mutability Errors (E300-E399)
    // ============================================

    public const string AssignToImmutable = "E300";
    public const string ModifyImmutableStruct = "E301";

    // ============================================
    // Function/Method Errors (E400-E499)
    // ============================================

    public const string WrongArgumentCount = "E400";
    public const string InvalidArgument = "E401";
    public const string MissingReturnValue = "E402";
    public const string InvalidReturnType = "E403";
    public const string ReturnOutsideFunction = "E404";

    // ============================================
    // Control Flow Errors (E500-E599)
    // ============================================

    public const string BreakOutsideLoop = "E500";
    public const string ContinueOutsideLoop = "E501";
    public const string InvalidConditionType = "E502";

    // ============================================
    // Nullability Errors (E600-E699)
    // ============================================

    public const string NullReferenceError = "E600";
    public const string CannotAssignNullToNonNullable = "E601";

    // ============================================
    // Struct Errors (E700-E799)
    // ============================================

    public const string MissingStructField = "E700";
    public const string UnknownStructField = "E701";
    public const string InvalidStructInitialization = "E702";

    // ============================================
    // Warnings (W001-W999)
    // ============================================

    public const string UnusedVariable = "W001";
    public const string UnusedParameter = "W002";
    public const string UnusedFunction = "W003";
    public const string PossibleNullReference = "W100";
    public const string ImplicitTypeConversion = "W101";
    public const string UnreachableCode = "W200";
    public const string MissingReturnPath = "W201";
    public const string ShadowedVariable = "W300";

    // ============================================
    // Info (I001-I999)
    // ============================================

    public const string InfoGeneral = "I001";
}
