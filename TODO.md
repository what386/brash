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

- [ ] Codegen: implement command execution, pipe operator lowering, async/await flow, and error handling constructs (high) #codegen #bash #shell #feature
      @created 2026-02-06 23:08

- [ ] Runtime: define and implement standard library contract used by generated Bash (Process, cmd/exec helpers, collection helpers) (high) #runtime #stdlib #feature
      @created 2026-02-06 23:08

- [ ] Testing: add end-to-end golden tests that compile each examples/\*.bsh file and assert diagnostics/codegen snapshots (high) #testing #e2e #examples #quality
      @created 2026-02-06 23:09

- [ ] Testing: add runtime integration tests that execute generated Bash for representative examples and validate outputs/exit codes #testing #integration #runtime #quality
      @created 2026-02-06 23:09

- [ ] Tooling: add CI workflow to run build + compiler tests + example progress suite on every push #infra #ci #quality
      @created 2026-02-06 23:09

- [ ] Docs: align language spec with implemented behavior and mark unsupported example features explicitly #docs #spec #project
      @created 2026-02-06 23:09

- [ ] Release 0.1.0: define supported language surface and fail-fast diagnostics for unsupported features (high) #release #spec #quality
      @created 2026-02-07 02:06

- [ ] Codegen: implement try/catch/throw lowering or enforce compile-time errors in transpile mode (high) #codegen #bash #error-handling #feature
      @created 2026-02-07 02:06

- [ ] Testing: add golden snapshot tests for generated Bash across selected examples/* (high) #testing #e2e #examples #quality
      @created 2026-02-07 02:06


## Completed

- [x] astbuilder: add more expr types
      @created 2026-02-06 01:48
      @completed 2026-02-06 23:20

- [x] Language: switch mut to modifier syntax ( + mutable parameters) and enforce parameter mutability with a dedicated MutabilityChecker (high) #language #semantic #parser #feature
      @created 2026-02-06 23:23
      @completed 2026-02-06 23:27

- [x] Parser: fix expression precedence so function calls like f(x) resolve as FunctionCallExpression instead of IdentifierExpression (high) #parser #ast #bug
      @created 2026-02-07 00:53
      @completed 2026-02-07 00:59

- [x] Codegen: generate Bash for structs/enums, method dispatch, and field/member access semantics (high) #codegen #bash #feature
      @created 2026-02-06 23:08
      @completed 2026-02-07 02:06

- [x] CLI: wire full compile pipeline (parse -> AST -> semantic -> codegen -> output file) with stable diagnostics and exit codes (high) #cli #compiler #feature
      @created 2026-02-06 23:09
      @completed 2026-02-07 02:06

