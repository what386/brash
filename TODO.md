# TODO — brash v0.1.0

@created: 2026-02-06
@modified: 2026-02-07

## Tasks

- [ ] Parser: implement full AST coverage for all grammar expression/statement variants (method call, member/index access, pipe, await, command forms, literals) (high) #parser #ast #feature
      @created 2026-02-06 23:08

- [ ] Semantic: complete type inference and nullability flow analysis across variables, calls, collections, and control-flow branches (high) #semantic #types #feature
      @created 2026-02-06 23:08

- [ ] Semantic: enforce mutability, scope, and self/impl method rules (including strict variable/parameter mutability and loop/function context checks) (high) #semantic #scope #feature
      @created 2026-02-06 23:08

- [ ] Modules: implement import resolution and multi-file symbol linking for module, named, and default imports (high) #modules #frontend #feature
      @created 2026-02-06 23:08

- [ ] Runtime: define and implement standard library contract used by generated Bash (Process, cmd/exec helpers, collection helpers) (high) #runtime #stdlib #feature
      @created 2026-02-06 23:08

- [ ] Testing: add end-to-end golden tests that compile each examples/\*.bsh file and assert diagnostics/codegen snapshots (high) #testing #e2e #examples #quality
      @created 2026-02-06 23:09

- [ ] Testing: add runtime integration tests that execute generated Bash for representative examples and validate outputs/exit codes #testing #integration #runtime #quality
      @created 2026-02-06 23:09

- [ ] Release 0.1.0: define supported language surface and fail-fast diagnostics for unsupported features (high) #release #spec #quality
      @created 2026-02-07 02:06

- [ ] Codegen: implement try/catch/throw lowering or enforce compile-time errors in transpile mode (high) #codegen #bash #error-handling #feature
      @created 2026-02-07 02:06

- [ ] Testing: add golden snapshot tests for generated Bash across selected examples/* (high) #testing #e2e #examples #quality
      @created 2026-02-07 02:06

- [ ] Semantic: resolve nullable warnings and tighten method/self nullability/type checks in SymbolResolver #semantic #types #quality
      @created 2026-02-07 02:06

- [ ] Runtime: define Process handle contract for spawn (pid, wait/exit semantics) and add Bash helper coverage (high) #runtime #process #bash #feature
      @created 2026-02-07 02:07

