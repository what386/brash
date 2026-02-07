namespace Brash.Compiler.Ast.Statements;

// ============================================
// Declaration Statements
// ============================================

public class VariableDeclaration : Statement
{
    public enum VarKind { Let, Mut, Const }
    public VarKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public TypeNode? Type { get; set; }
    public Expression Value { get; set; } = null!;
}

public class TupleBindingElement : AstNode
{
    public bool IsMutable { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class TupleVariableDeclaration : Statement
{
    public List<TupleBindingElement> Elements { get; set; } = new();
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
