namespace Brash.Compiler.Preprocessor.Macros;

internal sealed class ReadlnMacro : IPredefinedMacro
{
    public MacroDefinition Definition => new()
    {
        Name = "readln",
        Parameters = new[] { "prompt" },
        Body = "exec(\"bash\", \"-lc\", \"printf '%s' \\\"$1\\\"; IFS= read -r __brash_readln_in; printf '%s' \\\"$__brash_readln_in\\\"\", \"_\", prompt)"
    };
}
