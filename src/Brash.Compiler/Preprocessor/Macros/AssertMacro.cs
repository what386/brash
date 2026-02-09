namespace Brash.Compiler.Preprocessor.Macros;

internal sealed class AssertMacro : IPredefinedMacro
{
    public MacroDefinition Definition => new()
    {
        Name = "assert",
        Parameters = new[] { "condition" },
        Body = "if !(condition)\n    exec(\"printf\", \"%s\\n\", \"assertion failed\")\n    sh exit 1\nend"
    };
}
