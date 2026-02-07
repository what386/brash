namespace Brash.Compiler.CodeGen;

using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;
using Brash.Compiler.Ast.Statements;

public partial class BashGenerator
{
    private void GenerateStatement(Statement stmt)
    {
        currentContext = stmt.GetType().Name;

        switch (stmt)
        {
            case VariableDeclaration varDecl:
                GenerateVariableDeclaration(varDecl);
                break;

            case Assignment assignment:
                GenerateAssignment(assignment);
                break;

            case FunctionDeclaration funcDecl:
                GenerateFunctionDeclaration(funcDecl);
                break;

            case IfStatement ifStmt:
                GenerateIfStatement(ifStmt);
                break;

            case ForLoop forLoop:
                GenerateForLoop(forLoop);
                break;

            case WhileLoop whileLoop:
                GenerateWhileLoop(whileLoop);
                break;

            case ReturnStatement returnStmt:
                GenerateReturnStatement(returnStmt);
                break;

            case BreakStatement:
                Emit("break");
                break;

            case ContinueStatement:
                Emit("continue");
                break;

            case ExpressionStatement exprStmt:
                Emit(GenerateExpression(exprStmt.Expression));
                break;

            case StructDeclaration structDecl:
                EmitComment($"Struct '{structDecl.Name}' currently has no direct Bash output.");
                break;

            case RecordDeclaration recordDecl:
                EmitComment($"Record '{recordDecl.Name}' currently has no direct Bash output.");
                break;

            case EnumDeclaration enumDecl:
                EmitComment($"Enum '{enumDecl.Name}' currently has no direct Bash output.");
                break;

            case ImplBlock implBlock:
                EmitComment($"Impl block for '{implBlock.TypeName}' is not yet emitted to Bash.");
                break;

            case TryStatement:
                EmitComment("Try/catch is not yet emitted to Bash.");
                ReportUnsupported("try/catch statement");
                break;

            case ThrowStatement:
                EmitComment("Throw is not yet emitted to Bash.");
                ReportUnsupported("throw statement");
                break;

            case ImportStatement importStmt:
                EmitComment($"Import '{importStmt.Module ?? importStmt.FromModule ?? "<unknown>"}' is compile-time only.");
                break;

            default:
                EmitComment($"Unsupported statement '{stmt.GetType().Name}'.");
                ReportUnsupported($"statement '{stmt.GetType().Name}'");
                break;
        }
    }

    private void GenerateVariableDeclaration(VariableDeclaration varDecl)
    {
        var value = GenerateExpression(varDecl.Value);

        if (varDecl.Kind == VariableDeclaration.VarKind.Const)
        {
            Emit($"readonly {varDecl.Name}={value}");
        }
        else
        {
            // Both 'let' and 'mut' become regular bash variables
            Emit($"{varDecl.Name}={value}");
        }
    }

    private void GenerateAssignment(Assignment assignment)
    {
        var target = GenerateAssignmentTarget(assignment.Target);
        if (target == null)
            return;

        var value = GenerateExpression(assignment.Value);
        Emit($"{target}={value}");
    }

    private void GenerateFunctionDeclaration(FunctionDeclaration func)
    {
        Emit($"{func.Name}() {{");
        indentLevel++;

        // Generate parameter assignments
        for (int i = 0; i < func.Parameters.Count; i++)
        {
            var param = func.Parameters[i];
            EmitLine($"local {param.Name}=\"${{{i + 1}}}\"");
        }

        if (func.Parameters.Count > 0)
            EmitLine();

        // Generate body
        foreach (var stmt in func.Body)
        {
            GenerateStatement(stmt);
            EmitLine();
        }

        indentLevel--;
        Emit("}");
    }

    private void GenerateIfStatement(IfStatement ifStmt)
    {
        var condition = GenerateCondition(ifStmt.Condition);
        Emit($"if {condition}; then");
        indentLevel++;

        foreach (var stmt in ifStmt.ThenBlock)
        {
            EmitLine();
            GenerateStatement(stmt);
        }

        indentLevel--;

        foreach (var elif in ifStmt.ElifClauses)
        {
            EmitLine();
            var elifCondition = GenerateCondition(elif.Condition);
            Emit($"elif {elifCondition}; then");
            indentLevel++;

            foreach (var stmt in elif.Block)
            {
                EmitLine();
                GenerateStatement(stmt);
            }

            indentLevel--;
        }

        if (ifStmt.ElseBlock != null)
        {
            EmitLine();
            Emit("else");
            indentLevel++;

            foreach (var stmt in ifStmt.ElseBlock)
            {
                EmitLine();
                GenerateStatement(stmt);
            }

            indentLevel--;
        }

        EmitLine();
        Emit("fi");
    }

    private void GenerateForLoop(ForLoop forLoop)
    {
        // Generate range
        string rangeExpr;
        if (forLoop.Range is RangeExpression range)
        {
            var start = GenerateExpression(range.Start);
            var end = GenerateExpression(range.End);

            if (forLoop.Step != null)
            {
                var step = GenerateExpression(forLoop.Step);
                rangeExpr = $"$(seq {start} {step} {end})";
            }
            else
            {
                rangeExpr = $"$(seq {start} {end})";
            }
        }
        else
        {
            // Collection iteration
            rangeExpr = GenerateExpression(forLoop.Range);
        }

        Emit($"for {forLoop.Variable} in {rangeExpr}; do");
        indentLevel++;

        foreach (var stmt in forLoop.Body)
        {
            EmitLine();
            GenerateStatement(stmt);
        }

        indentLevel--;
        EmitLine();
        Emit("done");
    }

    private void GenerateWhileLoop(WhileLoop whileLoop)
    {
        var condition = GenerateCondition(whileLoop.Condition);
        Emit($"while {condition}; do");
        indentLevel++;

        foreach (var stmt in whileLoop.Body)
        {
            EmitLine();
            GenerateStatement(stmt);
        }

        indentLevel--;
        EmitLine();
        Emit("done");
    }

    private void GenerateReturnStatement(ReturnStatement returnStmt)
    {
        if (returnStmt.Value != null)
        {
            var value = GenerateExpression(returnStmt.Value);
            Emit($"echo {value}");
            EmitLine();
            Emit("return 0");
        }
        else
        {
            Emit("return 0");
        }
    }

    private string? GenerateAssignmentTarget(Expression target)
    {
        return target switch
        {
            IdentifierExpression ident => ident.Name,
            MemberAccessExpression member => GenerateMemberAssignmentTarget(member),
            _ => HandleUnsupportedAssignmentTarget(target)
        };
    }

    private string? GenerateMemberAssignmentTarget(MemberAccessExpression member)
    {
        if (member.Object is IdentifierExpression ident)
            return $"{ident.Name}_{member.MemberName}";

        return HandleUnsupportedAssignmentTarget(member);
    }

    private string? HandleUnsupportedAssignmentTarget(Expression target)
    {
        EmitComment($"Unsupported assignment target '{target.GetType().Name}'.");
        ReportUnsupported($"assignment target '{target.GetType().Name}'");
        return null;
    }

    private string GenerateCondition(Expression condition)
    {
        if (condition is BinaryExpression bin && IsComparisonOperator(bin.Operator))
        {
            return GenerateBinaryExpression(bin);
        }

        // Treat as boolean test
        var expr = GenerateExpression(condition);
        return $"[ {expr} -eq 0 ]";
    }

    private bool IsComparisonOperator(string op)
    {
        return op is "==" or "!=" or "<" or ">" or "<=" or ">=";
    }
}
