namespace Brash.Cli.Application.Commands;

using System.CommandLine;
using Brash.Cli.Application;

static class RunCommand
{
    public static Command Create()
    {
        Argument<string> fileArgument = new("file")
        {
            Description = "Path to the .bsh file to run"
        };

        Option<bool> keepTempOption = new("--keep-temp")
        {
            Description = "Keep generated temporary Bash file",
            DefaultValueFactory = parseResult => false
        };

        Argument<string[]> argsArgument = new("args")
        {
            Description = "Arguments passed to the Brash program",
            Arity = ArgumentArity.ZeroOrMore
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

        var command = new Command("run", "Compile a .bsh file to /tmp and run it with bash")
        {
            fileArgument,
            keepTempOption,
            argsArgument,
            SharedOptions.Verbose
        };

        command.SetAction(parseResult =>
        {
            var file = parseResult.GetValue(fileArgument)!;
            var keepTemp = parseResult.GetValue(keepTempOption);
            var args = parseResult.GetValue(argsArgument) ?? Array.Empty<string>();
            var verbose = parseResult.GetValue(SharedOptions.Verbose);
            return CompilePipeline.Run(file, keepTemp, args, verbose);
        });

        return command;
    }
}
