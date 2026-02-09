namespace Brash.Compiler.Preprocessor.Macros;

internal sealed class PrintlnMacro : IPredefinedMacro
{
    public MacroDefinition Definition => new()
    {
        Name = "println",
        Parameters = new[] { "value" },
        Body = "exec(\"printf\", \"%s\\n\", value)"
    };
}
