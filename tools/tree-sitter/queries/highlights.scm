; Keywords
[
  "let"
  "mut"
  "const"
  "fn"
  "async"
  "await"
  "spawn"
  "struct"
  "enum"
  "impl"
  "if"
  "elif"
  "else"
  "for"
  "while"
  "in"
  "step"
  "try"
  "catch"
  "throw"
  "import"
  "from"
  "return"
  "end"
] @keyword

[
  "self"
  "null"
] @constant.builtin

[
  "cmd"
  "exec"
] @function.builtin

; Builtin types
[
  "int"
  "float"
  "string"
  "bool"
  "char"
  "map"
  "void"
] @type.builtin

; Declarations
(function_declaration name: (identifier) @function)
(method_declaration name: (identifier) @method)
(struct_declaration name: (identifier) @type)
(enum_declaration name: (identifier) @type)
(enum_variant name: (identifier) @constant)
(cast_expression type: (type) @type)
(break_statement) @keyword
(continue_statement) @keyword

; Identifiers by role
(parameter name: (identifier) @parameter)
(field_declaration name: (identifier) @property)
(field_assignment name: (identifier) @property)
(member_access member: (identifier) @property)
(safe_navigation_expression member: (identifier) @property)
(method_call_expression method: (identifier) @function.method.call)
(function_call name: (identifier) @function.call)
(import_specifier default: (identifier) @namespace)
(import_specifier (identifier) @namespace)
(identifier) @variable

; Literals
(boolean) @boolean
(integer) @number
(float) @number.float
(string) @string
(interpolated_string) @string.special
(multiline_string) @string
(char) @character

; Operators / punctuation
[
  "="
  "=="
  "!="
  "<"
  ">"
  "<="
  ">="
  "+"
  "-"
  "*"
  "/"
  "%"
  "!"
  "&&"
  "||"
  "??"
  ".."
  "|"
  "."
  "?."
] @operator

[
  "("
  ")"
  "{"
  "}"
  "["
  "]"
  ","
  ":"
] @punctuation.delimiter

; Comments
(line_comment) @comment.line
(block_comment) @comment.block

; Preprocessor
(shebang_directive) @keyword.directive
(preprocessor_directive) @keyword.directive
