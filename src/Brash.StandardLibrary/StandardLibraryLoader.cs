namespace Brash.StandardLibrary;

public static class StandardLibraryLoader
{
    private const string StdModulePrefix = "std/";
    private const string StdModuleRootName = "std";

    public static bool TryResolveImportPath(string importRoot, string moduleSpec, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        var isStdRoot = string.Equals(moduleSpec, StdModuleRootName, StringComparison.Ordinal);
        var isStdModule = moduleSpec.StartsWith(StdModulePrefix, StringComparison.Ordinal);
        if (!isStdRoot && !isStdModule)
            return false;

        var relative = isStdRoot
            ? "std.bsh"
            : moduleSpec.Substring(StdModulePrefix.Length);

        if (!relative.EndsWith(".bsh", StringComparison.Ordinal))
            relative += ".bsh";

        foreach (var stdRoot in EnumerateStdLibRoots(importRoot))
        {
            var candidate = Path.GetFullPath(Path.Combine(stdRoot, relative));
            if (File.Exists(candidate))
            {
                resolvedPath = candidate;
                return true;
            }
        }

        var fallbackRoot = EnumerateStdLibRoots(importRoot).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fallbackRoot))
        {
            resolvedPath = Path.GetFullPath(Path.Combine(fallbackRoot, relative));
            return true;
        }

        resolvedPath = Path.GetFullPath(Path.Combine(importRoot, moduleSpec));
        return true;
    }

    private static IEnumerable<string> EnumerateStdLibRoots(string importRoot)
    {
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var seen = new HashSet<string>(pathComparer);

        var explicitStdPath = Environment.GetEnvironmentVariable("BRASH_STDLIB_PATH");
        if (!string.IsNullOrWhiteSpace(explicitStdPath))
        {
            var full = Path.GetFullPath(explicitStdPath);
            if (seen.Add(full))
                yield return full;
        }

        foreach (var candidate in EnumerateAncestorCandidates(importRoot))
        {
            if (seen.Add(candidate))
                yield return candidate;
        }

        foreach (var candidate in EnumerateBaseDirectoryCandidates())
        {
            if (seen.Add(candidate))
                yield return candidate;
        }
    }

    private static IEnumerable<string> EnumerateAncestorCandidates(string importRoot)
    {
        var current = new DirectoryInfo(Path.GetFullPath(importRoot));
        while (current != null)
        {
            var projectStdLib = Path.Combine(current.FullName, "src", "Brash.StandardLibrary", "StdLib");
            if (Directory.Exists(projectStdLib))
                yield return projectStdLib;

            var directProjectStdLib = Path.Combine(current.FullName, "Brash.StandardLibrary", "StdLib");
            if (Directory.Exists(directProjectStdLib))
                yield return directProjectStdLib;

            var legacySourceStdLib = Path.Combine(current.FullName, "src", "stdlib");
            if (Directory.Exists(legacySourceStdLib))
                yield return legacySourceStdLib;

            var directStdLib = Path.Combine(current.FullName, "StdLib");
            if (Directory.Exists(directStdLib))
                yield return directStdLib;

            var legacyStdLib = Path.Combine(current.FullName, "stdlib");
            if (Directory.Exists(legacyStdLib))
                yield return legacyStdLib;

            current = current.Parent;
        }
    }

    private static IEnumerable<string> EnumerateBaseDirectoryCandidates()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var directStdLib = Path.Combine(baseDirectory, "StdLib");
        if (Directory.Exists(directStdLib))
            yield return directStdLib;

        var projectStdLib = Path.Combine(baseDirectory, "Brash.StandardLibrary", "StdLib");
        if (Directory.Exists(projectStdLib))
            yield return projectStdLib;

        var sourceProjectStdLib = Path.Combine(baseDirectory, "src", "Brash.StandardLibrary", "StdLib");
        if (Directory.Exists(sourceProjectStdLib))
            yield return sourceProjectStdLib;

        var legacyStdLib = Path.Combine(baseDirectory, "stdlib");
        if (Directory.Exists(legacyStdLib))
            yield return legacyStdLib;

        var legacySourceStdLib = Path.Combine(baseDirectory, "src", "stdlib");
        if (Directory.Exists(legacySourceStdLib))
            yield return legacySourceStdLib;
    }
}
