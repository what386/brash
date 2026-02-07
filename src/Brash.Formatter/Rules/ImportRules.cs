namespace Brash.Formatter;

internal static class ImportRules
{
    public static List<string> SortTopLevelImports(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            return new List<string>();

        var result = new List<string>(lines.Count);
        int index = 0;

        while (index < lines.Count)
        {
            if (!IsTopLevelImportLine(lines[index]))
            {
                result.Add(lines[index]);
                index++;
                continue;
            }

            int start = index;
            while (index < lines.Count && IsTopLevelImportLine(lines[index]))
                index++;

            var block = lines.Skip(start).Take(index - start).ToList();
            block.Sort(StringComparer.Ordinal);
            result.AddRange(block);
        }

        return result;
    }

    private static bool IsTopLevelImportLine(string line)
    {
        if (line.Length == 0 || char.IsWhiteSpace(line[0]))
            return false;

        return line.StartsWith("import ", StringComparison.Ordinal) || line == "import";
    }
}
