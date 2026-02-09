namespace Brash.Compiler.Preprocessor.Macros;

internal static class PredefinedMacroRegistry
{
    public static IReadOnlyList<MacroDefinition> All { get; } = new IPredefinedMacro[]
    {
        new PrintMacro(),
        new PrintlnMacro(),
        new ReadlnMacro(),
        new PanicMacro(),
        new AssertMacro()
    }.Select(m => m.Definition).ToArray();
}
