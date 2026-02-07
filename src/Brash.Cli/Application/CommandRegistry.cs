namespace Brash.Cli.Application;

using System.CommandLine;
using Brash.Cli.Application.Commands;

static class CommandRegistry
{
    public static void RegisterCommands(RootCommand root)
    {
        root.Subcommands.Add(CompileCommand.Create());
        root.Subcommands.Add(CheckCommand.Create());
        root.Subcommands.Add(FormatCommand.Create());
        root.Subcommands.Add(RunCommand.Create());
    }
}
