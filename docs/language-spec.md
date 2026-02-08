## Brash Language Spec (Current)

This document describes the behavior currently implemented by the compiler and Bash transpiler.

### Core declarations

- Variables:
  - `let name = expr`
  - `let mut name = expr`
  - `const name = expr`
  - `pub const NAME = expr` for module exports
- Functions:
  - `fn name(args...): ReturnType ... end`
  - `pub fn name(args...): ReturnType ... end`
- Types:
  - `struct Name ... end`
  - `pub struct Name ... end`
  - `enum Name ... end`
  - `pub enum Name ... end`
  - `impl Name ... end` methods using `self`

### Imports

- Named imports:
  - `import { helper_fn, CONFIG } from "lib/tools.bsh"`
  - `import Name from "models/name.bsh"` (single-name form)
- Module import:
  - `import "shared/common.bsh"` imports all public top-level exports from that module.
- Export visibility:
  - Only `pub` declarations are importable (`pub fn`, `pub const`, `pub struct`, `pub enum`).
  - `pub let` / `pub let mut` are rejected.
- Import path resolution:
  - Import paths are resolved from the entry file directory (absolute-to-main behavior), not from the importing module's directory.

### Commands and processes

- `cmd(...)` returns a lazy `Command` value.
- `exec(...)` executes synchronously and returns `string` stdout.
- `spawn(...)` starts a background command and returns a `Process` handle.
- `async exec(...)` starts command execution asynchronously in fire-and-forget mode.
- `async spawn(...)` starts background execution and returns a `Process` handle.
- `await expr` waits for a `Process` handle and returns captured stdout.
- `sh ...` executes shell text directly (statement context):
  - emitted inline in generated Bash exactly as written after `sh`

### Builtin I/O

- `print(...)` writes arguments without a trailing newline.
- `println(...)` writes arguments with a trailing newline.
- `readln()` reads one line from stdin and returns `string`.
- `readln(prompt)` writes `prompt` (without newline), then reads one line and returns `string`.
- `print`, `println`, `readln`, and `panic` are reserved builtin names and cannot be redefined.

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
  - `5 as string`
  - `person.is_adult() as string`
- Cast validity is checked in semantic analysis.

### Error handling

- `throw expr` writes the value to stderr and fails the current execution path.
- `try ... catch err ... end` captures stderr from the `try` block and binds it to `err` in the catch block.
- Catch blocks run only when the try block exits non-zero.

### Fail-fast unsupported features

The compiler fails fast for features not ready for stable transpilation:

- range value code generation (`let r = 0..5` as a value)
- `sh ...` in expression context (it is statement-only)

### Codegen behavior

- Transpilation target is Bash.
- If codegen still encounters unsupported AST constructs, the CLI exits non-zero and reports unsupported items.
