namespace Brash.Cli.Application.Commands;

using System.CommandLine;

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

        var command = new Command("format", "Format Brash source (unimplemented in beta)")
        {
            fileArgument,
            checkOption
        };

        command.SetAction(parseResult =>
        {
            var paths = parseResult.GetValue(fileArgument) ?? Array.Empty<string>();
            var check = parseResult.GetValue(checkOption);

            Console.Error.WriteLine("format is not implemented yet.");
            Console.Error.WriteLine($"Paths: {string.Join(", ", paths)}");
            Console.Error.WriteLine($"Mode: {(check ? "check-only" : "write")}");
            return 2;
        });

        return command;
    }
}
