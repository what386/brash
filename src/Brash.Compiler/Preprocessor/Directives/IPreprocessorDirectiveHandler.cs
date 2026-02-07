namespace Brash.Compiler.Preprocessor.Directives;

internal interface IPreprocessorDirectiveHandler
{
    bool CanHandle(string trimmedLine);
    void Apply(DirectiveContext context);
}

