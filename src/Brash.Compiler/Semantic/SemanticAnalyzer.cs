namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Diagnostics;

/// <summary>
/// Main semantic analyzer - orchestrates symbol resolution and type checking
/// </summary>
public class SemanticAnalyzer
{
    private readonly DiagnosticBag diagnostics;
    private readonly SymbolTable symbolTable;
    private readonly TypeChecker typeChecker;
    private readonly NullabilityChecker nullabilityChecker;
    private readonly SymbolResolver symbolResolver;
    private readonly MutabilityChecker mutabilityChecker;
    private readonly TranspileReadinessChecker transpileReadinessChecker;

    private TypeNode? currentFunctionReturnType;
    private string? currentTypeName; // For 'self' in methods
    private bool inLoop;

    public SemanticAnalyzer(DiagnosticBag diagnostics)
    {
        this.diagnostics = diagnostics;
        this.symbolTable = new SymbolTable();
        this.typeChecker = new TypeChecker(diagnostics, symbolTable);
        this.nullabilityChecker = new NullabilityChecker(diagnostics, typeChecker);
        this.symbolResolver = new SymbolResolver(diagnostics, symbolTable, typeChecker, nullabilityChecker);
        this.mutabilityChecker = new MutabilityChecker(diagnostics, symbolTable);
        this.transpileReadinessChecker = new TranspileReadinessChecker(diagnostics);
    }

    public SymbolTable SymbolTable => symbolTable;

    // ============================================
    // Main Analysis Entry Point
    // ============================================

    public void Analyze(ProgramNode program)
    {
        // Phase 1: Collect all type and function declarations
        CollectDeclarations(program);
        ValidateMainSignature(program);

        // Phase 2: Analyze implementations
        AnalyzeImplementations(program);

        // Phase 3: Analyze statements
        foreach (var stmt in program.Statements)
        {
            AnalyzeStatement(stmt);
        }
    }

    // ============================================
    // Phase 1: Declaration Collection
    // ============================================

    private void CollectDeclarations(ProgramNode program)
    {
        foreach (var stmt in program.Statements)
        {
            switch (stmt)
            {
                case StructDeclaration structDecl:
                    CollectStructDeclaration(structDecl);
                    break;

                case FunctionDeclaration funcDecl:
                    CollectFunctionDeclaration(funcDecl);
                    break;

                case EnumDeclaration enumDecl:
                    CollectEnumDeclaration(enumDecl);
                    break;
            }
        }
    }

    private void ValidateMainSignature(ProgramNode program)
    {
        var mains = program.Statements
            .OfType<FunctionDeclaration>()
            .Where(f => string.Equals(f.Name, "main", StringComparison.Ordinal))
            .ToList();

        if (mains.Count == 0)
            return;

        // Duplicate name diagnostics are already handled in declaration collection.
        var main = mains[0];
        if (main.Parameters.Count != 0
            && (main.Parameters.Count != 1 || !IsStringArray(main.Parameters[0].Type)))
        {
            diagnostics.AddError(
                "Function 'main' must have signature 'fn main()' or 'fn main(args: string[])'",
                main.Line,
                main.Column);
        }

        if (!IsValidMainReturnType(main.ReturnType))
        {
            diagnostics.AddError(
                "Function 'main' may only return 'int' or 'void'",
                main.Line,
                main.Column);
        }
    }

    private static bool IsStringArray(TypeNode type)
    {
        return type is ArrayType
        {
            ElementType: PrimitiveType { PrimitiveKind: PrimitiveType.Kind.String }
        };
    }

    private static bool IsValidMainReturnType(TypeNode? returnType)
    {
        if (returnType == null)
            return true; // implicit void

        if (returnType is PrimitiveType { PrimitiveKind: PrimitiveType.Kind.Void })
            return true;

        return returnType is PrimitiveType { PrimitiveKind: PrimitiveType.Kind.Int };
    }

    private void CollectStructDeclaration(StructDeclaration structDecl)
    {
        if (!symbolTable.DeclareType(structDecl.Name, structDecl))
        {
            diagnostics.AddError(
                $"Type '{structDecl.Name}' is already defined",
                structDecl.Line, structDecl.Column);
        }
    }

    private void CollectFunctionDeclaration(FunctionDeclaration funcDecl)
    {
        if (symbolTable.IsBuiltinFunction(funcDecl.Name))
        {
            diagnostics.AddError(
                $"Function '{funcDecl.Name}' is reserved as a builtin and cannot be redefined",
                funcDecl.Line, funcDecl.Column);
            return;
        }

        if (!symbolTable.DeclareFunction(funcDecl.Name, funcDecl))
        {
            diagnostics.AddError(
                $"Function '{funcDecl.Name}' is already defined",
                funcDecl.Line, funcDecl.Column);
        }
    }

    private void CollectEnumDeclaration(EnumDeclaration enumDecl)
    {
        var seen = new HashSet<string>();
        foreach (var variant in enumDecl.Variants)
        {
            if (!seen.Add(variant.Name))
            {
                diagnostics.AddError(
                    $"Enum '{enumDecl.Name}' contains duplicate variant '{variant.Name}'",
                    variant.Line, variant.Column);
            }
        }

        if (!symbolTable.DeclareType(enumDecl.Name, enumDecl))
        {
            diagnostics.AddError(
                $"Type '{enumDecl.Name}' is already defined",
                enumDecl.Line, enumDecl.Column);
        }
    }

    // ============================================
    // Phase 2: Implementation Analysis
    // ============================================

    private void AnalyzeImplementations(ProgramNode program)
    {
        foreach (var stmt in program.Statements)
        {
            if (stmt is ImplBlock implBlock)
            {
                AnalyzeImplBlock(implBlock);
            }
        }
    }

    private void AnalyzeImplBlock(ImplBlock implBlock)
    {
        // Check that the type exists
        if (!symbolTable.TypeExists(implBlock.TypeName))
        {
            diagnostics.AddError(
                $"Cannot implement methods for undefined type '{implBlock.TypeName}'",
                implBlock.Line, implBlock.Column);
            return;
        }

        // Collect methods
        foreach (var method in implBlock.Methods)
        {
            if (!symbolTable.DeclareMethod(implBlock.TypeName, method))
            {
                diagnostics.AddError(
                    $"Method '{method.Name}' is already defined for type '{implBlock.TypeName}'",
                    implBlock.Line, implBlock.Column);
            }

            // Analyze method body
            AnalyzeMethodDeclaration(method, implBlock.TypeName);
        }
    }

    // ============================================
    // Phase 3: Statement Analysis
    // ============================================

    private void AnalyzeStatement(Statement stmt)
    {
        transpileReadinessChecker.ValidateStatement(stmt);

        switch (stmt)
        {
            case VariableDeclaration varDecl:
                ValidateVariableVisibility(varDecl);
                AnalyzeVariableDeclaration(varDecl);
                break;

            case TupleVariableDeclaration tupleDecl:
                AnalyzeTupleVariableDeclaration(tupleDecl);
                break;

            case Assignment assignment:
                AnalyzeAssignment(assignment);
                break;

            case FunctionDeclaration funcDecl:
                ValidateTopLevelVisibility(funcDecl.IsPublic, "function", funcDecl.Name, funcDecl.Line, funcDecl.Column);
                AnalyzeFunctionDeclaration(funcDecl);
                break;

            case IfStatement ifStmt:
                AnalyzeIfStatement(ifStmt);
                break;

            case ForLoop forLoop:
                AnalyzeForLoop(forLoop);
                break;

            case WhileLoop whileLoop:
                AnalyzeWhileLoop(whileLoop);
                break;

            case TryStatement tryStmt:
                AnalyzeTryStatement(tryStmt);
                break;

            case ThrowStatement throwStmt:
                AnalyzeThrowStatement(throwStmt);
                break;

            case ReturnStatement returnStmt:
                AnalyzeReturnStatement(returnStmt);
                break;

            case BreakStatement:
            case ContinueStatement:
                if (!inLoop)
                {
                    diagnostics.AddError(
                        $"{stmt.GetType().Name} outside of loop",
                        stmt.Line, stmt.Column);
                }
                break;

            case ImportStatement importStmt:
                AnalyzeImportStatement(importStmt);
                break;

            case ExpressionStatement exprStmt:
                symbolResolver.ResolveExpressionType(exprStmt.Expression);
                break;

            case StructDeclaration structDecl:
                ValidateTopLevelVisibility(structDecl.IsPublic, "struct", structDecl.Name, structDecl.Line, structDecl.Column);
                break;

            case EnumDeclaration enumDecl:
                ValidateTopLevelVisibility(enumDecl.IsPublic, "enum", enumDecl.Name, enumDecl.Line, enumDecl.Column);
                break;

            case ImplBlock:
                // Already handled in earlier phases
                break;
        }
    }

    private void ValidateVariableVisibility(VariableDeclaration varDecl)
    {
        if (!varDecl.IsPublic)
            return;

        ValidateTopLevelVisibility(true, "variable", varDecl.Name, varDecl.Line, varDecl.Column);
        if (varDecl.Kind != VariableDeclaration.VarKind.Const)
        {
            diagnostics.AddError(
                $"Only const declarations can be public. Change '{varDecl.Name}' to 'pub const'.",
                varDecl.Line,
                varDecl.Column);
        }
    }

    private void ValidateTopLevelVisibility(bool isPublic, string kind, string name, int line, int column)
    {
        if (!isPublic)
            return;

        if (symbolTable.CurrentScopeLevel > 1)
        {
            diagnostics.AddError(
                $"Public {kind} '{name}' must be declared at module top level",
                line,
                column);
        }
    }

    private void AnalyzeVariableDeclaration(VariableDeclaration varDecl)
    {
        // Resolve the value type
        var valueType = symbolResolver.ResolveExpressionType(varDecl.Value);

        // Determine final type
        TypeNode finalType;
        if (varDecl.Type != null)
        {
            // Type is explicitly specified
            finalType = varDecl.Type;

            // Validate compatibility
            typeChecker.ValidateAssignment(varDecl.Type, valueType, varDecl.Line, varDecl.Column);
        }
        else
        {
            // Infer type from value
            finalType = valueType;
        }

        // Declare the variable
        bool isMutable = varDecl.Kind == VariableDeclaration.VarKind.Mut;
        if (!symbolTable.DeclareVariable(varDecl.Name, finalType, isMutable))
        {
            diagnostics.AddError(
                $"Variable '{varDecl.Name}' is already declared in this scope",
                varDecl.Line, varDecl.Column);
        }
    }

    private void AnalyzeTupleVariableDeclaration(TupleVariableDeclaration tupleDecl)
    {
        var valueType = symbolResolver.ResolveExpressionType(tupleDecl.Value);

        if (valueType is not TupleType tupleType)
        {
            diagnostics.AddError(
                $"Tuple destructuring requires a tuple value, got '{valueType}'",
                tupleDecl.Line,
                tupleDecl.Column);
            return;
        }

        if (tupleType.ElementTypes.Count != tupleDecl.Elements.Count)
        {
            diagnostics.AddError(
                $"Tuple destructuring arity mismatch: expected {tupleDecl.Elements.Count} values, got {tupleType.ElementTypes.Count}",
                tupleDecl.Line,
                tupleDecl.Column);
            return;
        }

        for (var i = 0; i < tupleDecl.Elements.Count; i++)
        {
            var element = tupleDecl.Elements[i];
            var elementType = tupleType.ElementTypes[i];
            if (!symbolTable.DeclareVariable(element.Name, elementType, element.IsMutable))
            {
                diagnostics.AddError(
                    $"Variable '{element.Name}' is already declared in this scope",
                    element.Line,
                    element.Column);
            }
        }
    }

    private void AnalyzeAssignment(Assignment assignment)
    {
        if (!mutabilityChecker.ValidateAssignmentTarget(assignment.Target, assignment.Line, assignment.Column))
            return;

        var targetType = symbolResolver.ResolveExpressionType(assignment.Target);
        var valueType = symbolResolver.ResolveExpressionType(assignment.Value);

        typeChecker.ValidateAssignment(targetType, valueType, assignment.Line, assignment.Column);
    }

    private void AnalyzeFunctionDeclaration(FunctionDeclaration funcDecl)
    {
        symbolTable.EnterScope();

        // Declare parameters
        foreach (var param in funcDecl.Parameters)
        {
            if (!symbolTable.DeclareVariable(param.Name, param.Type, param.IsMutable))
            {
                diagnostics.AddError(
                    $"Parameter '{param.Name}' is already declared",
                    funcDecl.Line, funcDecl.Column);
            }
        }

        // Set return type context
        var previousReturnType = currentFunctionReturnType;
        currentFunctionReturnType = funcDecl.ReturnType ?? new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };

        // Analyze body
        foreach (var stmt in funcDecl.Body)
        {
            AnalyzeStatement(stmt);
        }

        currentFunctionReturnType = previousReturnType;
        symbolTable.ExitScope();
    }

    private void AnalyzeMethodDeclaration(MethodDeclaration method, string typeName)
    {
        symbolTable.EnterScope();

        // Set type context for 'self'
        var previousTypeName = currentTypeName;
        currentTypeName = method.IsStatic ? null : typeName;
        symbolResolver.SetCurrentTypeContext(method.IsStatic ? null : typeName);

        // Declare parameters
        foreach (var param in method.Parameters)
        {
            if (!symbolTable.DeclareVariable(param.Name, param.Type, param.IsMutable))
            {
                diagnostics.AddError(
                    $"Parameter '{param.Name}' is already declared",
                    method.Line, method.Column);
            }
        }

        // Set return type context
        var previousReturnType = currentFunctionReturnType;
        currentFunctionReturnType = method.ReturnType ?? new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };

        // Analyze body
        foreach (var stmt in method.Body)
        {
            AnalyzeStatement(stmt);
        }

        currentFunctionReturnType = previousReturnType;
        currentTypeName = previousTypeName;
        symbolResolver.SetCurrentTypeContext(previousTypeName);
        symbolTable.ExitScope();
    }

    private void AnalyzeIfStatement(IfStatement ifStmt)
    {
        // Analyze condition
        var conditionType = symbolResolver.ResolveExpressionType(ifStmt.Condition);
        typeChecker.ValidateCondition(conditionType, ifStmt.Line, ifStmt.Column);

        // Analyze then block
        symbolTable.EnterScope();
        foreach (var stmt in ifStmt.ThenBlock)
            AnalyzeStatement(stmt);
        symbolTable.ExitScope();

        // Analyze elif clauses
        foreach (var elif in ifStmt.ElifClauses)
        {
            var elifCondType = symbolResolver.ResolveExpressionType(elif.Condition);
            typeChecker.ValidateCondition(elifCondType, ifStmt.Line, ifStmt.Column);

            symbolTable.EnterScope();
            foreach (var stmt in elif.Block)
                AnalyzeStatement(stmt);
            symbolTable.ExitScope();
        }

        // Analyze else block
        if (ifStmt.ElseBlock != null)
        {
            symbolTable.EnterScope();
            foreach (var stmt in ifStmt.ElseBlock)
                AnalyzeStatement(stmt);
            symbolTable.ExitScope();
        }
    }

    private void AnalyzeForLoop(ForLoop forLoop)
    {
        symbolTable.EnterScope();

        // Declare loop variable
        var loopVarType = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int };
        if (!symbolTable.DeclareVariable(forLoop.Variable, loopVarType, false))
        {
            diagnostics.AddError(
                $"Loop variable '{forLoop.Variable}' conflicts with existing declaration",
                forLoop.Line, forLoop.Column);
        }

        // Analyze range
        symbolResolver.ResolveExpressionType(forLoop.Range);

        // Analyze step if present
        if (forLoop.Step != null)
        {
            var stepType = symbolResolver.ResolveExpressionType(forLoop.Step);
            if (!typeChecker.IsNumericType(stepType))
            {
                diagnostics.AddError(
                    $"For loop step must be numeric, got '{stepType}'",
                    forLoop.Line, forLoop.Column);
            }
        }

        // Analyze body
        var wasInLoop = inLoop;
        inLoop = true;

        foreach (var stmt in forLoop.Body)
            AnalyzeStatement(stmt);

        inLoop = wasInLoop;
        symbolTable.ExitScope();
    }

    private void AnalyzeWhileLoop(WhileLoop whileLoop)
    {
        // Analyze condition
        var conditionType = symbolResolver.ResolveExpressionType(whileLoop.Condition);
        typeChecker.ValidateCondition(conditionType, whileLoop.Line, whileLoop.Column);

        symbolTable.EnterScope();

        var wasInLoop = inLoop;
        inLoop = true;

        foreach (var stmt in whileLoop.Body)
            AnalyzeStatement(stmt);

        inLoop = wasInLoop;
        symbolTable.ExitScope();
    }

    private void AnalyzeTryStatement(TryStatement tryStmt)
    {
        // Analyze try block
        symbolTable.EnterScope();
        foreach (var stmt in tryStmt.TryBlock)
            AnalyzeStatement(stmt);
        symbolTable.ExitScope();

        // Analyze catch block
        symbolTable.EnterScope();

        // Declare error variable (always of type Error)
        var errorType = new NamedType { Name = "Error" };
        if (!symbolTable.DeclareVariable(tryStmt.ErrorVariable, errorType, false))
        {
            diagnostics.AddError(
                $"Error variable '{tryStmt.ErrorVariable}' conflicts with existing declaration",
                tryStmt.Line, tryStmt.Column);
        }

        foreach (var stmt in tryStmt.CatchBlock)
            AnalyzeStatement(stmt);

        symbolTable.ExitScope();
    }

    private void AnalyzeThrowStatement(ThrowStatement throwStmt)
    {
        symbolResolver.ResolveExpressionType(throwStmt.Value);
    }

    private void AnalyzeReturnStatement(ReturnStatement returnStmt)
    {
        if (currentFunctionReturnType == null)
        {
            diagnostics.AddError(
                "Return statement outside of function",
                returnStmt.Line, returnStmt.Column);
            return;
        }

        if (returnStmt.Value == null)
        {
            // No return value
            var voidType = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };
            if (!typeChecker.AreTypesCompatible(currentFunctionReturnType, voidType))
            {
                diagnostics.AddError(
                    $"Function must return a value of type '{currentFunctionReturnType}'",
                    returnStmt.Line, returnStmt.Column);
            }
        }
        else
        {
            // Has return value
            var returnType = symbolResolver.ResolveExpressionType(returnStmt.Value);
            typeChecker.ValidateReturnType(currentFunctionReturnType, returnType,
                returnStmt.Line, returnStmt.Column);
        }
    }

    private void AnalyzeImportStatement(ImportStatement importStmt)
    {
        // For now, just validate syntax
        // In a full implementation, we'd resolve the imported module and add its symbols
        if (importStmt.FromModule != null && importStmt.ImportedItems.Count == 0)
        {
            diagnostics.AddWarning(
                $"Import from '{importStmt.FromModule}' has no imported items",
                importStmt.Line, importStmt.Column);
        }
    }
}
