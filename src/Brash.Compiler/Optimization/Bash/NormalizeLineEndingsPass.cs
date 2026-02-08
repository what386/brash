namespace Brash.Compiler.Optimization.Bash;

internal sealed class NormalizeLineEndingsPass : IBashOptimizationPass
{
    public string Apply(string script)
    {
        return script.Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Replace("\r", "\n", StringComparison.Ordinal);
    }
}
