namespace Brash.Compiler.Ast.Expressions;

// ============================================
// Async & Command Expressions
// ============================================

public class CommandExpression : Expression
{
    public List<Expression> Arguments { get; set; } = new();
    public bool IsAsync { get; set; }
    public bool IsExec { get; set; }
    public string? MethodName { get; set; } // For .exec() or .exec_async()
}

public class AwaitExpression : Expression
{
    public Expression Expression { get; set; } = null!;
}
