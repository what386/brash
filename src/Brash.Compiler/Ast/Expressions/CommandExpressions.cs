namespace Brash.Compiler.Ast.Expressions;

// ============================================
// Async & Command Expressions
// ============================================

public enum CommandKind
{
    Cmd,
    Exec,
    Spawn
}

public class CommandExpression : Expression
{
    public List<Expression> Arguments { get; set; } = new();
    public CommandKind Kind { get; set; } = CommandKind.Cmd;
    public bool IsAsync { get; set; }
}

public class AwaitExpression : Expression
{
    public Expression Expression { get; set; } = null!;
}
