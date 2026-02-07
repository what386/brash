namespace Brash.Cli.Application;

using System.Diagnostics;
using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.CodeGen;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Frontend;
using Brash.Compiler.Semantic;

internal static class CompilePipeline
{
    public static int Check(string inputPath)
    {
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        if (!TryParse(inputPath, out var program))
            return 1;

        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);
        analyzer.Analyze(program!);

        if (diagnostics.HasErrors)
        {
            diagnostics.PrintToConsole();
            return 1;
        }

        return 0;
    }

    public static int Compile(string inputPath, string outputPath)
    {
        if (Check(inputPath) != 0)
            return 1;

        if (!TryParse(inputPath, out var program))
            return 1;

        var generator = new BashGenerator();
        var bash = generator.Generate(program!);

        if (generator.Warnings.Count > 0)
        {
            Console.Error.WriteLine("Code generation unsupported features:");
            foreach (var warning in generator.Warnings)
                Console.Error.WriteLine($"- Unsupported: {warning}");
            return 1;
        }

        try
        {
            File.WriteAllText(outputPath, bash);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to write '{outputPath}': {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Bash emitted: {outputPath}");
        return 0;
    }

    public static int Run(string inputPath, bool keepTemp, IReadOnlyList<string> args)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"brash-run-{Guid.NewGuid():N}.sh");
        if (Compile(inputPath, tempPath) != 0)
            return 1;

        try
        {
            var psi = new ProcessStartInfo("bash")
            {
                UseShellExecute = false
            };
            psi.ArgumentList.Add(tempPath);
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to launch bash.");
                return 1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        finally
        {
            if (keepTemp)
            {
                Console.WriteLine($"Kept generated script: {tempPath}");
            }
            else if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static bool TryParse(string inputPath, out ProgramNode? program)
    {
        program = null;

        var diagnostics = new DiagnosticBag();
        if (!ModuleLoader.TryLoadProgram(inputPath, diagnostics, out program))
        {
            diagnostics.PrintToConsole();
            return false;
        }

        return true;
    }
}
