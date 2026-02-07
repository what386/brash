namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;

/// <summary>
/// Validates pipe operator type rules.
/// </summary>
public class PipeChecker
{
    private readonly DiagnosticBag diagnostics;

    public PipeChecker(DiagnosticBag diagnostics)
    {
        this.diagnostics = diagnostics;
    }

    public void ValidatePipeTypes(TypeNode leftType, TypeNode rightType, int line, int column)
    {
        if (!IsCommandType(leftType))
        {
            diagnostics.AddError(
                $"Pipe operator left operand must be of type 'Command', got '{leftType}'",
                line, column);
        }

        if (!IsCommandType(rightType))
        {
            diagnostics.AddError(
                $"Pipe operator right operand must be of type 'Command', got '{rightType}'",
                line, column);
        }
    }

    private static bool IsCommandType(TypeNode type)
    {
        return type is NamedType { Name: "Command" };
    }
}
