namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;

public class VariableSymbol
{
    public string Name { get; set; } = string.Empty;
    public TypeNode Type { get; set; } = null!;
    public bool IsMutable { get; set; }
}

public class SymbolTable
{
    private readonly Stack<Dictionary<string, VariableSymbol>> scopes = new();
    private readonly Dictionary<string, FunctionDeclaration> functions = new();
    private readonly Dictionary<string, Statement> types = new();

    public SymbolTable()
    {
        // Start with global scope
        EnterScope();
    }

    // ============================================
    // Scope Management
    // ============================================

    public void EnterScope()
    {
        scopes.Push(new Dictionary<string, VariableSymbol>());
    }

    public void ExitScope()
    {
        if (scopes.Count > 1)
            scopes.Pop();
    }

    // ============================================
    // Variables
    // ============================================

    public void DeclareVariable(string name, TypeNode type, bool isMutable)
    {
        var currentScope = scopes.Peek();
        currentScope[name] = new VariableSymbol
        {
            Name = name,
            Type = type,
            IsMutable = isMutable
        };
    }

    public VariableSymbol? LookupVariable(string name)
    {
        // Search from innermost to outermost scope
        foreach (var scope in scopes)
        {
            if (scope.TryGetValue(name, out var symbol))
                return symbol;
        }
        return null;
    }

    public bool VariableExistsInCurrentScope(string name)
    {
        return scopes.Peek().ContainsKey(name);
    }

    // ============================================
    // Functions
    // ============================================

    public void DeclareFunction(string name, FunctionDeclaration func)
    {
        functions[name] = func;
    }

    public FunctionDeclaration? LookupFunction(string name)
    {
        return functions.GetValueOrDefault(name);
    }

    public bool FunctionExists(string name)
    {
        return functions.ContainsKey(name);
    }

    // ============================================
    // Types (Structs/Records)
    // ============================================

    public void DeclareType(string name, Statement typeDecl)
    {
        types[name] = typeDecl;
    }

    public Statement? LookupType(string name)
    {
        return types.GetValueOrDefault(name);
    }

    public bool TypeExists(string name)
    {
        return types.ContainsKey(name);
    }
}
