namespace Brash.Compiler.Optimization.Bash;

internal sealed class TrimTrailingWhitespacePass : IBashOptimizationPass
{
    public string Apply(string script)
    {
        var lines = script.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            lines[i] = lines[i].TrimEnd(' ', '\t');
        return string.Join('\n', lines);
    }
}
