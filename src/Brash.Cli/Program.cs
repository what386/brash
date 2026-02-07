namespace Brash.Cli;

using System.CommandLine;
using Brash.Cli.Application;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Brash CLI");

        rootCommand.SetAction(parseResult =>
        {
            Console.WriteLine("Use `brash --help` to see available commands.");
            return 0;
        });

        CommandRegistry.RegisterCommands(rootCommand);
        return await rootCommand.Parse(args).InvokeAsync();
    }
}
