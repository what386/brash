namespace Brash.Compiler.Ast.Expressions;

// ============================================
// Operator Expressions
// ============================================

public class BinaryExpression : Expression
{
    public Expression Left { get; set; } = null!;
    public string Operator { get; set; } = string.Empty;
    public Expression Right { get; set; } = null!;
}

public class UnaryExpression : Expression
{
    public string Operator { get; set; } = string.Empty;
    public Expression Operand { get; set; } = null!;
}

public class CastExpression : Expression
{
    public TypeNode TargetType { get; set; } = null!;
    public Expression Value { get; set; } = null!;
}

public class RangeExpression : Expression
{
    public Expression Start { get; set; } = null!;
    public Expression End { get; set; } = null!;
}

public class PipeExpression : Expression
{
    public Expression Left { get; set; } = null!;
    public Expression Right { get; set; } = null!;
}

public class NullCoalesceExpression : Expression
{
    public Expression Left { get; set; } = null!;
    public Expression Right { get; set; } = null!;
}
