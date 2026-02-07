namespace Brash.Cli;

using System.CommandLine;
using Brash.Cli.Application;

internal static class Program
{
    private const string Version = "0.1.0-beta";

    private static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Brash CLI");

        var versionOption = new Option<bool>("--version")
        {
            Description = "Show Brash CLI version."
        };

        rootCommand.Options.Add(versionOption);
        rootCommand.SetAction(parseResult =>
        {
            if (parseResult.GetValue(versionOption))
            {
                Console.WriteLine($"brash {Version}");
                return 0;
            }

            Console.WriteLine("Use `brash --help` to see available commands.");
            return 0;
        });

        CommandRegistry.RegisterCommands(rootCommand);
        return await rootCommand.Parse(args).InvokeAsync();
    }
}
