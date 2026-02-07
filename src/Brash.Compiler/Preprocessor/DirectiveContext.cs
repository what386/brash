namespace Brash.Compiler.Preprocessor;

internal readonly record struct DirectiveContext(
    string Trimmed,
    int LineNumber,
    bool CurrentActive,
    PreprocessorState State,
    MacroExpander MacroExpander);

