## Compiler Architecture (Current)

### Pipeline

1. Parse source with ANTLR4 grammar (`Brash.g4`).
2. Build AST (`Frontend/AstBuilder.cs`).
3. Run semantic analysis (`Semantic/SemanticAnalyzer.cs`):
   - declaration collection
   - impl/method analysis
   - type/symbol/nullability/mutability checks
4. Generate Bash (`CodeGen/BashGenerator*.cs`).

### Semantic subsystems

- `TypeChecker`: type compatibility, operators, function/method arg checks.
- `SymbolResolver`: expression type resolution and symbol lookup.
- `NullabilityChecker`: nullable safety diagnostics.
- `MutabilityChecker`: assignment mutability checks (`let mut`, mutable params).
- `PipeChecker`: enforces command pipe typing.

### Command model

- `cmd(...)` builds command values.
- `exec(...)` materializes stdout.
- `spawn(...)` creates process handles.
- `async exec(...)` is fire-and-forget background execution.
- `async spawn(...)` creates async process handles.
- `await process` waits and materializes captured stdout.
- Command pipelines are lazy values until materialized with `exec(...)`.
- `sh ...` executes raw shell text in statement context.

### Runtime helpers emitted in Bash

- struct/method helpers (`brash_get_field`, `brash_set_field`, `brash_call_method`)
- map/index helpers (`brash_map_new`, `brash_map_set`, `brash_map_get`, `brash_map_literal`, `brash_index_get`, `brash_index_set`)
- command/process helpers (`brash_exec_cmd`, `brash_async_spawn_cmd`, `brash_await`)
- console input helper (`brash_readln`)

### Failure policy

- Unsupported language/runtime features should fail with clear diagnostics.
- CLI treats remaining codegen unsupported warnings as fatal when emitting Bash.
