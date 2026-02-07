namespace Brash.Compiler.Frontend;

using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Ast.Expressions;

public class AstBuilder : BrashBaseVisitor<AstNode>
{
    // ============================================
    // Program
    // ============================================

    public override AstNode VisitProgram(BrashParser.ProgramContext context)
    {
        var program = new ProgramNode
        {
            Line = context.Start.Line,
            Column = context.Start.Column
        };

        // Process preprocessor directives
        foreach (var directive in context.preprocessorDirective())
        {
            var dir = Visit(directive) as PreprocessorDirective;
            if (dir != null)
                program.Directives.Add(dir);
        }

        // Process statements
        foreach (var stmt in context.statement())
        {
            var statement = Visit(stmt) as Statement;
            if (statement != null)
                program.Statements.Add(statement);
        }

        return program;
    }

    // ============================================
    // Statements
    // ============================================

    public override AstNode VisitVariableDeclaration(BrashParser.VariableDeclarationContext context)
    {
        var kind = context.CONST() != null
            ? VariableDeclaration.VarKind.Const
            : context.MUT() != null
                ? VariableDeclaration.VarKind.Mut
                : VariableDeclaration.VarKind.Let;

        return new VariableDeclaration
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Kind = kind,
            Name = context.IDENTIFIER().GetText(),
            Type = context.type() != null ? Visit(context.type()) as TypeNode : null,
            Value = Visit(context.expression()) as Expression ?? new NullLiteral()
        };
    }

    public override AstNode VisitAssignment(BrashParser.AssignmentContext context)
    {
        Expression target;

        if (context.IDENTIFIER() != null)
        {
            target = new IdentifierExpression
            {
                Name = context.IDENTIFIER().GetText(),
                Line = context.Start.Line,
                Column = context.Start.Column
            };
        }
        else if (context.memberAccess() != null)
        {
            target = Visit(context.memberAccess()) as Expression ?? new NullLiteral();
        }
        else
        {
            target = Visit(context.indexAccess()) as Expression ?? new NullLiteral();
        }

        return new Assignment
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Target = target,
            Value = Visit(context.expression()) as Expression ?? new NullLiteral()
        };
    }

    public override AstNode VisitFunctionDeclaration(BrashParser.FunctionDeclarationContext context)
    {
        var func = new FunctionDeclaration
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            IsAsync = context.ASYNC() != null,
            Name = context.IDENTIFIER().GetText()
        };

        // Parameters
        if (context.parameterList() != null)
        {
            foreach (var param in context.parameterList().parameter())
            {
                func.Parameters.Add(new Parameter
                {
                    Line = param.Start.Line,
                    Column = param.Start.Column,
                    IsMutable = param.MUT() != null,
                    Name = param.IDENTIFIER().GetText(),
                    Type = Visit(param.type()) as TypeNode ?? new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void },
                    DefaultValue = param.expression() != null ? Visit(param.expression()) as Expression : null
                });
            }
        }

        // Return type
        if (context.returnType() != null)
        {
            if (context.returnType().VOID() != null)
            {
                func.ReturnType = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };
            }
            else if (context.returnType().type() != null)
            {
                func.ReturnType = Visit(context.returnType().type()) as TypeNode;
            }
            else if (context.returnType().tupleType() != null)
            {
                func.ReturnType = Visit(context.returnType().tupleType()) as TypeNode;
            }
        }

        // Body
        if (context.functionBody() != null)
        {
            foreach (var stmt in context.functionBody().statement())
            {
                var statement = Visit(stmt) as Statement;
                if (statement != null)
                    func.Body.Add(statement);
            }
        }

        return func;
    }

    public override AstNode VisitStructDeclaration(BrashParser.StructDeclarationContext context)
    {
        var structDecl = new StructDeclaration
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Name = context.IDENTIFIER().GetText()
        };

        if (context.structBody() != null)
        {
            foreach (var field in context.structBody().fieldDeclaration())
            {
                structDecl.Fields.Add(new FieldDeclaration
                {
                    Name = field.IDENTIFIER().GetText(),
                    Type = Visit(field.type()) as TypeNode ?? new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void }
                });
            }
        }

        return structDecl;
    }

    public override AstNode VisitEnumDeclaration(BrashParser.EnumDeclarationContext context)
    {
        var enumDecl = new EnumDeclaration
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Name = context.IDENTIFIER().GetText()
        };

        foreach (var variant in context.enumBody().enumVariant())
        {
            enumDecl.Variants.Add(new EnumVariant
            {
                Line = variant.Start.Line,
                Column = variant.Start.Column,
                Name = variant.IDENTIFIER().GetText()
            });
        }

        return enumDecl;
    }

    public override AstNode VisitIfStatement(BrashParser.IfStatementContext context)
    {
        var ifStmt = new IfStatement
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Condition = Visit(context.expression()) as Expression ?? new NullLiteral()
        };

        // Then block
        foreach (var stmt in context.statement())
        {
            var statement = Visit(stmt) as Statement;
            if (statement != null)
                ifStmt.ThenBlock.Add(statement);
        }

        // Elif clauses
        foreach (var elif in context.elifClause())
        {
            var elifClause = new ElifClause
            {
                Condition = Visit(elif.expression()) as Expression ?? new NullLiteral()
            };

            foreach (var stmt in elif.statement())
            {
                var statement = Visit(stmt) as Statement;
                if (statement != null)
                    elifClause.Block.Add(statement);
            }

            ifStmt.ElifClauses.Add(elifClause);
        }

        // Else clause
        if (context.elseClause() != null)
        {
            ifStmt.ElseBlock = new List<Statement>();
            foreach (var stmt in context.elseClause().statement())
            {
                var statement = Visit(stmt) as Statement;
                if (statement != null)
                    ifStmt.ElseBlock.Add(statement);
            }
        }

        return ifStmt;
    }

    public override AstNode VisitForLoop(BrashParser.ForLoopContext context)
    {
        return new ForLoop
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            IsIncrementing = context.GetChild(1).GetText() != "-",
            Variable = context.IDENTIFIER().GetText(),
            Range = Visit(context.expression(0)) as Expression ?? new NullLiteral(),
            Step = context.expression().Length > 1 ? Visit(context.expression(1)) as Expression : null,
            Body = context.statement().Select(s => Visit(s) as Statement).Where(s => s != null).ToList()!
        };
    }

    public override AstNode VisitWhileLoop(BrashParser.WhileLoopContext context)
    {
        return new WhileLoop
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Condition = Visit(context.expression()) as Expression ?? new NullLiteral(),
            Body = context.statement().Select(s => Visit(s) as Statement).Where(s => s != null).ToList()!
        };
    }

    public override AstNode VisitReturnStatement(BrashParser.ReturnStatementContext context)
    {
        return new ReturnStatement
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Value = context.expression() != null ? Visit(context.expression()) as Expression : null
        };
    }

    public override AstNode VisitBreakStatement(BrashParser.BreakStatementContext context)
    {
        return new BreakStatement { Line = context.Start.Line, Column = context.Start.Column };
    }

    public override AstNode VisitContinueStatement(BrashParser.ContinueStatementContext context)
    {
        return new ContinueStatement { Line = context.Start.Line, Column = context.Start.Column };
    }

    public override AstNode VisitExpressionStatement(BrashParser.ExpressionStatementContext context)
    {
        return new ExpressionStatement
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Expression = Visit(context.expression()) as Expression ?? new NullLiteral()
        };
    }

    public override AstNode VisitThrowStatement(BrashParser.ThrowStatementContext context)
    {
        return new ThrowStatement
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Value = Visit(context.expression()) as Expression ?? new NullLiteral()
        };
    }

    public override AstNode VisitTryStatement(BrashParser.TryStatementContext context)
    {
        var tryStmt = new TryStatement
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            ErrorVariable = context.IDENTIFIER().GetText()
        };

        // tryStatement: 'try' statement* 'catch' IDENTIFIER statement* 'end'
        // Split direct statement children at catch token.
        bool inCatch = false;
        foreach (var child in context.children)
        {
            if (child.GetText() == "catch")
            {
                inCatch = true;
                continue;
            }

            if (child is BrashParser.StatementContext stmtCtx)
            {
                var statement = Visit(stmtCtx) as Statement;
                if (statement == null)
                    continue;

                if (inCatch)
                    tryStmt.CatchBlock.Add(statement);
                else
                    tryStmt.TryBlock.Add(statement);
            }
        }

        return tryStmt;
    }

    public override AstNode VisitImportStatement(BrashParser.ImportStatementContext context)
    {
        var importStmt = new ImportStatement
        {
            Line = context.Start.Line,
            Column = context.Start.Column
        };

        if (context.stringLiteral() != null)
        {
            importStmt.Module = UnquoteStringLiteral(context.stringLiteral().GetText());
            return importStmt;
        }

        var spec = context.importSpecifier();
        if (spec == null)
            return importStmt;

        var module = spec.stringLiteral();
        if (module != null)
            importStmt.FromModule = UnquoteStringLiteral(module.GetText());

        var names = spec.IDENTIFIER();
        foreach (var name in names)
            importStmt.ImportedItems.Add(name.GetText());

        return importStmt;
    }

    public override AstNode VisitImplBlock(BrashParser.ImplBlockContext context)
    {
        var impl = new ImplBlock
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            TypeName = context.IDENTIFIER().GetText()
        };

        foreach (var methodCtx in context.methodDeclaration())
        {
            var method = Visit(methodCtx) as MethodDeclaration;
            if (method != null)
                impl.Methods.Add(method);
        }

        return impl;
    }

    public override AstNode VisitMethodDeclaration(BrashParser.MethodDeclarationContext context)
    {
        var method = new MethodDeclaration
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Name = context.IDENTIFIER().GetText()
        };

        if (context.parameterList() != null)
        {
            foreach (var param in context.parameterList().parameter())
            {
                method.Parameters.Add(new Parameter
                {
                    Line = param.Start.Line,
                    Column = param.Start.Column,
                    IsMutable = param.MUT() != null,
                    Name = param.IDENTIFIER().GetText(),
                    Type = Visit(param.type()) as TypeNode ?? new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void },
                    DefaultValue = param.expression() != null ? Visit(param.expression()) as Expression : null
                });
            }
        }

        if (context.returnType() != null)
        {
            if (context.returnType().VOID() != null)
            {
                method.ReturnType = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };
            }
            else if (context.returnType().type() != null)
            {
                method.ReturnType = Visit(context.returnType().type()) as TypeNode;
            }
            else if (context.returnType().tupleType() != null)
            {
                method.ReturnType = Visit(context.returnType().tupleType()) as TypeNode;
            }
        }

        if (context.functionBody() != null)
        {
            foreach (var stmt in context.functionBody().statement())
            {
                var statement = Visit(stmt) as Statement;
                if (statement != null)
                    method.Body.Add(statement);
            }
        }

        return method;
    }

    // ============================================
    // Expressions
    // ============================================

    public override AstNode VisitPrimaryExpr(BrashParser.PrimaryExprContext context)
    {
        return Visit(context.primaryExpression());
    }

    public override AstNode VisitPrimaryExpression(BrashParser.PrimaryExpressionContext context)
    {
        if (context.literal() != null)
            return Visit(context.literal());

        if (context.functionCall() != null)
            return Visit(context.functionCall());

        if (context.IDENTIFIER() != null)
            return new IdentifierExpression
            {
                Name = context.IDENTIFIER().GetText(),
                Line = context.Start.Line,
                Column = context.Start.Column
            };

        if (context.NULL() != null)
            return new NullLiteral { Line = context.Start.Line, Column = context.Start.Column };

        if (context.SELF() != null)
            return new SelfExpression { Line = context.Start.Line, Column = context.Start.Column };

        if (context.expression() != null)
            return Visit(context.expression());

        if (context.tupleExpression() != null)
            return Visit(context.tupleExpression());

        if (context.arrayLiteral() != null)
            return Visit(context.arrayLiteral());

        if (context.mapLiteral() != null)
            return Visit(context.mapLiteral());

        if (context.structLiteral() != null)
            return Visit(context.structLiteral());

        return new NullLiteral();
    }

    public override AstNode VisitFunctionCall(BrashParser.FunctionCallContext context)
    {
        var call = new FunctionCallExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            FunctionName = context.IDENTIFIER().GetText()
        };

        if (context.argumentList() != null)
        {
            foreach (var arg in context.argumentList().expression())
            {
                var expr = Visit(arg) as Expression;
                if (expr != null)
                    call.Arguments.Add(expr);
            }
        }

        return call;
    }

    public override AstNode VisitMethodCallExpr(BrashParser.MethodCallExprContext context)
    {
        var call = new MethodCallExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Object = Visit(context.expression()) as Expression ?? new NullLiteral(),
            MethodName = context.IDENTIFIER().GetText()
        };

        if (context.argumentList() != null)
        {
            foreach (var arg in context.argumentList().expression())
            {
                var expr = Visit(arg) as Expression;
                if (expr != null)
                    call.Arguments.Add(expr);
            }
        }

        return call;
    }

    public override AstNode VisitMemberAccessExpr(BrashParser.MemberAccessExprContext context)
    {
        return new MemberAccessExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Object = Visit(context.expression()) as Expression ?? new NullLiteral(),
            MemberName = context.IDENTIFIER().GetText()
        };
    }

    public override AstNode VisitIndexAccessExpr(BrashParser.IndexAccessExprContext context)
    {
        return new IndexAccessExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Array = Visit(context.expression(0)) as Expression ?? new NullLiteral(),
            Index = Visit(context.expression(1)) as Expression ?? new NullLiteral()
        };
    }

    public override AstNode VisitAwaitExpr(BrashParser.AwaitExprContext context)
    {
        return new AwaitExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Expression = Visit(context.expression()) as Expression ?? new NullLiteral()
        };
    }

    public override AstNode VisitPipeExpr(BrashParser.PipeExprContext context)
    {
        return new PipeExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Left = Visit(context.expression(0)) as Expression ?? new NullLiteral(),
            Right = Visit(context.expression(1)) as Expression ?? new NullLiteral()
        };
    }

    public override AstNode VisitUnaryExpr(BrashParser.UnaryExprContext context)
    {
        return new UnaryExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Operator = context.GetChild(0).GetText(),
            Operand = Visit(context.expression()) as Expression ?? new NullLiteral()
        };
    }

    public override AstNode VisitMultiplicativeExpr(BrashParser.MultiplicativeExprContext context)
    {
        return BuildBinaryExpression(context);
    }

    public override AstNode VisitAdditiveExpr(BrashParser.AdditiveExprContext context)
    {
        return BuildBinaryExpression(context);
    }

    public override AstNode VisitRangeExpr(BrashParser.RangeExprContext context)
    {
        return new RangeExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Start = Visit(context.expression(0)) as Expression ?? new NullLiteral(),
            End = Visit(context.expression(1)) as Expression ?? new NullLiteral()
        };
    }

    public override AstNode VisitComparisonExpr(BrashParser.ComparisonExprContext context)
    {
        return BuildBinaryExpression(context);
    }

    public override AstNode VisitLogicalExpr(BrashParser.LogicalExprContext context)
    {
        return BuildBinaryExpression(context);
    }

    public override AstNode VisitNullCoalesceExpr(BrashParser.NullCoalesceExprContext context)
    {
        return new NullCoalesceExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Left = Visit(context.expression(0)) as Expression ?? new NullLiteral(),
            Right = Visit(context.expression(1)) as Expression ?? new NullLiteral()
        };
    }

    public override AstNode VisitSafeNavigationExpr(BrashParser.SafeNavigationExprContext context)
    {
        return new SafeNavigationExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Object = Visit(context.expression()) as Expression ?? new NullLiteral(),
            MemberName = context.IDENTIFIER().GetText()
        };
    }

    public override AstNode VisitCommandExpr(BrashParser.CommandExprContext context)
    {
        return BuildCommandExpression(context, CommandKind.Cmd, isAsync: false);
    }

    public override AstNode VisitExecExpr(BrashParser.ExecExprContext context)
    {
        return BuildCommandExpression(context, CommandKind.Exec, isAsync: false);
    }

    public override AstNode VisitAsyncExecExpr(BrashParser.AsyncExecExprContext context)
    {
        return BuildCommandExpression(context, CommandKind.Exec, isAsync: true);
    }

    public override AstNode VisitAsyncSpawnExpr(BrashParser.AsyncSpawnExprContext context)
    {
        return BuildCommandExpression(context, CommandKind.Spawn, isAsync: true);
    }

    public override AstNode VisitSpawnExpr(BrashParser.SpawnExprContext context)
    {
        return BuildCommandExpression(context, CommandKind.Spawn, isAsync: false);
    }

    public override AstNode VisitTupleExpression(BrashParser.TupleExpressionContext context)
    {
        var tuple = new TupleExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column
        };

        foreach (var expr in context.expression())
        {
            var element = Visit(expr) as Expression;
            if (element != null)
                tuple.Elements.Add(element);
        }

        return tuple;
    }

    public override AstNode VisitArrayLiteral(BrashParser.ArrayLiteralContext context)
    {
        var array = new ArrayLiteral
        {
            Line = context.Start.Line,
            Column = context.Start.Column
        };

        foreach (var expr in context.expression())
        {
            var element = Visit(expr) as Expression;
            if (element != null)
                array.Elements.Add(element);
        }

        return array;
    }

    public override AstNode VisitMapLiteral(BrashParser.MapLiteralContext context)
    {
        var map = new MapLiteral
        {
            Line = context.Start.Line,
            Column = context.Start.Column
        };

        foreach (var entry in context.mapEntry())
        {
            var key = Visit(entry.expression(0)) as Expression ?? new NullLiteral();
            var value = Visit(entry.expression(1)) as Expression ?? new NullLiteral();
            map.Entries.Add((key, value));
        }

        return map;
    }

    public override AstNode VisitStructLiteral(BrashParser.StructLiteralContext context)
    {
        var structLiteral = new StructLiteral
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            TypeName = context.IDENTIFIER().GetText()
        };

        foreach (var assignment in context.fieldAssignment())
        {
            structLiteral.Fields.Add((
                assignment.IDENTIFIER().GetText(),
                Visit(assignment.expression()) as Expression ?? new NullLiteral()));
        }

        return structLiteral;
    }

    public override AstNode VisitMemberAccess(BrashParser.MemberAccessContext context)
    {
        return new MemberAccessExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Object = Visit(context.expression()) as Expression ?? new NullLiteral(),
            MemberName = context.IDENTIFIER().GetText()
        };
    }

    public override AstNode VisitIndexAccess(BrashParser.IndexAccessContext context)
    {
        return new IndexAccessExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Array = Visit(context.expression(0)) as Expression ?? new NullLiteral(),
            Index = Visit(context.expression(1)) as Expression ?? new NullLiteral()
        };
    }

    public override AstNode VisitLiteral(BrashParser.LiteralContext context)
    {
        if (context.INTEGER() != null)
        {
            return new LiteralExpression
            {
                Line = context.Start.Line,
                Column = context.Start.Column,
                Value = int.Parse(context.INTEGER().GetText()),
                Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Int }
            };
        }

        if (context.FLOAT() != null)
        {
            return new LiteralExpression
            {
                Line = context.Start.Line,
                Column = context.Start.Column,
                Value = double.Parse(context.FLOAT().GetText()),
                Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Float }
            };
        }

        if (context.BOOLEAN() != null)
        {
            return new LiteralExpression
            {
                Line = context.Start.Line,
                Column = context.Start.Column,
                Value = bool.Parse(context.BOOLEAN().GetText()),
                Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Bool }
            };
        }

        if (context.CHAR() != null)
        {
            var text = context.CHAR().GetText();
            return new LiteralExpression
            {
                Line = context.Start.Line,
                Column = context.Start.Column,
                Value = text.Length >= 3 ? text[1] : '\0',
                Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Char }
            };
        }

        if (context.stringLiteral() != null)
        {
            return new LiteralExpression
            {
                Line = context.Start.Line,
                Column = context.Start.Column,
                Value = UnquoteStringLiteral(context.stringLiteral().GetText()),
                Type = new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.String }
            };
        }

        return new NullLiteral { Line = context.Start.Line, Column = context.Start.Column };
    }

    // ============================================
    // Types
    // ============================================

    public override AstNode VisitType(BrashParser.TypeContext context)
    {
        TypeNode baseType;

        if (context.baseType() != null)
        {
            baseType = Visit(context.baseType()) as TypeNode ?? new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };
        }
        else
        {
            return new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void };
        }

        // Apply suffixes
        foreach (var suffix in context.typeSuffix())
        {
            if (suffix.GetText() == "[]")
            {
                baseType = new ArrayType { ElementType = baseType };
            }
            else if (suffix.GetText() == "?")
            {
                baseType = new NullableType { BaseType = baseType };
            }
        }

        return baseType;
    }

    public override AstNode VisitPrimitiveType(BrashParser.PrimitiveTypeContext context)
    {
        var text = context.GetText();
        var kind = text switch
        {
            "int" => PrimitiveType.Kind.Int,
            "float" => PrimitiveType.Kind.Float,
            "string" => PrimitiveType.Kind.String,
            "bool" => PrimitiveType.Kind.Bool,
            "char" => PrimitiveType.Kind.Char,
            _ => PrimitiveType.Kind.Void
        };

        return new PrimitiveType { PrimitiveKind = kind };
    }

    public override AstNode VisitTupleType(BrashParser.TupleTypeContext context)
    {
        var tuple = new TupleType
        {
            Line = context.Start.Line,
            Column = context.Start.Column
        };

        foreach (var typeCtx in context.type())
        {
            var elementType = Visit(typeCtx) as TypeNode;
            if (elementType != null)
                tuple.ElementTypes.Add(elementType);
        }

        return tuple;
    }

    public override AstNode VisitMapType(BrashParser.MapTypeContext context)
    {
        var keyType = Visit(context.type(0)) as TypeNode ?? new UnknownType();
        var valueType = Visit(context.type(1)) as TypeNode ?? new UnknownType();
        return new MapType
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            KeyType = keyType,
            ValueType = valueType
        };
    }

    public override AstNode VisitBaseType(BrashParser.BaseTypeContext context)
    {
        if (context.primitiveType() != null)
            return Visit(context.primitiveType());

        if (context.mapType() != null)
            return Visit(context.mapType());

        if (context.IDENTIFIER() != null)
        {
            return new NamedType
            {
                Line = context.Start.Line,
                Column = context.Start.Column,
                Name = context.IDENTIFIER().GetText()
            };
        }

        return new UnknownType();
    }

    private AstNode BuildBinaryExpression(ParserRuleContext context)
    {
        return new BinaryExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Left = Visit(((dynamic)context).expression(0)) as Expression ?? new NullLiteral(),
            Operator = context.GetChild(1).GetText(),
            Right = Visit(((dynamic)context).expression(1)) as Expression ?? new NullLiteral()
        };
    }

    private CommandExpression BuildCommandExpression(ParserRuleContext context, CommandKind kind, bool isAsync)
    {
        var cmd = new CommandExpression
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Kind = kind,
            IsAsync = isAsync
        };

        var argumentList = ((dynamic)context).argumentList();
        foreach (var arg in argumentList.expression())
        {
            var expr = Visit(arg) as Expression;
            if (expr != null)
                cmd.Arguments.Add(expr);
        }

        return cmd;
    }

    private static string UnquoteStringLiteral(string text)
    {
        if (text.StartsWith("$\"") && text.EndsWith("\"") && text.Length >= 3)
            return text[2..^1];
        if (text.StartsWith("\"") && text.EndsWith("\"") && text.Length >= 2)
            return text[1..^1];
        if (text.StartsWith("[[") && text.EndsWith("]]") && text.Length >= 4)
            return text[2..^2];
        return text;
    }
}
