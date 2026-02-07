## Compiler Architecture (Current)

### Pipeline

1. Parse source with ANTLR4 grammar (`Brash.g4`).
2. Build AST (`Frontend/AstBuilder.cs`).
3. Run semantic analysis (`Semantic/SemanticAnalyzer.cs`):
   - declaration collection
   - impl/method analysis
   - type/symbol/nullability/mutability checks
   - transpile-readiness checks for unsupported features
4. Generate Bash (`CodeGen/BashGenerator*.cs`).

### Semantic subsystems

- `TypeChecker`: type compatibility, operators, function/method arg checks.
- `SymbolResolver`: expression type resolution and symbol lookup.
- `NullabilityChecker`: nullable safety diagnostics.
- `MutabilityChecker`: assignment mutability checks (`let mut`, mutable params).
- `PipeChecker`: enforces command pipe typing.
- `TranspileReadinessChecker`: fail-fast diagnostics for unsupported constructs.

### Command model

- `cmd(...)` builds command values.
- `exec(...)` materializes stdout.
- `spawn(...)` creates process handles.
- Command pipelines are lazy values until materialized with `exec(...)`.

### Runtime helpers emitted in Bash

- `brash_build_cmd`
- `brash_pipe_cmd`
- `brash_exec_cmd`
- `brash_spawn_cmd`
- struct/method helpers (`brash_get_field`, `brash_set_field`, `brash_call_method`)

### Failure policy

- Unsupported language/runtime features should fail in semantic analysis with clear errors (for example `import`, async/await flow, and unsupported literal/value forms).
- CLI also treats remaining codegen unsupported warnings as fatal when emitting Bash.
