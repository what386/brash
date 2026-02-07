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
  "break"
  "continue"
  "end"
  "self"
  "null"
  "cmd"
  "exec"
] @keyword

; Types
[
  "int"
  "float"
  "string"
  "bool"
  "char"
  "map"
  "void"
] @type.builtin

; Literals
(boolean) @constant.builtin
(integer) @number
(float) @number.float
(string) @string
(interpolated_string) @string.special
(multiline_string) @string
(char) @string.escape

; Identifiers
(identifier) @variable
(function_declaration name: (identifier) @function)
(method_declaration name: (identifier) @method)
(function_call name: (identifier) @function.call)
(enum_declaration name: (identifier) @type)
(struct_declaration name: (identifier) @type)

; Comments
(line_comment) @comment
(block_comment) @comment

; Preprocessor
(shebang_directive) @keyword.directive
(preprocessor_directive) @keyword.directive

