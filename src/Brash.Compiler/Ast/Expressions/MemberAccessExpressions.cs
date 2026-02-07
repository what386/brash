namespace Brash.Compiler.Ast.Expressions;

// ============================================
// Member Access & Call Expressions
// ============================================

public class FunctionCallExpression : Expression
{
    public string FunctionName { get; set; } = string.Empty;
    public List<Expression> Arguments { get; set; } = new();
}

public class MethodCallExpression : Expression
{
    public Expression Object { get; set; } = null!;
    public string MethodName { get; set; } = string.Empty;
    public List<Expression> Arguments { get; set; } = new();
}

public class MemberAccessExpression : Expression
{
    public Expression Object { get; set; } = null!;
    public string MemberName { get; set; } = string.Empty;
}

public class IndexAccessExpression : Expression
{
    public Expression Array { get; set; } = null!;
    public Expression Index { get; set; } = null!;
}

public class SafeNavigationExpression : Expression
{
    public Expression Object { get; set; } = null!;
    public string MemberName { get; set; } = string.Empty;
}
