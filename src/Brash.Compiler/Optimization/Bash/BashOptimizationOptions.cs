namespace Brash.Compiler.Optimization.Bash;

public sealed class BashOptimizationOptions
{
    public bool NormalizeLineEndings { get; set; } = true;
    public bool TrimTrailingWhitespace { get; set; } = true;
}
