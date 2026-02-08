namespace Brash.Compiler.Optimization.Bash;

public sealed class BashOptimizer
{
    public string Optimize(string script, BashOptimizationOptions? options = null)
    {
        options ??= new BashOptimizationOptions();

        var passes = BuildPasses(options);
        var current = script;
        foreach (var pass in passes)
            current = pass.Apply(current);

        return current;
    }

    private static IEnumerable<IBashOptimizationPass> BuildPasses(BashOptimizationOptions options)
    {
        if (options.NormalizeLineEndings)
            yield return new NormalizeLineEndingsPass();

        if (options.TrimTrailingWhitespace)
            yield return new TrimTrailingWhitespacePass();
    }
}
