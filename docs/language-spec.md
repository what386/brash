## Brash Language Spec (Current)

This document describes the behavior currently implemented by the compiler and Bash transpiler.

### Core declarations

- Variables:
  - `let name = expr`
  - `let mut name = expr`
  - `const name = expr`
- Functions:
  - `fn name(args...): ReturnType ... end`
  - `async fn` is parsed but currently rejected by semantic analysis.
- Types:
  - `struct Name ... end`
  - `enum Name ... end`
  - `impl Name ... end` methods using `self`

### Commands and processes

- `cmd(...)` returns a lazy `Command` value.
- `exec(...)` executes synchronously and returns `string` stdout.
- `spawn(...)` starts a background command and returns a `Process` handle.
- `async exec(...)` and `async spawn(...)` syntax is supported but currently rejected with explicit errors.
- `await expr` is currently rejected with an explicit error.

### Pipe operator

- `|` composes command values.
- Type rule: both sides of `|` must be `Command`.
- To execute a pipeline, wrap it with `exec(...)`.
  - Example: `let out = exec(cmd("ls") | cmd("wc", "-l"))`

### Fail-fast unsupported features

The semantic phase intentionally rejects features not ready for stable transpilation:

- `import ...`
- `try/catch`
- `throw ...`
- map literal code generation
- tuple value code generation
- range value code generation
- async execution and await flow

These errors are emitted as hard errors so unsupported behavior does not silently transpile.

### Codegen behavior

- Transpilation target is Bash.
- If codegen still encounters unsupported AST constructs, the CLI exits non-zero and reports unsupported items.
