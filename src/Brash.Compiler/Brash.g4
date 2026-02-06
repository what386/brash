grammar Brash;

// ============================================
// PARSER RULES
// ============================================

program
    : preprocessorDirective* statement* EOF
    ;

// Preprocessor Directives
preprocessorDirective
    : '#define' IDENTIFIER expression
    | '#undef' IDENTIFIER
    | '#if' expression preprocessorBlock
    | '#ifdef' IDENTIFIER preprocessorBlock
    | '#ifndef' IDENTIFIER preprocessorBlock
    ;

preprocessorBlock
    : statement* ('#else' statement*)? '#endif'
    ;

// Statements
statement
    : variableDeclaration
    | assignment
    | functionDeclaration
    | structDeclaration
    | recordDeclaration
    | enumDeclaration
    | implBlock
    | ifStatement
    | forLoop
    | whileLoop
    | tryStatement
    | importStatement
    | returnStatement
    | throwStatement
    | breakStatement
    | continueStatement
    | expressionStatement
    ;

// Variable Declarations
variableDeclaration
    : ('let' | 'mut' | 'const') IDENTIFIER (':' type)? '=' expression
    ;

assignment
    : (IDENTIFIER | memberAccess | indexAccess) '=' expression
    ;

// Function Declaration
functionDeclaration
    : 'async'? 'fn' IDENTIFIER '(' parameterList? ')' (':' returnType)? functionBody
    ;

parameterList
    : parameter (',' parameter)*
    ;

parameter
    : IDENTIFIER ':' type ('=' expression)?
    ;

returnType
    : type
    | tupleType
    | 'void'
    ;

tupleType
    : '(' type (',' type)+ ')'
    ;

functionBody
    : statement* 'end'
    ;

// Struct and Record Declarations
structDeclaration
    : 'struct' IDENTIFIER structBody
    ;

recordDeclaration
    : 'record' IDENTIFIER structBody
    ;

enumDeclaration
    : 'enum' IDENTIFIER enumBody
    ;

enumBody
    : enumVariant (',' enumVariant)* ','? 'end'
    ;

enumVariant
    : IDENTIFIER ('(' type (',' type)* ')')?
    ;

structBody
    : fieldDeclaration* 'end'
    ;

fieldDeclaration
    : IDENTIFIER ':' type
    ;

// Implementation Block
implBlock
    : 'impl' IDENTIFIER methodDeclaration* 'end'
    ;

methodDeclaration
    : 'fn' IDENTIFIER '(' parameterList? ')' (':' returnType)? functionBody
    ;

// Control Flow
ifStatement
    : 'if' expression statement* elifClause* elseClause? 'end'
    ;

elifClause
    : 'elif' expression statement*
    ;

elseClause
    : 'else' statement*
    ;

forLoop
    : 'for' '+' ? '-' ? IDENTIFIER 'in' expression ('step' expression)? statement* 'end'
    ;

whileLoop
    : 'while' expression statement* 'end'
    ;

// Error Handling
tryStatement
    : 'try' statement* 'catch' IDENTIFIER statement* 'end'
    ;

throwStatement
    : 'throw' expression
    ;

// Import Statement
importStatement
    : 'import' (stringLiteral | importSpecifier)
    ;

importSpecifier
    : '{' IDENTIFIER (',' IDENTIFIER)* '}' 'from' stringLiteral
    | IDENTIFIER 'from' stringLiteral
    ;

// Return and Control
returnStatement
    : 'return' expression?
    ;

breakStatement
    : 'break'
    ;

continueStatement
    : 'continue'
    ;

expressionStatement
    : expression
    ;

// Expressions
expression
    : primaryExpression                                          # PrimaryExpr
    | 'await' expression                                         # AwaitExpr
    | expression '|' expression                                  # PipeExpr
    | expression ('.' IDENTIFIER '(' argumentList? ')')          # MethodCallExpr
    | expression '.' IDENTIFIER                                  # MemberAccessExpr
    | expression '[' expression ']'                              # IndexAccessExpr
    | IDENTIFIER '(' argumentList? ')'                           # FunctionCallExpr
    | 'cmd' '(' argumentList ')' ('.' IDENTIFIER '(' ')')?       # CommandExpr
    | 'exec' '(' argumentList ')'                                # ExecExpr
    | 'async' '(' argumentList ')'                               # AsyncExpr
    | ('!' | '-' | '+') expression                               # UnaryExpr
    | expression ('*' | '/' | '%') expression                    # MultiplicativeExpr
    | expression ('+' | '-') expression                          # AdditiveExpr
    | expression ('..' ) expression                              # RangeExpr
    | expression ('==' | '!=' | '<' | '>' | '<=' | '>=') expression # ComparisonExpr
    | expression ('&&' | '||') expression                        # LogicalExpr
    | expression '??' expression                                 # NullCoalesceExpr
    | expression '?.' IDENTIFIER                                 # SafeNavigationExpr
    ;

primaryExpression
    : literal
    | IDENTIFIER
    | tupleExpression
    | arrayLiteral
    | mapLiteral
    | structLiteral
    | '(' expression ')'
    | 'self'
    | 'null'
    ;

tupleExpression
    : '(' expression ',' expression (',' expression)* ')'
    ;

arrayLiteral
    : '[' (expression (',' expression)*)? ']'
    ;

mapLiteral
    : '{' (mapEntry (',' mapEntry)*)? '}'
    ;

mapEntry
    : expression ':' expression
    ;

structLiteral
    : IDENTIFIER '{' (fieldAssignment (',' fieldAssignment)*)? '}'
    ;

fieldAssignment
    : IDENTIFIER ':' expression
    ;

argumentList
    : expression (',' expression)*
    ;

memberAccess
    : expression '.' IDENTIFIER
    ;

indexAccess
    : expression '[' expression ']'
    ;

// Types
type
    : baseType typeSuffix*
    ;

baseType
    : primitiveType
    | mapType
    | IDENTIFIER
    ;

typeSuffix
    : '[' ']'      // array
    | '?'          // nullable
    ;

primitiveType
    : 'int'
    | 'float'
    | 'string'
    | 'bool'
    | 'char'
    ;

mapType
    : 'map' '<' type ',' type '>'
    ;

// Literals
literal
    : INTEGER
    | FLOAT
    | stringLiteral
    | CHAR
    | BOOLEAN
    ;

stringLiteral
    : STRING
    | INTERPOLATED_STRING
    | MULTILINE_STRING
    ;

// ============================================
// LEXER RULES
// ============================================

// Keywords
ENUM        : 'enum';
LET         : 'let';
MUT         : 'mut';
CONST       : 'const';
FN          : 'fn';
ASYNC       : 'async';
AWAIT       : 'await';
STRUCT      : 'struct';
RECORD      : 'record';
IMPL        : 'impl';
IF          : 'if';
ELIF        : 'elif';
ELSE        : 'else';
FOR         : 'for';
WHILE       : 'while';
IN          : 'in';
STEP        : 'step';
BREAK       : 'break';
CONTINUE    : 'continue';
RETURN      : 'return';
TRY         : 'try';
CATCH       : 'catch';
THROW       : 'throw';
IMPORT      : 'import';
FROM        : 'from';
END         : 'end';
SELF        : 'self';
NULL        : 'null';
EXEC        : 'exec';
CMD         : 'cmd';
VOID        : 'void';

// Type Keywords
INT         : 'int';
FLOAT_TYPE  : 'float';
STRING_TYPE : 'string';
BOOL_TYPE   : 'bool';
CHAR_TYPE   : 'char';
MAP         : 'map';

// Boolean Literals
BOOLEAN
    : 'true'
    | 'false'
    ;

// Identifiers
IDENTIFIER
    : [a-zA-Z_][a-zA-Z0-9_]*
    ;

// Numeric Literals
INTEGER
    : [0-9]+
    ;

FLOAT
    : [0-9]+ '.' [0-9]+
    | '.' [0-9]+
    ;

// String Literals
STRING
    : '"' (~["\\\r\n] | '\\' .)* '"'
    ;

INTERPOLATED_STRING
    : '$"' ( ~["{\\] | '\\' . | '{' ~[}]* '}' )* '"'
    ;

MULTILINE_STRING
    : '[[' .*? ']]'
    ;

// Character Literal
CHAR
    : '\'' (~['\\\r\n] | '\\' .) '\''
    ;

// Operators
PLUS        : '+';
MINUS       : '-';
STAR        : '*';
SLASH       : '/';
PERCENT     : '%';
ASSIGN      : '=';
EQ          : '==';
NEQ         : '!=';
LT          : '<';
GT          : '>';
LE          : '<=';
GE          : '>=';
AND         : '&&';
OR          : '||';
NOT         : '!';
QUESTION    : '?';
COALESCE    : '??';
PIPE        : '|';
DOT         : '.';
COMMA       : ',';
COLON       : ':';
SEMICOLON   : ';';
RANGE       : '..';
SAFE_NAV    : '?.';

// Delimiters
LPAREN      : '(';
RPAREN      : ')';
LBRACE      : '{';
RBRACE      : '}';
LBRACK      : '[';
RBRACK      : ']';

// Preprocessor
HASH        : '#';

// Whitespace and Comments
WS
    : [ \t\r\n]+ -> skip
    ;

LINE_COMMENT
    : '//' ~[\r\n]* -> skip
    ;

BLOCK_COMMENT
    : '/*' .*? '*/' -> skip
    ;
