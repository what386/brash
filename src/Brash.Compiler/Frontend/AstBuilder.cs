namespace Brash.Compiler.Frontend;

using Brash.Compiler.Ast;
using Brash.Compiler.Frontend;

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
        var kindText = context.GetChild(0).GetText();
        var kind = kindText switch
        {
            "let" => VariableDeclaration.VarKind.Let,
            "mut" => VariableDeclaration.VarKind.Mut,
            "const" => VariableDeclaration.VarKind.Const,
            _ => VariableDeclaration.VarKind.Let
        };

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

    public override AstNode VisitRecordDeclaration(BrashParser.RecordDeclarationContext context)
    {
        var recordDecl = new RecordDeclaration
        {
            Line = context.Start.Line,
            Column = context.Start.Column,
            Name = context.IDENTIFIER().GetText()
        };

        if (context.structBody() != null)
        {
            foreach (var field in context.structBody().fieldDeclaration())
            {
                recordDecl.Fields.Add(new FieldDeclaration
                {
                    Name = field.IDENTIFIER().GetText(),
                    Type = Visit(field.type()) as TypeNode ?? new PrimitiveType { PrimitiveKind = PrimitiveType.Kind.Void }
                });
            }
        }

        return recordDecl;
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

    public override AstNode VisitFunctionCallExpr(BrashParser.FunctionCallExprContext context)
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
}
