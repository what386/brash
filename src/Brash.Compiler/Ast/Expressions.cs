namespace Brash.Compiler.Ast;

// ============================================
// Simple Expression Types
// ============================================

public class LiteralExpression : Expression
{
    public object Value { get; set; } = null!;
    public TypeNode Type { get; set; } = null!;
}

public class IdentifierExpression : Expression
{
    public string Name { get; set; } = string.Empty;
}

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

public class ArrayLiteral : Expression
{
    public List<Expression> Elements { get; set; } = new();
}

public class MapLiteral : Expression
{
    public List<(Expression Key, Expression Value)> Entries { get; set; } = new();
}

public class StructLiteral : Expression
{
    public string TypeName { get; set; } = string.Empty;
    public List<(string Field, Expression Value)> Fields { get; set; } = new();
}

public class TupleExpression : Expression
{
    public List<Expression> Elements { get; set; } = new();
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

public class SafeNavigationExpression : Expression
{
    public Expression Object { get; set; } = null!;
    public string MemberName { get; set; } = string.Empty;
}

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

public class NullLiteral : Expression { }

public class SelfExpression : Expression { }

public class ParenthesizedExpression : Expression
{
    public Expression Expression { get; set; } = null!;
}

public class EnumLiteral : Expression
{
    public string EnumName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public List<Expression> AssociatedValues { get; set; } = new();
}
