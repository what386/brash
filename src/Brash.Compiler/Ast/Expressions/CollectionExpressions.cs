namespace Brash.Compiler.Ast.Expressions;

// ============================================
// Collection & Literal Expressions
// ============================================

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

public class EnumLiteral : Expression
{
    public string EnumName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public List<Expression> AssociatedValues { get; set; } = new();
}
