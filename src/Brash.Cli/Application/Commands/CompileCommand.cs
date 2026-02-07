namespace Brash.Cli.Application.Commands;

using System.CommandLine;
using Brash.Cli.Application;


static class CompileCommand
{
    public static Command Create()
    {
        Argument<string> fileArgument = new("file")
        {
            Description = "Path to the .bsh file to compile"
        };

        Option<string?> outputOption = new("-o", "--output")
        {
            Description = "Output file path (defaults to <input>.sh)"
        };

        fileArgument.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>() ?? "";
            if (string.IsNullOrWhiteSpace(value))
            {
                result.AddError("File path cannot be empty.");
                return;
            }
            if (!value.EndsWith(".bsh"))
            {
                result.AddError("File must have a .bsh extension.");
            }
        });

        var command = new Command("compile", "Compile a .bsh file to Bash")
        {
            fileArgument,
            outputOption,
            SharedOptions.Verbose
        };

        command.SetAction(parseResult =>
        {
            var file = parseResult.GetValue(fileArgument)!;
            var output = parseResult.GetValue(outputOption);
            var verbose = parseResult.GetValue(SharedOptions.Verbose);
            output ??= Path.Combine(
                Path.GetDirectoryName(file) ?? ".",
                $"{Path.GetFileNameWithoutExtension(file)}.sh");

            return CompilePipeline.Compile(file, output, verbose);
        });

        return command;
    }
}
