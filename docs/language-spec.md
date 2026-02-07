## Brash Language Spec (Current)

This document describes the behavior currently implemented by the compiler and Bash transpiler.

### Core declarations

- Variables:
  - `let name = expr`
  - `let mut name = expr`
  - `const name = expr`
- Functions:
  - `fn name(args...): ReturnType ... end`
  - `async fn` currently parses and transpiles with the same runtime behavior as `fn`.
- Types:
  - `struct Name ... end`
  - `enum Name ... end`
  - `impl Name ... end` methods using `self`

### Commands and processes

- `cmd(...)` returns a lazy `Command` value.
- `exec(...)` executes synchronously and returns `string` stdout.
- `spawn(...)` starts a background command and returns a `Process` handle.
- `async exec(...)` starts command execution asynchronously in fire-and-forget mode.
- `async spawn(...)` starts background execution and returns a `Process` handle.
- `await expr` waits for a `Process` handle and returns captured stdout.

### Pipe operator

- Command mode:
  - `|` composes command values.
  - To execute a command pipeline, wrap it with `exec(...)`.
  - Example: `let out = exec(cmd("ls") | cmd("wc", "-l"))`
- Value mode:
  - `|` can pass a value into a function/method stage.
  - The stage receives piped input as the implicit first argument.
  - Stage return type must match input type.
  - Example: `a = a | add_two() | double()`

### String concatenation

- `+` supports numeric addition for numeric operands.
- `+` supports string concatenation when either operand is string-like.
- Common usage:
  - `let message = "Hello, " + name`

### Explicit casts

- Cast syntax: `(type)expr`
- Typical usage:
  - `(string)5`
  - `(string)(person.is_adult())`
- Cast validity is checked in semantic analysis.

### Error handling

- `throw expr` writes the value to stderr and fails the current execution path.
- `try ... catch err ... end` captures stderr from the `try` block and binds it to `err` in the catch block.
- Catch blocks run only when the try block exits non-zero.

### Fail-fast unsupported features

The semantic phase intentionally rejects features not ready for stable transpilation:

- `import ...`
- map literal code generation
- range value code generation

These errors are emitted as hard errors so unsupported behavior does not silently transpile.

### Codegen behavior

- Transpilation target is Bash.
- If codegen still encounters unsupported AST constructs, the CLI exits non-zero and reports unsupported items.
