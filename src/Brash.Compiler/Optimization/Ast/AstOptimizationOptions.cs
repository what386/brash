namespace Brash.Compiler.Optimization.Ast;

public sealed class AstOptimizationOptions
{
    public bool Enable { get; set; } = true;
    public bool EnableConstantPropagation { get; set; } = true;
    public bool EnableConstantFolding { get; set; } = true;
    public bool EnableControlFlowSimplification { get; set; } = true;
    public bool EnableDeadLocalElimination { get; set; } = true;
}
