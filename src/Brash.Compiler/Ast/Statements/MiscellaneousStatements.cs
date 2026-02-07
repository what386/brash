namespace Brash.Compiler.Ast.Statements;

// ============================================
// Miscellaneous Statements
// ============================================

public class Assignment : Statement
{
    public Expression Target { get; set; } = null!;
    public Expression Value { get; set; } = null!;
}

public class ImportStatement : Statement
{
    public string? Module { get; set; }
    public List<string> ImportedItems { get; set; } = new();
    public string? FromModule { get; set; }
}

public class ExpressionStatement : Statement
{
    public Expression Expression { get; set; } = null!;
}
