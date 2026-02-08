# Brash

Brash is a small typed language that transpiles to Bash.

It is inspired by Lua ergonomics, with language ideas from C# and Rust (`let mut`, structs/enums, impl methods, nullability).

## Current status

Brash is approaching a stable `0.1.0` core.

- Parser, AST, semantic analysis, and Bash codegen are working across the main language surface.
- Remaining unsupported features fail fast with explicit diagnostics.

## Repository layout

- `src/Brash.Compiler`: compiler implementation (ANTLR grammar, AST, semantic, codegen)
- `src/Brash.Cli`: user-facing CLI (`brash`)
- `tests/Brash.Compiler.Tests`: unit/integration/E2E tests
- `examples`: language examples and progress targets
- `docs/language-spec.md`: current implemented language behavior
- `docs/architecture.md`: compiler architecture overview

## Build and test

```bash
dotnet build Brash.sln
dotnet test
```

## Run the compiler

```bash
# Parse + print AST
dotnet run --project src/Brash.Compiler/Brash.Compiler.csproj -- examples/01_the-basics.bsh --ast

# Transpile to Bash
dotnet run --project src/Brash.Compiler/Brash.Compiler.csproj -- examples/01_the-basics.bsh --emit-bash out.sh
```

## Brash CLI

Use `Brash.Cli` as the user-facing command-line tool:

```bash
# Show commands
dotnet run --project src/Brash.Cli/Brash.Cli.csproj -- --help

# Validate source (no output file)
dotnet run --project src/Brash.Cli/Brash.Cli.csproj -- check app.bsh

# Format source files
dotnet run --project src/Brash.Cli/Brash.Cli.csproj -- format app.bsh
dotnet run --project src/Brash.Cli/Brash.Cli.csproj -- format . --check

# Compile source to Bash
dotnet run --project src/Brash.Cli/Brash.Cli.csproj -- compile app.bsh -o app.sh

# Compile and run via temp script
dotnet run --project src/Brash.Cli/Brash.Cli.csproj -- run app.bsh -- arg1 arg2
```

Notes:

- `format --check` exits non-zero when files need formatting.
- `run` generates a temporary Bash script under `/tmp` and executes it with `bash`.

## Language highlights (implemented)

- `let`, `let mut`, `const`
- `pub` exports for top-level API surface: `pub fn`, `pub const`, `pub struct`, `pub enum`
- functions and return types
- structs, enums, `impl` methods, `self`
- nullability + `??` + safe navigation
- explicit casts: `(type)expr` (for example `(string)5`)
- string concatenation with `+` (for example `"Hello, " + name`)
- command model:
  - `cmd(...)` -> lazy `Command`
  - `exec(...)` -> blocking execution, returns stdout string
  - `spawn(...)` -> process handle value
  - `async exec(...)` -> fire-and-forget background execution (no awaitable output)
  - `async spawn(...)` -> background process handle (awaitable)
  - `await process` -> waits for `Process` and returns captured stdout
  - pipelines via `|` for command and value flow
  - `bash("...")` / `bash([[...]])` -> inject raw shell execution (statement context)
- module imports:
  - `import { item } from "path.bsh"`
  - `import Name from "path.bsh"`
  - `import "path.bsh"` for all public exports
  - import paths are resolved from the entry file directory

Example:

```brash
let pipeline = cmd("printf", "abc\n") | cmd("tr", "a-z", "A-Z")
let output = exec(pipeline)
exec("printf", "%s\n", output)
```

## Example learning path

Recommended order in `examples/`:

1. `00_general-overview.bsh`
2. `01_the-basics.bsh`
3. `02_functions.bsh`
4. `03_control-flow.bsh`
5. `04_collections.bsh`
6. `05_data-structures.bsh`
7. `06_any-type.bsh`
8. `07_shell-integration.bsh`
9. `08_pipe-operator.bsh`
10. `09_error-handling.bsh`
11. `10_import-system.bsh`
12. `11_async-await.bsh`
13. `12_preprocessor-directives.bsh`

## Explicitly unsupported (fail-fast)

These currently fail during transpilation/codegen:

- range value codegen (ranges are supported for `for ... in start..end`)
- `bash(...)` in expression context (use as a statement)

## Contributing

1. Run `dotnet test` before opening changes.
2. Keep new language behavior reflected in:
   - `docs/language-spec.md`
   - `docs/architecture.md`
   - tests in `tests/Brash.Compiler.Tests`
