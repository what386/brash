namespace Brash.Compiler.Semantic;

using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;

public class SemanticAnalyzer
{
    private readonly DiagnosticBag diagnostics;
    private readonly SymbolTable symbolTable;
    private TypeNode? currentFunctionReturnType;
    private bool inLoop;

    public SemanticAnalyzer(DiagnosticBag diagnostics)
    {
        this.diagnostics = diagnostics;
        this.symbolTable = new SymbolTable();
    }

    public void Analyze(ProgramNode program)
    {
        // First pass: collect all struct/record/function declarations
        foreach (var stmt in program.Statements)
        {
            if (stmt is StructDeclaration structDecl)
            {
                DeclareType(structDecl.Name, structDecl);
            }
            else if (stmt is RecordDeclaration recordDecl)
            {
                DeclareType(recordDecl.Name, recordDecl);
            }
            else if (stmt is FunctionDeclaration funcDecl)
            {
                DeclareFunction(funcDecl);
            }
        }

        // Second pass: analyze statements
        foreach (var stmt in program.Statements)
        {
            AnalyzeStatement(stmt);
        }
    }

    private void DeclareType(string name, Statement declaration)
    {
        if (symbolTable.TypeExists(name))
        {
            diagnostics.AddError($"Type '{name}' is already defined",
                declaration.Line, declaration.Column);
            return;
        }

        symbolTable.DeclareType(name, declaration);
    }

    private void DeclareFunction(FunctionDeclaration func)
    {
        if (symbolTable.FunctionExists(func.Name))
        {
            diagnostics.AddError($"Function '{func.Name}' is already defined",
                func.Line, func.Column);
            return;
        }

        symbolTable.DeclareFunction(func.Name, func);
    }

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

            case ReturnStatement returnStmt:
                AnalyzeReturnStatement(returnStmt);
                break;

            case BreakStatement:
            case ContinueStatement:
                if (!inLoop)
                {
                    diagnostics.AddError($"{stmt.GetType().Name} outside of loop",
                        stmt.Line, stmt.Column);
                }
                break;

            case ExpressionStatement exprStmt:
                AnalyzeExpression(exprStmt.Expression);
                break;
        }
    }

    private void AnalyzeVariableDeclaration(VariableDeclaration varDecl)
    {
        // Check if variable already exists in current scope
        if (symbolTable.VariableExistsInCurrentScope(varDecl.Name))
        {
            diagnostics.AddError($"Variable '{varDecl.Name}' is already declared in this scope",
                varDecl.Line, varDecl.Column);
            return;
        }

        // Analyze the initializer expression
        var valueType = AnalyzeExpression(varDecl.Value);

        // If type is specified, check compatibility
        if (varDecl.Type != null)
        {
            if (!TypesCompatible(varDecl.Type, valueType))
            {
                diagnostics.AddError(
                    $"Cannot assign value of type '{valueType}' to variable of type '{varDecl.Type}'",
                    varDecl.Line, varDecl.Column);
            }
        }

        // Declare the variable
        symbolTable.DeclareVariable(varDecl.Name, varDecl.Type ?? valueType, varDecl.Kind == VariableDeclaration.VarKind.Mut);
    }

    private void AnalyzeAssignment(Assignment assignment)
    {
        // Check if target is valid
        if (assignment.Target is IdentifierExpression ident)
        {
            var symbol = symbolTable.LookupVariable(ident.Name);
            if (symbol == null)
            {
                diagnostics.AddError($"Variable '{ident.Name}' is not declared",
                    assignment.Line, assignment.Column);
                return;
            }

            if (!symbol.IsMutable)
            {
                diagnostics.AddError($"Cannot assign to immutable variable '{ident.Name}'",
                    assignment.Line, assignment.Column);
                return;
            }

            var valueType = AnalyzeExpression(assignment.Value);
            if (!TypesCompatible(symbol.Type, valueType))
            {
                diagnostics.AddError(
                    $"Cannot assign value of type '{valueType}' to variable of type '{symbol.Type}'",
                    assignment.Line, assignment.Column);
            }
        }
        else
        {
            // Member access or index access
            AnalyzeExpression(assignment.Target);
            AnalyzeExpression(assignment.Value);
        }
    }

    private void AnalyzeFunctionDeclaration(FunctionDeclaration func)
    {
        symbolTable.EnterScope();

        // Declare parameters
        foreach (var param in func.Parameters)
        {
            symbolTable.DeclareVariable(param.Name, param.Type, false);
        }

        // Store current function return type for return statement checking
        var previousReturnType = currentFunctionReturnType;
        currentFunctionReturnType = func.ReturnType;

        // Analyze body
        foreach (var stmt in func.Body)
        {
            AnalyzeStatement(stmt);
        }

        currentFunctionReturnType = previousReturnType;
        symbolTable.ExitScope();
    }

    private void AnalyzeIfStatement(IfStatement ifStmt)
    {
        // Analyze condition
        var conditionType = AnalyzeExpression(ifStmt.Condition);
        if (conditionType is not PrimitiveType { PrimitiveKind: PrimitiveType.Kind.Bool })
        {
            diagnostics.AddWarning($"Condition should be boolean, got '{conditionType}'",
                ifStmt.Line, ifStmt.Column);
        }

        // Analyze blocks
        symbolTable.EnterScope();
        foreach (var stmt in ifStmt.ThenBlock)
            AnalyzeStatement(stmt);
        symbolTable.ExitScope();

        foreach (var elif in ifStmt.ElifClauses)
        {
            AnalyzeExpression(elif.Condition);
            symbolTable.EnterScope();
            foreach (var stmt in elif.Block)
                AnalyzeStatement(stmt);
            symbolTable.ExitScope();
        }

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
        symbolTable.DeclareVariable(forLoop.Variable,
            new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int }, false);

        AnalyzeExpression(forLoop.Range);
        if (forLoop.Step != null)
            AnalyzeExpression(forLoop.Step);

        var wasInLoop = inLoop;
        inLoop = true;

        foreach (var stmt in forLoop.Body)
            AnalyzeStatement(stmt);

        inLoop = wasInLoop;
        symbolTable.ExitScope();
    }

    private void AnalyzeWhileLoop(WhileLoop whileLoop)
    {
        AnalyzeExpression(whileLoop.Condition);

        symbolTable.EnterScope();

        var wasInLoop = inLoop;
        inLoop = true;

        foreach (var stmt in whileLoop.Body)
            AnalyzeStatement(stmt);

        inLoop = wasInLoop;
        symbolTable.ExitScope();
    }

    private void AnalyzeReturnStatement(ReturnStatement returnStmt)
    {
        if (currentFunctionReturnType == null)
        {
            diagnostics.AddError("Return statement outside of function",
                returnStmt.Line, returnStmt.Column);
            return;
        }

        if (returnStmt.Value == null)
        {
            if (currentFunctionReturnType is not PrimitiveType { PrimitiveKind: PrimitiveType.Kind.Void })
            {
                diagnostics.AddError($"Function must return a value of type '{currentFunctionReturnType}'",
                    returnStmt.Line, returnStmt.Column);
            }
        }
        else
        {
            var returnType = AnalyzeExpression(returnStmt.Value);
            if (!TypesCompatible(currentFunctionReturnType, returnType))
            {
                diagnostics.AddError(
                    $"Cannot return value of type '{returnType}' from function expecting '{currentFunctionReturnType}'",
                    returnStmt.Line, returnStmt.Column);
            }
        }
    }

    private TypeNode AnalyzeExpression(Expression expr)
    {
        return expr switch
        {
            LiteralExpression lit => lit.Type,
            IdentifierExpression ident => AnalyzeIdentifier(ident),
            BinaryExpression bin => AnalyzeBinaryExpression(bin),
            FunctionCallExpression call => AnalyzeFunctionCall(call),
            NullLiteral => new NullableType { BaseType = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void } },
            _ => new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void }
        };
    }

    private TypeNode AnalyzeIdentifier(IdentifierExpression ident)
    {
        var symbol = symbolTable.LookupVariable(ident.Name);
        if (symbol == null)
        {
            diagnostics.AddError($"Undefined variable '{ident.Name}'",
                ident.Line, ident.Column);
            return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };
        }

        return symbol.Type;
    }

    private TypeNode AnalyzeBinaryExpression(BinaryExpression bin)
    {
        var leftType = AnalyzeExpression(bin.Left);
        var rightType = AnalyzeExpression(bin.Right);

        // Simple type checking for arithmetic operators
        if (bin.Operator is "+" or "-" or "*" or "/" or "%")
        {
            if (leftType is PrimitiveType leftPrim && rightType is PrimitiveType rightPrim)
            {
                if (leftPrim.PrimitiveKind == PrimitiveType.Kind.Int && rightPrim.PrimitiveKind == PrimitiveType.Kind.Int)
                    return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int };

                if ((leftPrim.PrimitiveKind == PrimitiveType.Kind.Float || rightPrim.PrimitiveKind == PrimitiveType.Kind.Float))
                    return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Float };
            }
        }

        // Comparison operators return bool
        if (bin.Operator is "==" or "!=" or "<" or ">" or "<=" or ">=")
        {
            return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Bool };
        }

        // Logical operators
        if (bin.Operator is "&&" or "||")
        {
            return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Bool };
        }

        return leftType;
    }

    private TypeNode AnalyzeFunctionCall(FunctionCallExpression call)
    {
        var func = symbolTable.LookupFunction(call.FunctionName);
        if (func == null)
        {
            diagnostics.AddError($"Undefined function '{call.FunctionName}'",
                call.Line, call.Column);
            return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };
        }

        // Check argument count
        if (call.Arguments.Count != func.Parameters.Count)
        {
            diagnostics.AddError(
                $"Function '{call.FunctionName}' expects {func.Parameters.Count} arguments, got {call.Arguments.Count}",
                call.Line, call.Column);
        }

        // Analyze arguments
        foreach (var arg in call.Arguments)
        {
            AnalyzeExpression(arg);
        }

        return func.ReturnType ?? new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };
    }

    private bool TypesCompatible(TypeNode expected, TypeNode actual)
    {
        // Simplified type compatibility check
        // TODO: Implement full type compatibility (nullability, subtyping, etc.)

        if (expected.GetType() != actual.GetType())
            return false;

        if (expected is PrimitiveType expPrim && actual is PrimitiveType actPrim)
            return expPrim.PrimitiveKind == actPrim.PrimitiveKind;

        return expected.ToString() == actual.ToString();
    }
}
