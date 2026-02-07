namespace Brash.Cli.Application.Commands;

using System.CommandLine;
using Brash.Cli.Application;

static class FormatCommand
{
    public static Command Create()
    {
        Argument<string[]> fileArgument = new("paths")
        {
            Description = "Path(s) to .bsh file(s) or directories to format",
            Arity = ArgumentArity.OneOrMore
        };

        Option<bool> checkOption = new("--check")
        {
            Description = "Check formatting without modifying files",
            DefaultValueFactory = parseResult => false
        };

        var command = new Command("format", "Format Brash source files")
        {
            fileArgument,
            checkOption,
            SharedOptions.Verbose
        };

        command.SetAction(parseResult =>
        {
            var paths = parseResult.GetValue(fileArgument) ?? Array.Empty<string>();
            var check = parseResult.GetValue(checkOption);
            var verbose = parseResult.GetValue(SharedOptions.Verbose);
            return CompilePipeline.Format(paths, check, verbose);
        });

        return command;
    }
}
