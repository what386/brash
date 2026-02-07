namespace Brash.Compiler;

using System.Collections;
using System.Reflection;
using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: brash-compiler <input-file.bsh>");
            return 1;
        }

        var path = args[0];
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return 1;
        }

        string source;
        try
        {
            source = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read '{path}': {ex.Message}");
            return 1;
        }

        var diagnostics = new DiagnosticBag();
        var input = new AntlrInputStream(source);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        lexer.AddErrorListener(new LexerDiagnosticErrorListener(diagnostics));
        parser.AddErrorListener(new Brash.Compiler.Diagnostics.DiagnosticErrorListener(diagnostics));

        var parseTree = parser.program();

        if (diagnostics.HasErrors)
        {
            diagnostics.PrintToConsole();
            return 1;
        }

        var astBuilder = new AstBuilder();
        var ast = astBuilder.VisitProgram(parseTree);
        if (ast is not ProgramNode program)
        {
            Console.Error.WriteLine("Failed to build AST root.");
            return 1;
        }

        Console.WriteLine($"Parsed: {path}");
        Console.WriteLine("AST:");
        AstPrinter.Print(program);

        return 0;
    }
}

internal sealed class LexerDiagnosticErrorListener : IAntlrErrorListener<int>
{
    private readonly DiagnosticBag diagnostics;

    public LexerDiagnosticErrorListener(DiagnosticBag diagnostics)
    {
        this.diagnostics = diagnostics;
    }

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        diagnostics.AddError($"Lexer error: {msg}", line, charPositionInLine, "E000");
    }
}

internal static class AstPrinter
{
    public static void Print(AstNode node)
    {
        PrintNode(node, indent: 0);
    }

    private static void PrintNode(object? value, int indent)
    {
        if (value == null)
        {
            WriteIndent(indent);
            Console.WriteLine("<null>");
            return;
        }

        if (value is string s)
        {
            WriteIndent(indent);
            Console.WriteLine($"\"{s}\"");
            return;
        }

        if (value is IEnumerable enumerable && value is not AstNode)
        {
            var items = enumerable.Cast<object?>().ToList();
            WriteIndent(indent);
            Console.WriteLine($"[{items.Count}]");

            foreach (var item in items)
            {
                PrintNode(item, indent + 1);
            }

            return;
        }

        var type = value.GetType();
        if (IsSimple(type))
        {
            WriteIndent(indent);
            Console.WriteLine(value);
            return;
        }

        WriteIndent(indent);
        Console.WriteLine(type.Name);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead)
                continue;

            var propValue = prop.GetValue(value);
            WriteIndent(indent + 1);
            Console.WriteLine($"{prop.Name}:");
            PrintNode(propValue, indent + 2);
        }
    }

    private static bool IsSimple(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(Guid);
    }

    private static void WriteIndent(int indent)
    {
        Console.Write(new string(' ', indent * 2));
    }
}
