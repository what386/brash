namespace Brash.Compiler.Optimization.Bash;

internal interface IBashOptimizationPass
{
    string Apply(string script);
}
