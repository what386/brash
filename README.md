# Brash

Brash is a small typed language that transpiles to Bash.

It is inspired by Lua ergonomics, with language ideas from C# and Rust (`let mut`, structs/enums, impl methods, nullability).

## Current status

Brash is pre-`0.1.0` and under active development.

- Parser, AST, semantic analysis, and Bash codegen are working for a growing core subset.
- Unsupported features fail fast with explicit diagnostics.

## Repository layout

- `src/Brash.Compiler`: compiler implementation (ANTLR grammar, AST, semantic, codegen, CLI)
- `tests/Brash.Compiler.Tests`: unit/integration/E2E tests
- `examples`: language examples and progress targets
- `docs/language-spec.md`: current implemented language behavior
- `docs/architecture.md`: compiler architecture overview
- `TODO.md`: active backlog (mirrors tally tasks)

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

# Compile source to Bash
dotnet run --project src/Brash.Cli/Brash.Cli.csproj -- compile app.bsh -o app.sh

# Compile and run via temp script
dotnet run --project src/Brash.Cli/Brash.Cli.csproj -- run app.bsh -- arg1 arg2
```

Notes:

- `format` exists but is intentionally unimplemented in beta.
- `run` generates a temporary Bash script under `/tmp` and executes it with `bash`.

## Language highlights (implemented)

- `let`, `let mut`, `const`
- functions and return types
- structs, enums, `impl` methods, `self`
- nullability + `??` + safe navigation
- command model:
  - `cmd(...)` -> lazy `Command`
  - `exec(...)` -> blocking execution, returns stdout string
  - `spawn(...)` -> process handle value
  - pipelines via `|`

Example:

```brash
let pipeline = cmd("printf", "abc\n") | cmd("tr", "a-z", "A-Z")
let output = exec(pipeline)
exec("printf", "%s\n", output)
```

## Explicitly unsupported (fail-fast)

These are currently rejected during semantic/transpile-readiness checks:

- `import` module system codegen
- `try/catch` and `throw`
- `await`
- `async fn`
- `async exec(...)` / `async spawn(...)`
- map/tuple/range value codegen paths not yet implemented

## Contributing

1. Run `dotnet test` before opening changes.
2. Keep new language behavior reflected in:
   - `docs/language-spec.md`
   - `docs/architecture.md`
   - tests in `tests/Brash.Compiler.Tests`
