namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;

// ============================================
// Symbol Types
// ============================================

public class VariableSymbol
{
    public string Name { get; set; } = string.Empty;
    public TypeNode Type { get; set; } = null!;
    public bool IsMutable { get; set; }
    public int ScopeLevel { get; set; }
}

public class FunctionSymbol
{
    public string Name { get; set; } = string.Empty;
    public List<TypeNode> ParameterTypes { get; set; } = new();
    public List<string> ParameterNames { get; set; } = new();
    public TypeNode ReturnType { get; set; } = null!;
    public FunctionDeclaration Declaration { get; set; } = null!;
    public bool IsBuiltin { get; set; }
}

public class TypeSymbol
{
    public string Name { get; set; } = string.Empty;
    public Statement Declaration { get; set; } = null!; // StructDeclaration or EnumDeclaration
    public Dictionary<string, TypeNode> Fields { get; set; } = new();
    public HashSet<string> EnumVariants { get; set; } = new();
    public bool IsEnum { get; set; }
}

public class MethodSymbol
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty; // The type this method belongs to
    public bool IsStatic { get; set; }
    public List<TypeNode> ParameterTypes { get; set; } = new();
    public List<string> ParameterNames { get; set; } = new();
    public TypeNode ReturnType { get; set; } = null!;
    public MethodDeclaration Declaration { get; set; } = null!;
}

// ============================================
// Symbol Table
// ============================================

public class SymbolTable
{
    private readonly Stack<Dictionary<string, VariableSymbol>> scopes = new();
    private readonly Dictionary<string, FunctionSymbol> functions = new();
    private readonly Dictionary<string, TypeSymbol> types = new();
    private readonly Dictionary<string, List<MethodSymbol>> methods = new(); // Key: TypeName
    private int currentScopeLevel = 0;

    public SymbolTable()
    {
        // Start with global scope
        EnterScope();
        RegisterBuiltins();
    }

    // ============================================
    // Scope Management
    // ============================================

    public void EnterScope()
    {
        scopes.Push(new Dictionary<string, VariableSymbol>());
        currentScopeLevel++;
    }

    public void ExitScope()
    {
        if (scopes.Count > 1)
        {
            scopes.Pop();
            currentScopeLevel--;
        }
    }

    public int CurrentScopeLevel => currentScopeLevel;

    // ============================================
    // Variables
    // ============================================

    public bool DeclareVariable(string name, TypeNode type, bool isMutable)
    {
        var currentScope = scopes.Peek();

        if (currentScope.ContainsKey(name))
            return false; // Already declared in current scope

        currentScope[name] = new VariableSymbol
        {
            Name = name,
            Type = type,
            IsMutable = isMutable,
            ScopeLevel = currentScopeLevel
        };

        return true;
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

    public bool VariableExists(string name)
    {
        return LookupVariable(name) != null;
    }

    // ============================================
    // Functions
    // ============================================

    public bool DeclareFunction(string name, FunctionDeclaration func)
    {
        if (functions.ContainsKey(name))
            return false;

        var paramTypes = func.Parameters.Select(p => p.Type).ToList();
        var paramNames = func.Parameters.Select(p => p.Name).ToList();

        functions[name] = new FunctionSymbol
        {
            Name = name,
            ParameterTypes = paramTypes,
            ParameterNames = paramNames,
            ReturnType = func.ReturnType ?? new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void },
            Declaration = func
        };

        return true;
    }

    public bool IsBuiltinFunction(string name)
    {
        return functions.TryGetValue(name, out var symbol) && symbol.IsBuiltin;
    }

    public FunctionSymbol? LookupFunction(string name)
    {
        return functions.GetValueOrDefault(name);
    }

    public bool FunctionExists(string name)
    {
        return functions.ContainsKey(name);
    }

    public IEnumerable<FunctionSymbol> GetAllFunctions()
    {
        return functions.Values;
    }

    // ============================================
    // Types (Structs/Enums)
    // ============================================

    public bool DeclareType(string name, Statement declaration)
    {
        if (types.ContainsKey(name))
            return false;

        var fields = new Dictionary<string, TypeNode>();
        var enumVariants = new HashSet<string>();
        bool isEnum = false;
        if (declaration is StructDeclaration structDecl)
        {
            foreach (var field in structDecl.Fields)
                fields[field.Name] = field.Type;
        }
        else if (declaration is EnumDeclaration enumDecl)
        {
            foreach (var variant in enumDecl.Variants)
                enumVariants.Add(variant.Name);
            isEnum = true;
        }

        types[name] = new TypeSymbol
        {
            Name = name,
            Declaration = declaration,
            Fields = fields,
            EnumVariants = enumVariants,
            IsEnum = isEnum
        };

        return true;
    }

    public TypeSymbol? LookupType(string name)
    {
        return types.GetValueOrDefault(name);
    }

    public bool TypeExists(string name)
    {
        return types.ContainsKey(name);
    }

    public IEnumerable<TypeSymbol> GetAllTypes()
    {
        return types.Values;
    }

    // ============================================
    // Methods
    // ============================================

    public bool DeclareMethod(string typeName, MethodDeclaration method)
    {
        if (!methods.ContainsKey(typeName))
            methods[typeName] = new List<MethodSymbol>();

        // Check if method already exists for this type
        if (methods[typeName].Any(m => m.Name == method.Name))
            return false;

        var paramTypes = method.Parameters.Select(p => p.Type).ToList();
        var paramNames = method.Parameters.Select(p => p.Name).ToList();

        methods[typeName].Add(new MethodSymbol
        {
            Name = method.Name,
            TypeName = typeName,
            IsStatic = method.IsStatic,
            ParameterTypes = paramTypes,
            ParameterNames = paramNames,
            ReturnType = method.ReturnType ?? new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void },
            Declaration = method
        });

        return true;
    }

    public MethodSymbol? LookupMethod(string typeName, string methodName)
    {
        if (!methods.TryGetValue(typeName, out var typeMethods))
            return null;

        return typeMethods.FirstOrDefault(m => m.Name == methodName);
    }

    public List<MethodSymbol> GetMethodsForType(string typeName)
    {
        return methods.GetValueOrDefault(typeName) ?? new List<MethodSymbol>();
    }

    public bool MethodExists(string typeName, string methodName)
    {
        return LookupMethod(typeName, methodName) != null;
    }

    // ============================================
    // Utility
    // ============================================

    public void Clear()
    {
        scopes.Clear();
        functions.Clear();
        types.Clear();
        methods.Clear();
        currentScopeLevel = 0;
        EnterScope(); // Re-enter global scope
        RegisterBuiltins();
    }

    private void RegisterBuiltins()
    {
        functions["panic"] = new FunctionSymbol
        {
            Name = "panic",
            ParameterTypes = new List<TypeNode>
            {
                new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
            },
            ParameterNames = new List<string> { "message" },
            ReturnType = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void },
            IsBuiltin = true
        };

        functions["bash"] = new FunctionSymbol
        {
            Name = "bash",
            ParameterTypes = new List<TypeNode>
            {
                new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
            },
            ParameterNames = new List<string> { "script" },
            ReturnType = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void },
            IsBuiltin = true
        };
    }
}
