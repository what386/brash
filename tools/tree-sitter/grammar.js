/**
 * Tree-sitter grammar for Brash.
 * Mirrors the current ANTLR grammar in src/Brash.Compiler/Brash.g4.
 */

const PREC = {
  PIPE: 1,
  COALESCE: 2,
  OR: 3,
  AND: 4,
  COMPARE: 5,
  CAST: 6,
  RANGE: 7,
  ADD: 8,
  MUL: 9,
  UNARY: 10,
  CALL: 11,
  MEMBER: 12,
};

module.exports = grammar({
  name: "brash",

  extras: ($) => [/\s/, $.line_comment, $.block_comment],

  word: ($) => $.identifier,

  rules: {
    source_file: ($) =>
      seq(optional($.shebang_directive), repeat(choice($.preprocessor_directive, $.statement))),

    shebang_directive: (_) => token(seq("#!", /.*/)),

    preprocessor_directive: ($) =>
      choice(
        seq("#define", field("name", $.identifier), field("value", $.expression)),
        seq("#undef", field("name", $.identifier)),
        seq("#if", field("condition", $.expression), $.preprocessor_block),
        seq("#ifdef", field("name", $.identifier), $.preprocessor_block),
        seq("#ifndef", field("name", $.identifier), $.preprocessor_block)
      ),

    preprocessor_block: ($) =>
      seq(
        repeat($.statement),
        optional(seq("#else", repeat($.statement))),
        "#endif"
      ),

    statement: ($) =>
      choice(
        $.variable_declaration,
        $.assignment,
        $.function_declaration,
        $.struct_declaration,
        $.enum_declaration,
        $.impl_block,
        $.if_statement,
        $.for_loop,
        $.while_loop,
        $.try_statement,
        $.import_statement,
        $.sh_statement,
        $.return_statement,
        $.throw_statement,
        $.break_statement,
        $.continue_statement,
        $.expression_statement
      ),

    sh_statement: (_) => token(seq("sh", /[ \t]+/, /[^\r\n]+/)),

    variable_declaration: ($) =>
      choice(
        seq(
          optional("pub"),
          choice(seq("let", optional("mut")), "const"),
          field("name", $.identifier),
          optional(seq(":", field("type", $.type))),
          "=",
          field("value", $.expression)
        ),
        seq("let", field("binding", $.tuple_binding), "=", field("value", $.expression))
      ),

    tuple_binding: ($) =>
      seq(
        "(",
        field("first", $.tuple_binding_element),
        repeat1(seq(",", field("rest", $.tuple_binding_element))),
        ")"
      ),

    tuple_binding_element: ($) => seq(optional("mut"), field("name", $.identifier)),

    assignment: ($) =>
      seq(choice($.identifier, $.member_access, $.index_access), "=", field("value", $.expression)),

    function_declaration: ($) =>
      seq(
        optional("pub"),
        optional("async"),
        "fn",
        field("name", $.identifier),
        "(",
        optional($.parameter_list),
        ")",
        optional(seq(":", field("return_type", $.return_type))),
        $.function_body
      ),

    parameter_list: ($) => seq($.parameter, repeat(seq(",", $.parameter))),

    parameter: ($) =>
      seq(
        optional("mut"),
        field("name", $.identifier),
        ":",
        field("type", $.type),
        optional(seq("=", field("default_value", $.expression)))
      ),

    return_type: ($) => choice($.type, $.tuple_type, "void"),

    tuple_type: ($) =>
      seq("(", field("first", $.type), ",", field("second", $.type), repeat(seq(",", $.type)), ")"),

    function_body: ($) => seq(repeat($.statement), "end"),

    struct_declaration: ($) =>
      seq(optional("pub"), "struct", field("name", $.identifier), repeat($.field_declaration), "end"),

    enum_declaration: ($) =>
      seq(optional("pub"), "enum", field("name", $.identifier), $.enum_body),

    enum_body: ($) =>
      seq(
        field("first", $.enum_variant),
        repeat(seq(",", $.enum_variant)),
        optional(","),
        "end"
      ),

    enum_variant: ($) => field("name", $.identifier),

    field_declaration: ($) => seq(field("name", $.identifier), ":", field("type", $.type)),

    impl_block: ($) => seq("impl", field("type_name", $.identifier), repeat($.method_declaration), "end"),

    method_declaration: ($) =>
      seq(
        optional("pub"),
        optional("static"),
        "fn",
        field("name", $.identifier),
        "(",
        optional($.parameter_list),
        ")",
        optional(seq(":", field("return_type", $.return_type))),
        $.function_body
      ),

    if_statement: ($) =>
      seq(
        "if",
        field("condition", $.expression),
        repeat($.statement),
        repeat($.elif_clause),
        optional($.else_clause),
        "end"
      ),

    elif_clause: ($) => seq("elif", field("condition", $.expression), repeat($.statement)),

    else_clause: ($) => seq("else", repeat($.statement)),

    for_loop: ($) =>
      seq(
        "for",
        optional("+"),
        optional("-"),
        field("variable", $.identifier),
        "in",
        field("range", $.expression),
        optional(seq("step", field("step", $.expression))),
        repeat($.statement),
        "end"
      ),

    while_loop: ($) => seq("while", field("condition", $.expression), repeat($.statement), "end"),

    try_statement: ($) =>
      seq(
        "try",
        repeat($.statement),
        "catch",
        field("error_variable", $.identifier),
        repeat($.statement),
        "end"
      ),

    throw_statement: ($) => seq("throw", field("value", $.expression)),

    import_statement: ($) =>
      seq("import", choice(field("module", $.string_literal), $.import_specifier)),

    import_specifier: ($) =>
      choice(
        seq(
          "{",
          field("first", $.identifier),
          repeat(seq(",", $.identifier)),
          "}",
          "from",
          field("module", $.string_literal)
        ),
        seq(field("default", $.identifier), "from", field("module", $.string_literal))
      ),

    return_statement: ($) => prec.right(seq("return", optional(field("value", $.expression)))),
    break_statement: (_) => "break",
    continue_statement: (_) => "continue",
    expression_statement: ($) => $.expression,

    expression: ($) =>
      choice(
        $.await_expression,
        $.cast_expression,
        $.pipe_expression,
        $.null_coalesce_expression,
        $.logical_expression,
        $.comparison_expression,
        $.range_expression,
        $.additive_expression,
        $.multiplicative_expression,
        $.unary_expression,
        $.method_call_expression,
        $.member_access,
        $.safe_navigation_expression,
        $.index_access,
        $.command_expression,
        $.exec_expression,
        $.async_exec_expression,
        $.async_spawn_expression,
        $.spawn_expression,
        $.primary_expression
      ),

    await_expression: ($) => prec.right(seq("await", $.expression)),

    cast_expression: ($) =>
      prec.left(PREC.CAST, seq(field("value", $.expression), "as", field("type", $.type))),

    pipe_expression: ($) =>
      prec.left(PREC.PIPE, seq(field("left", $.expression), "|", field("right", $.expression))),

    method_call_expression: ($) =>
      prec.left(
        PREC.CALL,
        seq(
          field("object", $.expression),
          ".",
          field("method", $.identifier),
          "(",
          optional($.argument_list),
          ")"
        )
      ),

    member_access: ($) =>
      prec.left(PREC.MEMBER, seq(field("object", $.expression), ".", field("member", $.identifier))),

    index_access: ($) =>
      prec.left(PREC.MEMBER, seq(field("object", $.expression), "[", field("index", $.expression), "]")),

    command_expression: ($) => seq("cmd", "(", optional($.argument_list), ")"),
    exec_expression: ($) => seq("exec", "(", optional($.argument_list), ")"),
    async_exec_expression: ($) => seq("async", "exec", "(", optional($.argument_list), ")"),
    async_spawn_expression: ($) => seq("async", "spawn", "(", optional($.argument_list), ")"),
    spawn_expression: ($) => seq("spawn", "(", optional($.argument_list), ")"),

    unary_expression: ($) =>
      prec.right(PREC.UNARY, seq(choice("!", "-", "+"), field("operand", $.expression))),

    multiplicative_expression: ($) =>
      prec.left(
        PREC.MUL,
        seq(field("left", $.expression), field("operator", choice("*", "/", "%")), field("right", $.expression))
      ),

    additive_expression: ($) =>
      prec.left(
        PREC.ADD,
        seq(field("left", $.expression), field("operator", choice("+", "-")), field("right", $.expression))
      ),

    range_expression: ($) =>
      prec.left(PREC.RANGE, seq(field("start", $.expression), "..", field("end", $.expression))),

    comparison_expression: ($) =>
      prec.left(
        PREC.COMPARE,
        seq(
          field("left", $.expression),
          field("operator", choice("==", "!=", "<", ">", "<=", ">=")),
          field("right", $.expression)
        )
      ),

    logical_expression: ($) =>
      prec.left(
        PREC.OR,
        seq(field("left", $.expression), field("operator", choice("&&", "||")), field("right", $.expression))
      ),

    null_coalesce_expression: ($) =>
      prec.left(PREC.COALESCE, seq(field("left", $.expression), "??", field("right", $.expression))),

    safe_navigation_expression: ($) =>
      prec.left(PREC.MEMBER, seq(field("object", $.expression), "?.", field("member", $.identifier))),

    primary_expression: ($) =>
      choice(
        $.literal,
        $.function_call,
        $.struct_literal,
        $.identifier,
        $.tuple_expression,
        $.array_literal,
        $.map_literal,
        seq("(", $.expression, ")"),
        "self",
        "null"
      ),

    tuple_expression: ($) =>
      seq("(", field("first", $.expression), ",", field("second", $.expression), repeat(seq(",", $.expression)), ")"),

    array_literal: ($) => seq("[", optional(seq($.expression, repeat(seq(",", $.expression)))), "]"),

    map_literal: ($) => seq("{", optional(seq($.map_entry, repeat(seq(",", $.map_entry)))), "}"),

    map_entry: ($) => seq(field("key", $.expression), ":", field("value", $.expression)),

    struct_literal: ($) =>
      prec(
        PREC.CALL,
        seq(
          field("type_name", $.identifier),
          "{",
          optional(seq($.field_assignment, repeat(seq(",", $.field_assignment)))),
          "}"
        )
      ),

    field_assignment: ($) => seq(field("name", $.identifier), ":", field("value", $.expression)),

    argument_list: ($) => seq($.expression, repeat(seq(",", $.expression))),
    function_call: ($) =>
      prec(
        PREC.CALL,
        seq(field("name", $.identifier), "(", optional($.argument_list), ")")
      ),

    type: ($) => prec.right(seq($.base_type, repeat($.type_suffix))),

    base_type: ($) => choice($.primitive_type, $.map_type, $.identifier),
    type_suffix: (_) => choice(seq("[", "]"), "?"),
    primitive_type: (_) => choice("int", "float", "string", "bool", "char", "any"),
    map_type: ($) => seq("map", "<", field("key_type", $.type), ",", field("value_type", $.type), ">"),

    literal: ($) => choice($.integer, $.float, $.string_literal, $.char, $.boolean),
    string_literal: ($) => choice($.string, $.interpolated_string, $.multiline_string),

    boolean: (_) => choice("true", "false"),
    identifier: (_) => /[a-zA-Z_][a-zA-Z0-9_]*/,
    integer: (_) => /[0-9]+/,
    float: (_) => /([0-9]+\.[0-9]+)|(\.[0-9]+)/,
    string: (_) => /"([^"\\\n\r]|\\.)*"/,
    // Keep this intentionally broad for parser generation compatibility.
    // Precise interpolation validation belongs in compiler semantic checks.
    interpolated_string: (_) => token(seq('$"', /([^"\\\n\r]|\\.)*/, '"')),
    multiline_string: (_) => token(/\[\[[\s\S]*?\]\]/),
    char: (_) => /'([^'\\\n\r]|\\.)'/,

    line_comment: (_) => token(seq("//", /.*/)),
    block_comment: (_) =>
      seq(
        "/*",
        repeat(choice(/[^*]/, /\*+[^/*]/)),
        /\*+\//
      ),
  },
});
