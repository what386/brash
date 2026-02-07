namespace Brash.Compiler.Ast.Expressions;

// ============================================
// Primary Expressions
// ============================================

public class LiteralExpression : Expression
{
    public object Value { get; set; } = null!;
    public TypeNode Type { get; set; } = null!;
    public bool IsInterpolated { get; set; }
    public bool IsMultiline { get; set; }
}

public class IdentifierExpression : Expression
{
    public string Name { get; set; } = string.Empty;
}

public class NullLiteral : Expression { }

public class SelfExpression : Expression { }

public class ParenthesizedExpression : Expression
{
    public Expression Expression { get; set; } = null!;
}
