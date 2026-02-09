namespace Brash.Compiler.Preprocessor.Macros;

internal sealed class PanicMacro : IPredefinedMacro
{
    public MacroDefinition Definition => new()
    {
        Name = "panic",
        Parameters = new[] { "message" },
        Body = "panic(message)"
    };
}
