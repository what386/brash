namespace Brash.Compiler.Ast;

// ============================================
// Simple Statement Types
// ============================================

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

public class EnumDeclaration : Statement
{
    public string Name { get; set; } = string.Empty;
    public List<EnumVariant> Variants { get; set; } = new();
}

public class ImplBlock : Statement
{
    public string TypeName { get; set; } = string.Empty;
    public List<MethodDeclaration> Methods { get; set; } = new();
}

public class MethodDeclaration : AstNode
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
