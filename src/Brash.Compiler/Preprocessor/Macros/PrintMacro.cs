namespace Brash.Compiler.Preprocessor.Macros;

internal sealed class PrintMacro : IPredefinedMacro
{
    public MacroDefinition Definition => new()
    {
        Name = "print",
        Parameters = new[] { "value" },
        Body = "exec(\"printf\", \"%s\", value)"
    };
}
