namespace Brash.Compiler.Frontend;

using Antlr4.Runtime;
using Brash.Compiler.Ast;
using Brash.Compiler.Ast.Statements;
using Brash.Compiler.Diagnostics;
using Brash.Compiler.Preprocessor;
using Brash.StandardLibrary;

public static class ModuleLoader
{
    public static bool TryLoadProgram(string entryPath, DiagnosticBag diagnostics, out ProgramNode? program)
    {
        program = null;

        var fullEntryPath = Path.GetFullPath(entryPath);
        if (!File.Exists(fullEntryPath))
        {
            diagnostics.AddError($"File not found: {fullEntryPath}");
            return false;
        }

        var importRoot = Path.GetDirectoryName(fullEntryPath) ?? Directory.GetCurrentDirectory();
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var modules = new Dictionary<string, ProgramNode>(pathComparer);
        var parsingStack = new HashSet<string>(pathComparer);
        var emittedDeclarationKeys = new HashSet<string>(pathComparer);

        if (!ParseModuleRecursive(fullEntryPath, importRoot, diagnostics, modules, parsingStack))
            return false;

        if (diagnostics.HasErrors)
            return false;

        if (!modules.TryGetValue(fullEntryPath, out var entryProgram))
        {
            diagnostics.AddError($"Failed to parse entry module: {fullEntryPath}");
            return false;
        }

        var merged = new ProgramNode
        {
            Line = entryProgram.Line,
            Column = entryProgram.Column
        };

        foreach (var stmt in entryProgram.Statements)
        {
            if (stmt is ImportStatement importStmt)
            {
                ExpandImport(importStmt, importRoot, modules, emittedDeclarationKeys, diagnostics, merged.Statements);
                continue;
            }

            merged.Statements.Add(stmt);
        }

        if (diagnostics.HasErrors)
            return false;

        program = merged;
        return true;
    }

    private static bool ParseModuleRecursive(
        string modulePath,
        string importRoot,
        DiagnosticBag diagnostics,
        Dictionary<string, ProgramNode> modules,
        HashSet<string> parsingStack)
    {
        if (modules.ContainsKey(modulePath))
            return true;

        if (!parsingStack.Add(modulePath))
        {
            diagnostics.AddError($"Circular import detected for module '{modulePath}'");
            return false;
        }

        if (!TryParseSingleFile(modulePath, diagnostics, out var module))
        {
            parsingStack.Remove(modulePath);
            return false;
        }

        modules[modulePath] = module!;

        foreach (var import in module!.Statements.OfType<ImportStatement>())
        {
            var spec = import.FromModule ?? import.Module;
            if (string.IsNullOrWhiteSpace(spec))
            {
                diagnostics.AddError("Import statement is missing a module path", import.Line, import.Column);
                continue;
            }

            var resolvedPath = ResolveImportPath(importRoot, spec);
            if (!File.Exists(resolvedPath))
            {
                diagnostics.AddError(
                    $"Imported module not found: '{spec}' (resolved to '{resolvedPath}')",
                    import.Line,
                    import.Column);
                continue;
            }

            ParseModuleRecursive(resolvedPath, importRoot, diagnostics, modules, parsingStack);
        }

        parsingStack.Remove(modulePath);
        return !diagnostics.HasErrors;
    }

    private static void ExpandImport(
        ImportStatement importStmt,
        string importRoot,
        IReadOnlyDictionary<string, ProgramNode> modules,
        HashSet<string> emittedDeclarationKeys,
        DiagnosticBag diagnostics,
        IList<Statement> destination)
    {
        var spec = importStmt.FromModule ?? importStmt.Module;
        if (string.IsNullOrWhiteSpace(spec))
        {
            diagnostics.AddError("Import statement is missing a module path", importStmt.Line, importStmt.Column);
            return;
        }

        var resolvedPath = ResolveImportPath(importRoot, spec);
        if (!modules.TryGetValue(resolvedPath, out var module))
        {
            diagnostics.AddError(
                $"Imported module not loaded: '{spec}' (resolved to '{resolvedPath}')",
                importStmt.Line,
                importStmt.Column);
            return;
        }

        // Ensure imported modules can depend on their own imports.
        foreach (var nestedImport in module.Statements.OfType<ImportStatement>())
        {
            ExpandImport(nestedImport, importRoot, modules, emittedDeclarationKeys, diagnostics, destination);
        }

        var moduleDeclarations = GetTopLevelExportables(module);
        if (importStmt.Module != null && importStmt.FromModule == null)
        {
            foreach (var declaration in moduleDeclarations.Where(IsPublicExport))
                TryEmitImportedDeclaration(resolvedPath, declaration, emittedDeclarationKeys, destination);
            return;
        }

        foreach (var importedName in importStmt.ImportedItems)
        {
            var match = moduleDeclarations.FirstOrDefault(d => string.Equals(GetDeclarationName(d), importedName, StringComparison.Ordinal));
            if (match == null)
            {
                diagnostics.AddError(
                    $"Module '{spec}' does not export '{importedName}'",
                    importStmt.Line,
                    importStmt.Column);
                continue;
            }

            if (!IsPublicExport(match))
            {
                diagnostics.AddError(
                    $"'{importedName}' in module '{spec}' is not public. Mark it with 'pub' to import it.",
                    importStmt.Line,
                    importStmt.Column);
                continue;
            }

            TryEmitImportedDeclaration(resolvedPath, match, emittedDeclarationKeys, destination);
        }
    }

    private static void TryEmitImportedDeclaration(
        string modulePath,
        Statement declaration,
        HashSet<string> emittedDeclarationKeys,
        IList<Statement> destination)
    {
        var name = GetDeclarationName(declaration);
        if (string.IsNullOrWhiteSpace(name))
            return;

        var key = modulePath + "::" + name;
        if (!emittedDeclarationKeys.Add(key))
            return;

        destination.Add(declaration);
    }

    private static IEnumerable<Statement> GetTopLevelExportables(ProgramNode module)
    {
        foreach (var stmt in module.Statements)
        {
            if (stmt is ImportStatement)
                continue;

            if (stmt is FunctionDeclaration
                or StructDeclaration
                or EnumDeclaration
                or VariableDeclaration)
            {
                yield return stmt;
            }
        }
    }

    private static bool IsPublicExport(Statement declaration)
    {
        return declaration switch
        {
            FunctionDeclaration fn => fn.IsPublic,
            StructDeclaration st => st.IsPublic,
            EnumDeclaration en => en.IsPublic,
            VariableDeclaration varDecl => varDecl.IsPublic && varDecl.Kind == VariableDeclaration.VarKind.Const,
            _ => false
        };
    }

    private static string? GetDeclarationName(Statement declaration)
    {
        return declaration switch
        {
            FunctionDeclaration fn => fn.Name,
            StructDeclaration st => st.Name,
            EnumDeclaration en => en.Name,
            VariableDeclaration varDecl => varDecl.Name,
            _ => null
        };
    }

    private static string ResolveImportPath(string importRoot, string moduleSpec)
    {
        if (StandardLibraryLoader.TryResolveImportPath(importRoot, moduleSpec, out var stdPath))
            return stdPath;

        return Path.GetFullPath(Path.Combine(importRoot, moduleSpec));
    }

    private static bool TryParseSingleFile(string path, DiagnosticBag diagnostics, out ProgramNode? program)
    {
        program = null;

        string source;
        try
        {
            source = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            diagnostics.AddError($"Failed to read '{path}': {ex.Message}");
            return false;
        }

        var preprocessor = new BrashPreprocessor();
        source = preprocessor.Process(source, diagnostics);
        if (diagnostics.HasErrors)
            return false;

        var input = new AntlrInputStream(source);
        var lexer = new BrashLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new BrashParser(tokens);

        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        lexer.AddErrorListener(new LoaderLexerErrorListener(diagnostics, path));
        parser.AddErrorListener(new LoaderParserErrorListener(diagnostics, path));

        var parseTree = parser.program();
        if (diagnostics.HasErrors)
            return false;

        var ast = new AstBuilder().VisitProgram(parseTree);
        if (ast is not ProgramNode programNode)
        {
            diagnostics.AddError($"Failed to build AST root for '{path}'");
            return false;
        }

        program = programNode;
        return true;
    }
}

internal sealed class LoaderLexerErrorListener : IAntlrErrorListener<int>
{
    private readonly DiagnosticBag diagnostics;
    private readonly string path;

    public LoaderLexerErrorListener(DiagnosticBag diagnostics, string path)
    {
        this.diagnostics = diagnostics;
        this.path = path;
    }

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        diagnostics.AddDiagnostic(new Diagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Message = SyntaxErrorFormatter.FormatLexerError(msg),
            Line = line,
            Column = charPositionInLine,
            Code = "E000",
            FilePath = path
        });
    }
}

internal sealed class LoaderParserErrorListener : BaseErrorListener
{
    private readonly DiagnosticBag diagnostics;
    private readonly string path;

    public LoaderParserErrorListener(DiagnosticBag diagnostics, string path)
    {
        this.diagnostics = diagnostics;
        this.path = path;
    }

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        diagnostics.AddDiagnostic(new Diagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Message = SyntaxErrorFormatter.FormatParserError(offendingSymbol, msg),
            Line = line,
            Column = charPositionInLine,
            Code = "E001",
            FilePath = path
        });
    }
}
