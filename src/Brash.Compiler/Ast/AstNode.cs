namespace Brash.Compiler.Ast;

// ============================================
// Base AST Node
// ============================================

public abstract class AstNode
{
    public int Line { get; set; }
    public int Column { get; set; }
}

// ============================================
// Program Root
// ============================================

public class ProgramNode : AstNode
{
    public List<PreprocessorDirective> Directives { get; set; } = new();
    public List<Statement> Statements { get; set; } = new();
}

// ============================================
// Preprocessor Directives
// ============================================

public abstract class PreprocessorDirective : AstNode { }

public class DefineDirective : PreprocessorDirective
{
    public string Name { get; set; } = string.Empty;
    public Expression Value { get; set; } = null!;
}

public class UndefDirective : PreprocessorDirective
{
    public string Name { get; set; } = string.Empty;
}

public class IfDirective : PreprocessorDirective
{
    public Expression Condition { get; set; } = null!;
    public List<Statement> ThenStatements { get; set; } = new();
    public List<Statement> ElseStatements { get; set; } = new();
}

// ============================================
// Statements
// ============================================

public abstract class Statement : AstNode { }

public class VariableDeclaration : Statement
{
    public enum VarKind { Let, Mut, Const }

    public VarKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public TypeNode? Type { get; set; }
    public Expression Value { get; set; } = null!;
}

public class Assignment : Statement
{
    public Expression Target { get; set; } = null!;
    public Expression Value { get; set; } = null!;
}

public class FunctionDeclaration : Statement
{
    public bool IsAsync { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Parameter> Parameters { get; set; } = new();
    public TypeNode? ReturnType { get; set; }
    public List<Statement> Body { get; set; } = new();
}

public class Parameter
{
    public string Name { get; set; } = string.Empty;
    public TypeNode Type { get; set; } = null!;
    public Expression? DefaultValue { get; set; }
}

public class StructDeclaration : Statement
{
    public string Name { get; set; } = string.Empty;
    public List<FieldDeclaration> Fields { get; set; } = new();
}

public class RecordDeclaration : Statement
{
    public string Name { get; set; } = string.Empty;
    public List<FieldDeclaration> Fields { get; set; } = new();
}

public class FieldDeclaration
{
    public string Name { get; set; } = string.Empty;
    public TypeNode Type { get; set; } = null!;
}

public class ImplBlock : Statement
{
    public string TypeName { get; set; } = string.Empty;
    public List<MethodDeclaration> Methods { get; set; } = new();
}

public class MethodDeclaration
{
    public string Name { get; set; } = string.Empty;
    public List<Parameter> Parameters { get; set; } = new();
    public TypeNode? ReturnType { get; set; }
    public List<Statement> Body { get; set; } = new();
}

public class IfStatement : Statement
{
    public Expression Condition { get; set; } = null!;
    public List<Statement> ThenBlock { get; set; } = new();
    public List<ElifClause> ElifClauses { get; set; } = new();
    public List<Statement>? ElseBlock { get; set; }
}

public class ElifClause
{
    public Expression Condition { get; set; } = null!;
    public List<Statement> Block { get; set; } = new();
}

public class ForLoop : Statement
{
    public bool IsIncrementing { get; set; } = true;
    public string Variable { get; set; } = string.Empty;
    public Expression Range { get; set; } = null!;
    public Expression? Step { get; set; }
    public List<Statement> Body { get; set; } = new();
}

public class WhileLoop : Statement
{
    public Expression Condition { get; set; } = null!;
    public List<Statement> Body { get; set; } = new();
}

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

public class ReturnStatement : Statement
{
    public Expression? Value { get; set; }
}

public class BreakStatement : Statement { }

public class ContinueStatement : Statement { }

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

// ============================================
// Expressions
// ============================================

public abstract class Expression : AstNode { }

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
}

public class AwaitExpression : Expression
{
    public Expression Expression { get; set; } = null!;
}

public class NullLiteral : Expression { }

public class SelfExpression : Expression { }

// ============================================
// Types
// ============================================

public abstract class TypeNode : AstNode { }

public class PrimitiveType : TypeNode
{
    public enum Kind { Int, Float, String, Bool, Char, Void }

    public Kind PrimitiveKind { get; set; }

    public override string ToString() => PrimitiveKind.ToString().ToLower();
}

public class ArrayType : TypeNode
{
    public TypeNode ElementType { get; set; } = null!;

    public override string ToString() => $"{ElementType}[]";
}

public class MapType : TypeNode
{
    public TypeNode KeyType { get; set; } = null!;
    public TypeNode ValueType { get; set; } = null!;

    public override string ToString() => $"map<{KeyType}, {ValueType}>";
}

public class NullableType : TypeNode
{
    public TypeNode BaseType { get; set; } = null!;

    public override string ToString() => $"{BaseType}?";
}

public class TupleType : TypeNode
{
    public List<TypeNode> ElementTypes { get; set; } = new();

    public override string ToString() => $"({string.Join(", ", ElementTypes)})";
}

public class NamedType : TypeNode
{
    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}
