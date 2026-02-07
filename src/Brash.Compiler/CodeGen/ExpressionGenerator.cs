namespace Brash.Compiler.CodeGen;

using System.Globalization;
using System.Text;
using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Expressions;

public partial class BashGenerator
{
    private string GenerateExpression(Expression expr)
    {
        return expr switch
        {
            LiteralExpression lit => GenerateLiteral(lit),
            IdentifierExpression ident => $"${{{ident.Name}}}",
            SelfExpression => "\"${__self}\"",
            BinaryExpression bin => GenerateBinaryExpression(bin),
            UnaryExpression unary => GenerateUnaryExpression(unary),
            CastExpression cast => GenerateCastExpression(cast),
            FunctionCallExpression call => GenerateFunctionCall(call),
            MethodCallExpression methodCall => GenerateMethodCall(methodCall),
            MemberAccessExpression member => GenerateMemberAccess(member),
            SafeNavigationExpression safeNav => GenerateSafeNavigation(safeNav),
            IndexAccessExpression index => GenerateIndexAccess(index),
            ArrayLiteral array => GenerateArrayLiteral(array),
            MapLiteral => HandleUnsupportedExpression(expr, "map literal"),
            TupleExpression tuple => GenerateTupleExpression(tuple),
            PipeExpression pipe => GeneratePipeExpression(pipe),
            NullCoalesceExpression nullCoalesce => GenerateNullCoalesce(nullCoalesce),
            CommandExpression command => GenerateCommandExpression(command),
            AwaitExpression awaitExpr => GenerateAwaitExpression(awaitExpr),
            RangeExpression => HandleUnsupportedExpression(expr, "range as value"),
            NullLiteral => "\"\"",
            _ => UnsupportedExpression(expr)
        };
    }

    private string GenerateLiteral(LiteralExpression lit)
    {
        if (lit.Type is PrimitiveType prim)
        {
            return prim.PrimitiveKind switch
            {
                PrimitiveType.Kind.String => lit.IsInterpolated
                    ? GenerateInterpolatedStringLiteral(lit.Value.ToString() ?? "")
                    : $"\"{EscapeString(lit.Value.ToString() ?? "")}\"",
                PrimitiveType.Kind.Int => lit.Value.ToString() ?? "0",
                PrimitiveType.Kind.Float => Convert.ToString(lit.Value, CultureInfo.InvariantCulture) ?? "0.0",
                PrimitiveType.Kind.Bool => lit.Value.ToString()?.ToLowerInvariant() == "true" ? "1" : "0",
                PrimitiveType.Kind.Char => $"'{lit.Value}'",
                _ => "\"\""
            };
        }

        return "\"\"";
    }

    private static string GenerateInterpolatedStringLiteral(string template)
    {
        var builder = new StringBuilder();
        builder.Append('"');

        int cursor = 0;
        while (cursor < template.Length)
        {
            var openBrace = FindNextUnescaped(template, '{', cursor);
            if (openBrace < 0)
            {
                builder.Append(EscapeForDoubleQuotes(template[cursor..]));
                break;
            }

            builder.Append(EscapeForDoubleQuotes(template[cursor..openBrace]));

            var closeBrace = FindNextUnescaped(template, '}', openBrace + 1);
            if (closeBrace < 0)
            {
                builder.Append(EscapeForDoubleQuotes(template[openBrace..]));
                break;
            }

            var placeholder = template[(openBrace + 1)..closeBrace].Trim();
            if (TryGetPlaceholderExpansion(placeholder, out var expansion))
            {
                builder.Append(expansion);
            }
            else
            {
                builder.Append(EscapeForDoubleQuotes(template[openBrace..(closeBrace + 1)]));
            }

            cursor = closeBrace + 1;
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static int FindNextUnescaped(string text, char needle, int start)
    {
        for (int i = start; i < text.Length; i++)
        {
            if (text[i] != needle)
                continue;

            if (i == 0 || text[i - 1] != '\\')
                return i;
        }

        return -1;
    }

    private static string EscapeForDoubleQuotes(string input)
    {
        return input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal);
    }

    private static bool TryGetPlaceholderExpansion(string placeholder, out string expansion)
    {
        if (TryGetIdentifierPath(placeholder, out var path))
        {
            expansion = "${" + path + "}";
            return true;
        }

        if (string.Equals(placeholder, "self", StringComparison.Ordinal))
        {
            expansion = "${__self}";
            return true;
        }

        expansion = string.Empty;
        return false;
    }

    private static bool TryGetIdentifierPath(string input, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var parts = input.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        foreach (var part in parts)
        {
            if (!IsIdentifier(part))
                return false;
        }

        path = string.Join("_", parts);
        return true;
    }

    private static bool IsIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        if (!IsIdentifierStart(value[0]))
            return false;

        for (int i = 1; i < value.Length; i++)
        {
            if (!IsIdentifierPart(value[i]))
                return false;
        }

        return true;
    }

    private static bool IsIdentifierStart(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
    }

    private static bool IsIdentifierPart(char c)
    {
        return IsIdentifierStart(c) || (c >= '0' && c <= '9');
    }

    private string GenerateBinaryExpression(BinaryExpression bin)
    {
        var left = GenerateExpression(bin.Left);
        var right = GenerateExpression(bin.Right);

        return bin.Operator switch
        {
            "+" => GenerateAddition(bin.Left, bin.Right, left, right),
            "-" => $"$(({left} - {right}))",
            "*" => $"$(({left} * {right}))",
            "/" => $"$(({left} / {right}))",
            "%" => $"$(({left} % {right}))",
            "==" => $"$(if [[ {left} == {right} ]]; then echo 1; else echo 0; fi)",
            "!=" => $"$(if [[ {left} != {right} ]]; then echo 1; else echo 0; fi)",
            "<" => $"$(if (( {left} < {right} )); then echo 1; else echo 0; fi)",
            ">" => $"$(if (( {left} > {right} )); then echo 1; else echo 0; fi)",
            "<=" => $"$(if (( {left} <= {right} )); then echo 1; else echo 0; fi)",
            ">=" => $"$(if (( {left} >= {right} )); then echo 1; else echo 0; fi)",
            "&&" => $"$(( ({left} != 0) && ({right} != 0) ))",
            "||" => $"$(( ({left} != 0) || ({right} != 0) ))",
            _ => $"{left} {bin.Operator} {right}"
        };
    }

    private string GenerateUnaryExpression(UnaryExpression unary)
    {
        var operand = GenerateExpression(unary.Operand);
        return unary.Operator switch
        {
            "-" => $"$((-{operand}))",
            "+" => $"$((+{operand}))",
            "!" => $"$((!{operand}))",
            _ => operand
        };
    }

    private string GenerateCastExpression(CastExpression cast)
    {
        var value = GenerateExpression(cast.Value);

        if (cast.TargetType is PrimitiveType prim)
        {
            return prim.PrimitiveKind switch
            {
                PrimitiveType.Kind.String => $"$(printf '%s' {value})",
                PrimitiveType.Kind.Int => $"$(( {value} ))",
                PrimitiveType.Kind.Float => $"$(awk \"BEGIN {{ print ({value}) + 0 }}\")",
                PrimitiveType.Kind.Bool => $"$(( ({value}) != 0 ))",
                PrimitiveType.Kind.Char => $"$(printf '%s' {value} | head -c 1)",
                _ => value
            };
        }

        return value;
    }

    private string GenerateFunctionCall(FunctionCallExpression call)
    {
        // Handle built-in functions
        if (call.FunctionName == "print")
        {
            var printArgs = string.Join(" ", call.Arguments.Select(GenerateSinglePrintArg));
            return $"printf '%s\\n' {printArgs}";
        }

        var args = string.Join(" ", call.Arguments.Select(GenerateFunctionCallArg));
        return args.Length > 0 ? $"$({call.FunctionName} {args})" : $"$({call.FunctionName})";
    }

    private string GenerateSinglePrintArg(Expression expression)
    {
        var rendered = GenerateExpression(expression);
        if (IsAlreadyQuoted(rendered))
            return rendered;
        return $"\"{rendered}\"";
    }

    private string GenerateFunctionCallArg(Expression expression)
    {
        var rendered = GenerateExpression(expression);
        if (IsAlreadyQuoted(rendered))
            return rendered;
        return $"\"{rendered}\"";
    }

    private string GenerateMethodCall(MethodCallExpression call)
    {
        if (call.IsStaticDispatch && !string.IsNullOrWhiteSpace(call.StaticTypeName))
        {
            var staticArgs = string.Join(" ", call.Arguments.Select(GenerateFunctionCallArg));
            return staticArgs.Length > 0
                ? $"$({call.StaticTypeName}__{call.MethodName} {staticArgs})"
                : $"$({call.StaticTypeName}__{call.MethodName})";
        }

        if (TryGenerateStringBuiltinMethodCall(call, out var stringBuiltin))
            return stringBuiltin;

        if (call.MethodName == "to_string")
        {
            if (call.Arguments.Count != 0)
            {
                return HandleUnsupportedExpression(call, "to_string() with arguments");
            }

            return $"$(printf '%s' {GenerateExpression(call.Object)})";
        }

        var receiverHandle = GenerateObjectHandle(call.Object);
        if (receiverHandle == null)
            return HandleUnsupportedExpression(call, $"method receiver '{call.Object.GetType().Name}'");

        var args = string.Join(" ", call.Arguments.Select(GenerateExpression));
        if (string.IsNullOrWhiteSpace(args))
            return $"$(brash_call_method {receiverHandle} \"{call.MethodName}\")";

        return $"$(brash_call_method {receiverHandle} \"{call.MethodName}\" {args})";
    }

    private bool TryGenerateStringBuiltinMethodCall(MethodCallExpression call, out string generated)
    {
        generated = string.Empty;
        if (!StringType.TryGetBuiltinMethod(call.MethodName, out var builtin))
            return false;

        if (call.Arguments.Count != builtin.ParameterKinds.Length)
            return false;

        var receiver = GenerateFunctionCallArg(call.Object);
        var args = call.Arguments.Select(GenerateFunctionCallArg).ToList();
        generated = builtin.EmitBash(receiver, args);
        return true;
    }

    private string GenerateMemberAccess(MemberAccessExpression member)
    {
        if (TryGetMemberPath(member, out var path))
            return $"${{{path}}}";

        var receiverHandle = GenerateObjectHandle(member.Object);
        if (receiverHandle == null)
            return HandleUnsupportedExpression(member, $"member access receiver '{member.Object.GetType().Name}'");

        return $"$(brash_get_field {receiverHandle} \"{member.MemberName}\")";
    }

    private string GenerateArrayLiteral(ArrayLiteral array)
    {
        var elements = string.Join(" ", array.Elements.Select(GenerateExpression));
        return $"({elements})";
    }

    private string GenerateNullCoalesce(NullCoalesceExpression expr)
    {
        var right = GenerateExpression(expr.Right);

        return expr.Left switch
        {
            _ when TryGetMemberPath(expr.Left, out var path) => $"${{{path}:-{right}}}",
            _ => HandleUnsupportedNullCoalesceLeft(expr.Left, right)
        };
    }

    private string GeneratePipeExpression(PipeExpression expr)
    {
        var left = GenerateCommandValue(expr.Left);
        var right = GenerateCommandValue(expr.Right);
        if (left != null && right != null)
            return $"$(brash_pipe_cmd \"{left}\" \"{right}\")";

        return GenerateValuePipeExpression(expr);
    }

    private string GenerateCommandExpression(CommandExpression expr)
    {
        if (expr.IsAsync)
        {
            return expr.Kind switch
            {
                CommandKind.Exec => GenerateAsyncExecValue(expr),
                CommandKind.Spawn => GenerateAsyncSpawnValue(expr),
                _ => HandleUnsupportedExpression(expr, $"async {expr.Kind.ToString().ToLowerInvariant()}(...)")
            };
        }

        return expr.Kind switch
        {
            CommandKind.Cmd => GenerateCmdValue(expr),
            CommandKind.Exec => GenerateExecValue(expr),
            CommandKind.Spawn => GenerateSpawnValue(expr),
            _ => HandleUnsupportedExpression(expr, $"command kind '{expr.Kind}'")
        };
    }

    private string GenerateAwaitExpression(AwaitExpression expr)
    {
        return $"$(brash_await \"{GenerateExpression(expr.Expression)}\")";
    }

    private string GenerateTupleExpression(TupleExpression tuple)
    {
        if (tuple.Elements.Count == 0)
            return "\"\"";

        var args = tuple.Elements.Select(GenerateExpression).ToList();
        var format = string.Join("\\t", Enumerable.Repeat("%s", args.Count));
        return $"$(printf '{format}' {string.Join(" ", args)})";
    }

    private string GenerateIndexAccess(IndexAccessExpression index)
    {
        if (index.Array is IdentifierExpression ident)
        {
            var idx = GenerateExpression(index.Index);
            return $"${{{ident.Name}[{idx}]}}";
        }

        return HandleUnsupportedExpression(index, "index access receiver");
    }

    private string GenerateSafeNavigation(SafeNavigationExpression safeNav)
    {
        if (TryGetMemberPath(safeNav.Object, out var path))
            return $"${{{path}_{safeNav.MemberName}:-}}";

        var receiverHandle = GenerateObjectHandle(safeNav.Object);
        if (receiverHandle == null)
            return HandleUnsupportedExpression(safeNav, $"safe navigation receiver '{safeNav.Object.GetType().Name}'");

        return $"$(brash_get_field {receiverHandle} \"{safeNav.MemberName}\")";
    }

    private string GenerateAddition(Expression leftExpr, Expression rightExpr, string left, string right)
    {
        if (IsStringLike(leftExpr) || IsStringLike(rightExpr))
        {
            var leftArg = GenerateStringOperand(leftExpr, left);
            var rightArg = GenerateStringOperand(rightExpr, right);
            return $"$(printf '%s%s' {leftArg} {rightArg})";
        }

        return $"$(( {left} + {right} ))";
    }

    private string GenerateStringOperand(Expression expression, string renderedExpression)
    {
        return expression switch
        {
            IdentifierExpression ident => $"\"${{{ident.Name}}}\"",
            LiteralExpression lit when lit.Type is PrimitiveType { PrimitiveKind: PrimitiveType.Kind.String } => renderedExpression,
            LiteralExpression lit when lit.Type is PrimitiveType { PrimitiveKind: PrimitiveType.Kind.Char } => renderedExpression,
            CastExpression { TargetType: PrimitiveType { PrimitiveKind: PrimitiveType.Kind.String } } => $"\"{GenerateExpression(expression)}\"",
            _ => $"\"{renderedExpression}\""
        };
    }

    private static bool IsStringLike(Expression expr)
    {
        return expr switch
        {
            LiteralExpression lit when lit.Type is PrimitiveType { PrimitiveKind: PrimitiveType.Kind.String } => true,
            LiteralExpression lit when lit.Type is PrimitiveType { PrimitiveKind: PrimitiveType.Kind.Char } => true,
            CastExpression { TargetType: PrimitiveType { PrimitiveKind: PrimitiveType.Kind.String } } => true,
            BinaryExpression bin when bin.Operator == "+" &&
                                      (IsStringLike(bin.Left) || IsStringLike(bin.Right)) => true,
            _ => false
        };
    }

    private string HandleUnsupportedNullCoalesceLeft(Expression left, string right)
    {
        ReportUnsupported($"null coalesce left operand '{left.GetType().Name}'");
        return right;
    }

    private string HandleUnsupportedExpression(Expression expr, string feature)
    {
        ReportUnsupported(feature);
        return UnsupportedExpression(expr);
    }

    private string? GenerateObjectHandle(Expression expr)
    {
        if (expr is IdentifierExpression ident)
            return $"\"${{{ident.Name}}}\"";

        if (expr is SelfExpression)
            return "\"${__self}\"";

        if (expr is MemberAccessExpression member && TryGetMemberPath(member, out var path))
            return $"\"${{{path}}}\"";

        return null;
    }

    private string? GeneratePipableCommand(Expression expr)
    {
        return expr switch
        {
            CommandExpression cmd when cmd.Kind is CommandKind.Cmd or CommandKind.Exec or CommandKind.Spawn => GenerateExpression(cmd),
            PipeExpression pipe => GenerateExpression(pipe),
            FunctionCallExpression call => $"$({call.FunctionName} {string.Join(" ", call.Arguments.Select(GenerateExpression))})",
            MethodCallExpression methodCall => $"$({GenerateRawMethodCall(methodCall)})",
            _ => null
        };
    }

    private string? GenerateRawMethodCall(MethodCallExpression call)
    {
        if (call.IsStaticDispatch && !string.IsNullOrWhiteSpace(call.StaticTypeName))
        {
            var staticArgs = string.Join(" ", call.Arguments.Select(GenerateFunctionCallArg));
            return string.IsNullOrWhiteSpace(staticArgs)
                ? $"{call.StaticTypeName}__{call.MethodName}"
                : $"{call.StaticTypeName}__{call.MethodName} {staticArgs}";
        }

        var receiverHandle = GenerateObjectHandle(call.Object);
        if (receiverHandle == null)
            return null;

        var args = string.Join(" ", call.Arguments.Select(GenerateExpression));
        return string.IsNullOrWhiteSpace(args)
            ? $"brash_call_method {receiverHandle} \"{call.MethodName}\""
            : $"brash_call_method {receiverHandle} \"{call.MethodName}\" {args}";
    }

    private string? GenerateCommandValue(Expression expr)
    {
        return expr switch
        {
            CommandExpression cmd when cmd.Kind is CommandKind.Cmd or CommandKind.Exec or CommandKind.Spawn => GenerateExpression(cmd),
            PipeExpression pipe => GenerateExpression(pipe),
            _ => null
        };
    }

    private string GenerateCmdValue(CommandExpression expr)
    {
        if (expr.Arguments.Count == 1 && expr.Arguments[0] is CommandExpression or PipeExpression)
            return GenerateExpression(expr.Arguments[0]);

        if (expr.Arguments.Count == 1)
            return GenerateCommandTextExpression(expr.Arguments[0]);

        if (expr.Arguments.Count == 0)
            return "\"\"";

        var args = string.Join(" ", expr.Arguments.Select(GenerateCommandBuilderArg));
        return $"$(brash_build_cmd {args})";
    }

    private string GenerateCommandBuilderArg(Expression expression)
    {
        // Force each evaluated expression to become a single shell argument
        // before brash_build_cmd applies shell-quoting.
        var rendered = GenerateExpression(expression);
        if (IsAlreadyQuoted(rendered))
            return rendered;
        return $"\"{rendered}\"";
    }

    private static bool IsAlreadyQuoted(string rendered)
    {
        if (rendered.Length < 2)
            return false;

        return (rendered[0] == '"' && rendered[^1] == '"')
               || (rendered[0] == '\'' && rendered[^1] == '\'');
    }

    private string GenerateExecValue(CommandExpression expr)
    {
        if (expr.Arguments.Count == 1)
        {
            var cmdValue = GenerateExpression(expr.Arguments[0]);
            return $"$(brash_exec_cmd \"{cmdValue}\")";
        }

        var cmd = GenerateCmdValue(new CommandExpression
        {
            Line = expr.Line,
            Column = expr.Column,
            Kind = CommandKind.Cmd,
            Arguments = expr.Arguments
        });
        return $"$(brash_exec_cmd \"{cmd}\")";
    }

    private string GenerateSpawnValue(CommandExpression expr)
    {
        if (expr.Arguments.Count == 1)
        {
            var cmdValue = GenerateExpression(expr.Arguments[0]);
            return $"$(brash_spawn_cmd \"{cmdValue}\")";
        }

        var cmd = GenerateCmdValue(new CommandExpression
        {
            Line = expr.Line,
            Column = expr.Column,
            Kind = CommandKind.Cmd,
            Arguments = expr.Arguments
        });
        return $"$(brash_spawn_cmd \"{cmd}\")";
    }

    private string GenerateAsyncExecValue(CommandExpression expr)
    {
        if (expr.Arguments.Count == 1)
        {
            var cmdValue = GenerateExpression(expr.Arguments[0]);
            return $"$(brash_async_exec_cmd \"{cmdValue}\")";
        }

        var cmd = GenerateCmdValue(new CommandExpression
        {
            Line = expr.Line,
            Column = expr.Column,
            Kind = CommandKind.Cmd,
            Arguments = expr.Arguments
        });
        return $"$(brash_async_exec_cmd \"{cmd}\")";
    }

    private string GenerateAsyncSpawnValue(CommandExpression expr)
    {
        if (expr.Arguments.Count == 1)
        {
            var cmdValue = GenerateExpression(expr.Arguments[0]);
            return $"$(brash_async_spawn_cmd \"{cmdValue}\")";
        }

        var cmd = GenerateCmdValue(new CommandExpression
        {
            Line = expr.Line,
            Column = expr.Column,
            Kind = CommandKind.Cmd,
            Arguments = expr.Arguments
        });
        return $"$(brash_async_spawn_cmd \"{cmd}\")";
    }

    private string GenerateCommandTextExpression(Expression expression)
    {
        // For string literals, preserve spaces in a single command text.
        if (expression is LiteralExpression
            {
                Type: PrimitiveType { PrimitiveKind: PrimitiveType.Kind.String },
                Value: string commandText
            })
        {
            return EscapeString(commandText);
        }

        return GenerateExpression(expression);
    }

    private bool TryGetMemberPath(Expression expr, out string path)
    {
        if (expr is IdentifierExpression ident)
        {
            path = ident.Name;
            return true;
        }

        if (expr is MemberAccessExpression member && TryGetMemberPath(member.Object, out var basePath))
        {
            path = $"{basePath}_{member.MemberName}";
            return true;
        }

        path = string.Empty;
        return false;
    }

    private string GenerateValuePipeExpression(PipeExpression expr)
    {
        var input = GenerateExpression(expr.Left);
        return expr.Right switch
        {
            FunctionCallExpression call => GenerateFunctionPipeInvocation(input, call),
            _ => HandleUnsupportedExpression(expr, "value pipe right stage")
        };
    }

    private string GenerateFunctionPipeInvocation(string pipedInput, FunctionCallExpression call)
    {
        // Pipe semantics: pass the left value as the implicit first argument.
        var args = new List<string> { QuoteRenderedArg(pipedInput) };
        args.AddRange(call.Arguments.Select(GenerateFunctionCallArg));
        return $"$({call.FunctionName} {string.Join(" ", args)})";
    }

    private static string QuoteRenderedArg(string rendered)
    {
        if (IsAlreadyQuoted(rendered))
            return rendered;
        return $"\"{rendered}\"";
    }
}
