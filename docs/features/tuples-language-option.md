
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

```ANTLR
assignment
    : unary_expression assignment_operator expression
    ;
    
unary_expression
    : primary_expression
    | null_conditional_expression
    | '+' unary_expression
    | '-' unary_expression
    | '!' unary_expression
    | '~' unary_expression
    | pre_increment_expression
    | pre_decrement_expression
    | cast_expression
    | await_expression
    | unary_expression_unsafe
    ;

primary_expression
    : primary_no_array_creation_expression
    | array_creation_expression
    ;

primary_no_array_creation_expression
    : literal
    | interpolated_string
    | simple_name
    | parenthesized_expression
    | member_access
    | invocation_expression
    | element_access
    | this_access
    | base_access
    | post_increment_expression
    | post_decrement_expression
    | object_creation_expression
    | delegate_creation_expression
    | anonymous_object_creation_expression
    | typeof_expression
    | checked_expression
    | unchecked_expression
    | default_value_expression
    | nameof_expression
    | anonymous_method_expression
    | primary_no_array_creation_expression_unsafe
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

