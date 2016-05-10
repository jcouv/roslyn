
For some background, there are different kinds of syntax:

* Statement
* Expression
* Declarator

Existing contexts where a variable can be defined:

* For
* LINQ
* 

Two major options to start from:

**Option 1**

Treat deconstruction of a tuple into existing variables as a kind of assignment.

Treat deconstruction of a tuple into new variables as a new kind of node (TODO: is it assignment or expression?). 
It would pick up the behavior of each contexts where new variables can be declared (TODO: need to list). For instance, in LINQ, new variables go into a transparent identifiers.
It is seen as deconstructing into separate variables (we don't introduce transparent identifiers in contexts where they didn't exist previously).

Deconstruction-assignment (deconstruction into into exising variables):

```ANTLR
assignment
    : unary_expression assignment_operator expression
    | tuple_expression assignment_operator expression
    ;

tuple_expression
    : '(' tuple_expression_element_list ')'
    ;

tuple_expression_element_list
    : unary_expression
    : tuple_expression_element_list ',' unary_expression
    ;
```

Deconstruction-declaration (deconstruction into new variables):

```ANTLR
declaration_statement
    : local_variable_declaration ';'
    | local_constant_declaration ';'
    | local_variable_combo_declaration ';'
    ;


local_variable_combo_declaration
    : local_variable_combo_declaration_lhs
    : local_variable_combo_declaration_lhs '=' expression
    
local_variable_combo_declaration_lhs
    : 'var' '(' identifier_list ')'
    | '(' local_variable_list ')'
    ;
    
identifier_list
    : identifier
    | identifier_list ',' identifier
    ;

local_variable_list
    : local_variable_type identifier
    | local_variable_list ',' local_variable_type identifier
    ;
```

**Option 2**

Treat deconstruction of a tuple into both existing or new variables as a new kind of node (TODO: more details).

**Other dimensions**

1. Whether all existing contexts that allow declaring new variables should also allow deconstruction of tuples into new variables. (I'm assuming yes)
2. Whether deconstruction for `System.ValueTuple` and `System.Tuple` involves a call to `Deconstruct`. (I'm assuming yes)

**Implications**

| Option 1 | Option 2 | Code | Notes |
| -------- | -------- | ---- | ----- |
| Not allowed | Succeeds | `int x;   (x, int y) = (0, 0);` | |
| Succeeds | Succeeds | `void M(out (int x, int y) v) { ... }    M(out (int x, int y));` | |

