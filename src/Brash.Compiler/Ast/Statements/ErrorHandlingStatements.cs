namespace Brash.Compiler.Ast.Statements;

// ============================================
// Error Handling Statements
// ============================================

public class TryStatement : Statement
{
    public List<Statement> TryBlock { get; set; } = new();
    public string ErrorVariable { get; set; } = string.Empty;
    public List<Statement> CatchBlock { get; set; } = new();
}

public class ThrowStatement : Statement
{
    public Expression Value { get; set; } = null!;
}
