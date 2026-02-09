namespace Brash.Compiler.Semantic.Checkers;

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
        if (target is IdentifierExpression ident)
        {
            return ValidateVariableAssignment(ident.Name, line, column);
        }

        if (target is MemberAccessExpression member)
        {
            return ValidateMemberAssignment(member, line, column);
        }

        return true;
    }

    private bool ValidateVariableAssignment(string name, int line, int column)
    {
        var symbol = symbolTable.LookupVariable(name);
        if (symbol == null)
        {
            diagnostics.AddError(
                $"Undefined variable '{name}'",
                line, column);
            return false;
        }

        if (!symbol.IsMutable)
        {
            diagnostics.AddError(
                $"Cannot assign to immutable variable '{name}'. Declare with 'let mut' or mark parameter as 'mut'.",
                line, column);
            return false;
        }

        return true;
    }

    private bool ValidateMemberAssignment(MemberAccessExpression member, int line, int column)
    {
        if (member.Object is not IdentifierExpression ident)
            return true;

        if (!ValidateVariableAssignment(ident.Name, line, column))
            return false;

        return true;
    }
}
