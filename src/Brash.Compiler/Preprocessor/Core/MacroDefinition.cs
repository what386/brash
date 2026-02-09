namespace Brash.Compiler.Preprocessor.Core;

internal sealed class MacroDefinition
{
    public required string Name { get; init; }
    public required IReadOnlyList<string> Parameters { get; init; }
    public required string Body { get; init; }
}
