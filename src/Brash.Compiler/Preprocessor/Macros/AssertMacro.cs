namespace Brash.Compiler.Preprocessor.Macros;

internal sealed class AssertMacro : IPredefinedMacro
{
    public MacroDefinition Definition => new()
    {
        Name = "assert",
        Parameters = new[] { "condition" },
        Body = "if !(condition) panic(\"assertion failed\") end"
    };
}
