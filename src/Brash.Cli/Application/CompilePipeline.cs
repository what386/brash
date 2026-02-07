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
    public static int Check(string inputPath, bool verbose = false)
    {
        var observer = new PipelineObserver(verbose);
        observer.Begin("check");

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            observer.Fail("file not found");
            return 1;
        }

        observer.Phase("parse", $"Loading '{inputPath}'");
        if (!TryParse(inputPath, out var program, out var parseSummary))
        {
            observer.Fail("parse failed");
            return 1;
        }
        observer.PhaseDone("parse", parseSummary);

        observer.Phase("semantic", "Analyzing symbols and types");
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);
        analyzer.Analyze(program!);
        observer.PhaseDone("semantic", diagnostics.GetSummary());

        if (diagnostics.HasErrors)
        {
            diagnostics.PrintToConsole();
            observer.Fail("semantic errors");
            return 1;
        }

        observer.Success("ok");
        return 0;
    }

    public static int Compile(string inputPath, string outputPath, bool verbose = false)
    {
        var observer = new PipelineObserver(verbose);
        observer.Begin("compile");

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            observer.Fail("file not found");
            return 1;
        }

        observer.Phase("parse", $"Loading '{inputPath}'");
        if (!TryParse(inputPath, out var program, out var parseSummary))
        {
            observer.Fail("parse failed");
            return 1;
        }
        observer.PhaseDone("parse", parseSummary);

        observer.Phase("semantic", "Analyzing symbols and types");
        var diagnostics = new DiagnosticBag();
        var analyzer = new SemanticAnalyzer(diagnostics);
        analyzer.Analyze(program!);
        observer.PhaseDone("semantic", diagnostics.GetSummary());
        if (diagnostics.HasErrors)
        {
            diagnostics.PrintToConsole();
            observer.Fail("semantic errors");
            return 1;
        }

        observer.Phase("codegen", "Generating Bash");
        var generator = new BashGenerator();
        var bash = generator.Generate(program!);
        observer.PhaseDone("codegen", $"{generator.Warnings.Count} warning(s)");

        if (generator.Warnings.Count > 0)
        {
            Console.Error.WriteLine("Code generation unsupported features:");
            foreach (var warning in generator.Warnings)
                Console.Error.WriteLine($"- Unsupported: {warning}");
            observer.Fail("codegen warnings");
            return 1;
        }

        observer.Phase("write", $"Writing '{outputPath}'");
        try
        {
            File.WriteAllText(outputPath, bash);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to write '{outputPath}': {ex.Message}");
            observer.Fail("write failed");
            return 1;
        }
        observer.PhaseDone("write", outputPath);

        TryMarkExecutable(outputPath);
        Console.WriteLine($"Bash emitted: {outputPath}");
        observer.Success("ok");
        return 0;
    }

    public static int Run(string inputPath, bool keepTemp, IReadOnlyList<string> args, bool verbose = false)
    {
        var observer = new PipelineObserver(verbose);
        observer.Begin("run");

        var tempPath = Path.Combine(Path.GetTempPath(), $"brash-run-{Guid.NewGuid():N}.sh");
        if (Compile(inputPath, tempPath, verbose) != 0)
        {
            observer.Fail("compile failed");
            return 1;
        }

        try
        {
            observer.Phase("execute", "Launching bash");
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
                observer.Fail("launch failed");
                return 1;
            }

            process.WaitForExit();
            observer.PhaseDone("execute", $"exit={process.ExitCode}");
            observer.Success($"exit={process.ExitCode}");
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

    private static bool TryParse(string inputPath, out ProgramNode? program, out string summary)
    {
        program = null;
        summary = "no parse output";

        var diagnostics = new DiagnosticBag();
        if (!ModuleLoader.TryLoadProgram(inputPath, diagnostics, out program))
        {
            diagnostics.PrintToConsole();
            summary = diagnostics.GetSummary();
            return false;
        }

        var statementCount = program?.Statements.Count ?? 0;
        summary = $"AST statements: {statementCount}";
        return true;
    }

    private static void TryMarkExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            var mode = File.GetUnixFileMode(path);
            mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(path, mode);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Console.Error.WriteLine($"Warning: unable to set executable permissions on '{path}': {ex.Message}");
        }
    }
}

internal sealed class PipelineObserver
{
    private readonly bool verbose;
    private readonly Stopwatch overall = Stopwatch.StartNew();
    private readonly Stopwatch phase = new();
    private string? currentPhase;

    public PipelineObserver(bool verbose)
    {
        this.verbose = verbose;
    }

    public void Begin(string operation)
    {
        if (!verbose)
            return;

        Console.Error.WriteLine($"[brash:{operation}] start");
    }

    public void Phase(string name, string message)
    {
        if (!verbose)
            return;

        currentPhase = name;
        phase.Restart();
        Console.Error.WriteLine($"[brash:{name}] {message}");
    }

    public void PhaseDone(string name, string summary)
    {
        if (!verbose)
            return;

        if (!phase.IsRunning || currentPhase != name)
            return;

        phase.Stop();
        Console.Error.WriteLine($"[brash:{name}] done in {phase.ElapsedMilliseconds}ms ({summary})");
        currentPhase = null;
    }

    public void Success(string summary)
    {
        if (!verbose)
            return;

        Console.Error.WriteLine($"[brash] success in {overall.ElapsedMilliseconds}ms ({summary})");
    }

    public void Fail(string reason)
    {
        if (!verbose)
            return;

        Console.Error.WriteLine($"[brash] failed in {overall.ElapsedMilliseconds}ms ({reason})");
    }
}
