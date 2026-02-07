namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Diagnostics;

/// <summary>
/// Validates assignment mutability rules.
/// </summary>
public class MutabilityChecker
{
    private readonly DiagnosticBag diagnostics;
    private readonly SymbolTable symbolTable;

    public MutabilityChecker(DiagnosticBag diagnostics, SymbolTable symbolTable)
    {
        this.diagnostics = diagnostics;
        this.symbolTable = symbolTable;
    }

    public bool ValidateAssignmentTarget(Expression target, int line, int column)
    {
        if (target is not IdentifierExpression ident)
            return true;

        var symbol = symbolTable.LookupVariable(ident.Name);
        if (symbol == null)
        {
            diagnostics.AddError(
                $"Undefined variable '{ident.Name}'",
                line, column);
            return false;
        }

        if (!symbol.IsMutable)
        {
            diagnostics.AddError(
                $"Cannot assign to immutable variable '{ident.Name}'. Declare with 'let mut' or mark parameter as 'mut'.",
                line, column);
            return false;
        }

        return true;
    }
}
