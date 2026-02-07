namespace Brash.Cli.Application.Commands;

using System.CommandLine;
using Brash.Formatter;

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
            checkOption
        };

        command.SetAction(parseResult =>
        {
            var paths = parseResult.GetValue(fileArgument) ?? Array.Empty<string>();
            var check = parseResult.GetValue(checkOption);
            var files = ResolveFiles(paths);

            if (files.Count == 0)
            {
                Console.Error.WriteLine("No .bsh files found to format.");
                return 1;
            }

            int changed = 0;
            foreach (var file in files)
            {
                var original = File.ReadAllText(file);
                var formatted = BrashFormatter.Format(original);

                if (original == formatted)
                    continue;

                changed++;
                if (check)
                {
                    Console.WriteLine(file);
                }
                else
                {
                    File.WriteAllText(file, formatted);
                    Console.WriteLine($"Formatted {file}");
                }
            }

            if (check)
            {
                if (changed > 0)
                {
                    Console.Error.WriteLine($"{changed} file(s) need formatting.");
                    return 1;
                }

                Console.WriteLine("All files are formatted.");
                return 0;
            }

            Console.WriteLine(changed == 0
                ? "No formatting changes required."
                : $"Formatted {changed} file(s).");
            return 0;
        });

        return command;
    }

    private static List<string> ResolveFiles(IEnumerable<string> paths)
    {
        var files = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                if (path.EndsWith(".bsh", StringComparison.OrdinalIgnoreCase))
                    files.Add(Path.GetFullPath(path));
                continue;
            }

            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.bsh", SearchOption.AllDirectories))
                    files.Add(Path.GetFullPath(file));
            }
        }

        return files.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }
}
