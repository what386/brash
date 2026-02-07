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

public class IfDefDirective : PreprocessorDirective
{
    public string Name { get; set; } = string.Empty;
    public List<Statement> ThenStatements { get; set; } = new();
    public List<Statement> ElseStatements { get; set; } = new();
}

public class IfNDefDirective : PreprocessorDirective
{
    public string Name { get; set; } = string.Empty;
    public List<Statement> ThenStatements { get; set; } = new();
    public List<Statement> ElseStatements { get; set; } = new();
}

// ============================================
// Base Statement and Expression
// ============================================

public abstract class Statement : AstNode { }

public abstract class Expression : AstNode { }

// ============================================
// Helper Classes
// ============================================

public class Parameter : AstNode
{
    public bool IsMutable { get; set; }
    public string Name { get; set; } = string.Empty;
    public TypeNode Type { get; set; } = null!;
    public Expression? DefaultValue { get; set; }
}

public class FieldDeclaration : AstNode
{
    public string Name { get; set; } = string.Empty;
    public TypeNode Type { get; set; } = null!;
}

public class ElifClause : AstNode
{
    public Expression Condition { get; set; } = null!;
    public List<Statement> Block { get; set; } = new();
}

public class EnumVariant : AstNode
{
    public string Name { get; set; } = string.Empty;
}
