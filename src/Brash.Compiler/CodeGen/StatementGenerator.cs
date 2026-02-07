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
                GenerateExpressionStatement(exprStmt.Expression);
                break;

            case StructDeclaration structDecl:
                EmitComment($"Struct '{structDecl.Name}' declaration");
                break;

            case EnumDeclaration enumDecl:
                GenerateEnumDeclaration(enumDecl);
                break;

            case ImplBlock implBlock:
                GenerateImplBlock(implBlock);
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
        if (varDecl.Value is StructLiteral structLiteral)
        {
            if (varDecl.Kind == VariableDeclaration.VarKind.Const)
            {
                EmitComment($"Const struct binding '{varDecl.Name}' is treated as mutable fields for now.");
                ReportUnsupported("const struct immutability in codegen");
                EmitLine();
            }

            GenerateStructBinding(varDecl.Name, structLiteral);
            return;
        }

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
        if (assignment.Target is MemberAccessExpression memberTarget)
        {
            GenerateMemberAssignment(memberTarget, assignment.Value);
            return;
        }

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
            _ => HandleUnsupportedAssignmentTarget(target)
        };
    }

    private void GenerateMemberAssignment(MemberAccessExpression member, Expression valueExpression)
    {
        var value = GenerateExpression(valueExpression);

        if (TryGetMemberPath(member, out var path))
        {
            Emit($"{path}={value}");
            return;
        }

        var objectHandle = ResolveObjectHandleForAssignment(member.Object);
        if (objectHandle == null)
        {
            HandleUnsupportedAssignmentTarget(member);
            return;
        }

        Emit($"brash_set_field {objectHandle} \"{member.MemberName}\" {value}");
    }

    private string? HandleUnsupportedAssignmentTarget(Expression target)
    {
        EmitComment($"Unsupported assignment target '{target.GetType().Name}'.");
        ReportUnsupported($"assignment target '{target.GetType().Name}'");
        return null;
    }

    private string GenerateCondition(Expression condition)
    {
        if (condition is BinaryExpression bin && IsConditionOperator(bin.Operator))
        {
            return GenerateBinaryExpression(bin);
        }

        // Treat as boolean test
        var expr = GenerateExpression(condition);
        return $"[ {expr} -ne 0 ]";
    }

    private bool IsConditionOperator(string op)
    {
        return op is "==" or "!=" or "<" or ">" or "<=" or ">=" or "&&" or "||";
    }

    private void GenerateStructBinding(string variableName, StructLiteral literal)
    {
        // Object handle stores its own path prefix.
        Emit($"{variableName}=\"{variableName}\"");
        EmitLine();
        Emit($"{variableName}__type=\"{literal.TypeName}\"");

        foreach (var (field, value) in literal.Fields)
        {
            EmitLine();
            GenerateStructFieldAssignment($"{variableName}_{field}", value);
        }
    }

    private void GenerateStructFieldAssignment(string fieldPath, Expression value)
    {
        if (value is StructLiteral nestedStruct)
        {
            Emit($"{fieldPath}=\"{fieldPath}\"");
            EmitLine();
            Emit($"{fieldPath}__type=\"{nestedStruct.TypeName}\"");

            foreach (var (nestedField, nestedValue) in nestedStruct.Fields)
            {
                EmitLine();
                GenerateStructFieldAssignment($"{fieldPath}_{nestedField}", nestedValue);
            }

            return;
        }

        Emit($"{fieldPath}={GenerateExpression(value)}");
    }

    private void GenerateExpressionStatement(Expression expression)
    {
        switch (expression)
        {
            case FunctionCallExpression call:
                Emit(GenerateFunctionCallStatement(call));
                return;

            case MethodCallExpression methodCall:
                Emit(GenerateMethodCallStatement(methodCall));
                return;

            case CommandExpression commandExpression:
                Emit(GenerateCommandStatement(commandExpression));
                return;

            case PipeExpression pipeExpression:
                Emit(GeneratePipeStatement(pipeExpression));
                return;
        }

        Emit(GenerateExpression(expression));
    }

    private string GenerateFunctionCallStatement(FunctionCallExpression call)
    {
        var args = string.Join(" ", call.Arguments.Select(GenerateExpression));
        if (call.FunctionName == "print")
            return $"printf '%s\\n' {args}";

        return args.Length > 0 ? $"{call.FunctionName} {args}" : call.FunctionName;
    }

    private string GenerateMethodCallStatement(MethodCallExpression call)
    {
        var raw = GenerateRawMethodCall(call);
        if (raw == null)
            return UnsupportedExpression(call);
        return raw;
    }

    private string GenerateCommandStatement(CommandExpression expr)
    {
        if (expr.IsAsync)
            return UnsupportedExpression(expr);

        return expr.Kind switch
        {
            CommandKind.Exec => GenerateExecStatement(expr),
            CommandKind.Spawn => GenerateSpawnStatement(expr),
            CommandKind.Cmd => ":",
            _ => UnsupportedExpression(expr)
        };
    }

    private string GeneratePipeStatement(PipeExpression expr)
    {
        // Pipe expressions are lazy command values. Expression statements don't execute them.
        return ":";
    }

    private string GenerateExecStatement(CommandExpression expr)
    {
        if (expr.Arguments.Count == 1)
            return $"brash_exec_cmd \"{GenerateExpression(expr.Arguments[0])}\"";

        return $"brash_exec_cmd \"{GenerateExpression(new CommandExpression { Kind = CommandKind.Cmd, Arguments = expr.Arguments })}\"";
    }

    private string GenerateSpawnStatement(CommandExpression expr)
    {
        if (expr.Arguments.Count == 1)
            return $"brash_spawn_cmd \"{GenerateExpression(expr.Arguments[0])}\" >/dev/null";

        return $"brash_spawn_cmd \"{GenerateExpression(new CommandExpression { Kind = CommandKind.Cmd, Arguments = expr.Arguments })}\" >/dev/null";
    }

    private void GenerateEnumDeclaration(EnumDeclaration enumDecl)
    {
        foreach (var variant in enumDecl.Variants)
        {
            Emit($"{enumDecl.Name}_{variant.Name}=\"{variant.Name}\"");
            EmitLine();
        }

        if (enumDecl.Variants.Count > 0)
            output.Length -= Environment.NewLine.Length;
    }

    private void GenerateImplBlock(ImplBlock implBlock)
    {
        foreach (var method in implBlock.Methods)
        {
            GenerateMethodDeclaration(implBlock.TypeName, method);
            EmitLine();
        }
    }

    private void GenerateMethodDeclaration(string typeName, MethodDeclaration method)
    {
        Emit($"{typeName}__{method.Name}() {{");
        indentLevel++;

        EmitLine("local __self=\"$1\"");
        EmitLine("shift");

        for (int i = 0; i < method.Parameters.Count; i++)
        {
            var param = method.Parameters[i];
            EmitLine($"local {param.Name}=\"${{{i + 1}}}\"");
        }

        if (method.Parameters.Count > 0)
            EmitLine();

        foreach (var stmt in method.Body)
        {
            GenerateStatement(stmt);
            EmitLine();
        }

        indentLevel--;
        Emit("}");
    }

    private string? ResolveObjectHandleForAssignment(Expression objectExpression)
    {
        if (objectExpression is IdentifierExpression identifier)
            return $"\"${{{identifier.Name}}}\"";

        if (objectExpression is SelfExpression)
            return "\"${__self}\"";

        if (objectExpression is MemberAccessExpression member && TryGetMemberPath(member, out var path))
            return $"\"${{{path}}}\"";

        return null;
    }
}
