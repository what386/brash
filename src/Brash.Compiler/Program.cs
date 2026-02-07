namespace Brash.Compiler;

using System.Collections;
using System.Reflection;
using Brash.Compiler.Ast;
using Brash.Compiler.CodeGen;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            PrintUsage();
            return 1;
        }

        var options = ParseOptions(args);
        if (!options.IsValid)
        {
            Console.Error.WriteLine(options.ErrorMessage);
            PrintUsage();
            return 1;
        }

        var path = options.InputPath!;
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return 1;
        }

        var diagnostics = new DiagnosticBag();
        if (!ModuleLoader.TryLoadProgram(path, diagnostics, out var program))
        {
            diagnostics.PrintToConsole();
            return 1;
        }

        if (options.PrintAst)
        {
            Console.WriteLine($"Parsed: {path}");
            Console.WriteLine("AST:");
            AstPrinter.Print(program);
        }

        if (options.EmitBashPath != null)
        {
            var semanticDiagnostics = new DiagnosticBag();
            var analyzer = new SemanticAnalyzer(semanticDiagnostics);
            analyzer.Analyze(program);

            if (semanticDiagnostics.HasErrors)
            {
                semanticDiagnostics.PrintToConsole();
                return 1;
            }

            var generator = new BashGenerator();
            var bash = generator.Generate(program);

            try
            {
                File.WriteAllText(options.EmitBashPath, bash);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write '{options.EmitBashPath}': {ex.Message}");
                return 1;
            }

            Console.WriteLine($"Bash emitted: {options.EmitBashPath}");
            if (generator.Warnings.Count > 0)
            {
                Console.Error.WriteLine("Code generation unsupported features:");
                foreach (var warning in generator.Warnings)
                    Console.Error.WriteLine($"- Unsupported: {warning}");
                return 1;
            }
        }

        return 0;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  brash-compiler <input-file.bsh> [--ast] [--emit-bash <output.sh>]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Examples:");
        Console.Error.WriteLine("  brash-compiler examples/01_the-basics.bsh --ast");
        Console.Error.WriteLine("  brash-compiler examples/01_the-basics.bsh --emit-bash out.sh");
        Console.Error.WriteLine("  brash-compiler examples/01_the-basics.bsh --ast --emit-bash out.sh");
    }

    private static Options ParseOptions(string[] args)
    {
        var options = new Options();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--ast":
                    options.PrintAst = true;
                    break;

                case "--emit-bash":
                    if (i + 1 >= args.Length)
                        return Options.Invalid("Missing output path after --emit-bash.");
                    options.EmitBashPath = args[++i];
                    break;

                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                        return Options.Invalid($"Unknown option: {arg}");
                    if (options.InputPath != null)
                        return Options.Invalid("Only one input file is supported.");
                    options.InputPath = arg;
                    break;
            }
        }

        if (options.InputPath == null)
            return Options.Invalid("Missing input .bsh file path.");

        if (!options.PrintAst && options.EmitBashPath == null)
            options.PrintAst = true; // default mode stays parser/AST inspection

        return options;
    }
}

internal sealed class Options
{
    public string? InputPath { get; set; }
    public bool PrintAst { get; set; }
    public string? EmitBashPath { get; set; }
    public bool IsValid { get; private set; } = true;
    public string ErrorMessage { get; private set; } = string.Empty;

    public static Options Invalid(string message)
    {
        return new Options
        {
            IsValid = false,
            ErrorMessage = message
        };
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
