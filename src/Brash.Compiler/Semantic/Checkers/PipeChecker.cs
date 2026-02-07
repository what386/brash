namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;

/// <summary>
/// Validates pipe operator type rules.
/// </summary>
public class PipeChecker
{
    private readonly DiagnosticBag diagnostics;
    private readonly TypeChecker typeChecker;

    public PipeChecker(DiagnosticBag diagnostics, TypeChecker typeChecker)
    {
        this.diagnostics = diagnostics;
        this.typeChecker = typeChecker;
    }

    public bool IsCommandType(TypeNode type)
    {
        return type is NamedType { Name: "Command" };
    }

    public void ValidateCommandPipe(TypeNode leftType, TypeNode rightType, int line, int column)
    {
        if (!IsCommandType(leftType))
            diagnostics.AddError(
                $"Pipe operator left operand must be of type 'Command', got '{leftType}'",
                line, column);

        if (!IsCommandType(rightType))
            diagnostics.AddError(
                $"Pipe operator right operand must be of type 'Command', got '{rightType}'",
                line, column);
    }

    public bool ValidateValuePipeTypeInvariant(TypeNode inputType, TypeNode outputType, int line, int column)
    {
        var outputCompatibleWithInput = typeChecker.AreTypesCompatible(inputType, outputType);
        var inputCompatibleWithOutput = typeChecker.AreTypesCompatible(outputType, inputType);
        if (outputCompatibleWithInput && inputCompatibleWithOutput)
            return true;

        diagnostics.AddError(
            $"Pipe value stage must preserve type: input '{inputType}' but output '{outputType}'",
            line,
            column);
        return false;
    }
}
