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
    }

    public SymbolTable SymbolTable => symbolTable;

    // ============================================
    // Main Analysis Entry Point
    // ============================================

    public void Analyze(ProgramNode program)
    {
        // Phase 1: Collect all type and function declarations
        CollectDeclarations(program);

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

                case RecordDeclaration recordDecl:
                    CollectRecordDeclaration(recordDecl);
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

    private void CollectStructDeclaration(StructDeclaration structDecl)
    {
        if (!symbolTable.DeclareType(structDecl.Name, structDecl))
        {
            diagnostics.AddError(
                $"Type '{structDecl.Name}' is already defined",
                structDecl.Line, structDecl.Column);
        }
    }

    private void CollectRecordDeclaration(RecordDeclaration recordDecl)
    {
        if (!symbolTable.DeclareType(recordDecl.Name, recordDecl))
        {
            diagnostics.AddError(
                $"Type '{recordDecl.Name}' is already defined",
                recordDecl.Line, recordDecl.Column);
        }
    }

    private void CollectFunctionDeclaration(FunctionDeclaration funcDecl)
    {
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
        switch (stmt)
        {
            case VariableDeclaration varDecl:
                AnalyzeVariableDeclaration(varDecl);
                break;

            case Assignment assignment:
                AnalyzeAssignment(assignment);
                break;

            case FunctionDeclaration funcDecl:
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

            case StructDeclaration:
            case RecordDeclaration:
            case EnumDeclaration:
            case ImplBlock:
                // Already handled in earlier phases
                break;
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
        currentTypeName = typeName;

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
