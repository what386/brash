namespace Brash.Compiler.Preprocessor.Core;

internal readonly record struct DirectiveContext(
    string Trimmed,
    int LineNumber,
    bool CurrentActive,
    PreprocessorState State,
    MacroExpander MacroExpander);

