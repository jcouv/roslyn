

**Option 1**

Treat deconstruction of a tuple into existing variables as a kind of assignment.

Treat deconstruction of a tuple into new variables as a new kind of node (AssignmentExpression). 
It would pick up the behavior of each contexts where new variables can be declared (TODO: need to list). For instance, in LINQ, new variables go into a transparent identifiers.
It is seen as deconstructing into separate variables (we don't introduce transparent identifiers in contexts where they didn't exist previously).

Deconstruction-assignment (deconstruction into into exising variables):
No syntax change.
One kind of unary_expression is tuple_literal.

- Static semantic: The LHS of the an assignment-expression used be a L-value, but now it can be L-value -- which uses existing rules -- or tuple_literal. The new rules for tuple_literal on the LHS...
- Dynamic semantic

- This should work even if `System.ValueTuple` is not present.
- This will create a new bound node, with the list of L-values to be assigned to, the node on the right and its `Deconstruct` member, and conversions?
- How is the Deconstruct method resolved? (there could be multiple candidates)
- Do the names matter? `int x, y; (a: x, b: y) = M();`

(note: assignment should be assignment_expression in C# spec)

Deconstruction-declaration (deconstruction into new variables):

```ANTLR
declaration_statement
    : local_variable_declaration ';'
    | local_constant_declaration ';'
    | local_variable_combo_declaration ';'  // new
    ;

local_variable_combo_declaration
    : local_variable_combo_declaration_lhs '=' expression
    
local_variable_combo_declaration_lhs
    : 'var' '(' identifier_list ')'
    | '(' local_variable_list ')'
    ;
    
identifier_list
    : identifier ',' identifier
    | identifier_list ',' identifier
    ;

local_variable_list
    : local_variable_type identifier ',' local_variable_type identifier
    | local_variable_list ',' local_variable_type identifier
    ;
    
foreach_statement
    : 'foreach' '(' local_variable_type identifier 'in' expression ')' embedded_statement
    | 'foreach' '(' local_variable_combo_declaration_lhs 'in' expression ')' embedded_statement // new
    ;
    
for_initializer
    : local_variable_declaration
    | local_variable_combo_declaration // new
    | statement_expression_list
    ;

let_clause
    : 'let' identifier '=' expression
    | 'let' '(' identifier_list ')' '=' expression // new
    ;
    
from_clause // not sure
    : 'from' type? identifier 'in' expression
    ;
    
join_clause // not sure
    : 'join' type? identifier 'in' expression 'on' expression 'equals' expression
    ;

join_into_clause // not sure
    : 'join' type? identifier 'in' expression 'on' expression 'equals' expression 'into' identifier
    ;

constant_declarator // not sure
    : identifier '=' constant_expression
    ;
```

Should we allow this?
`var t = (x: 1, y: 2);    (x: var a, y: var b) = t;`
or `var (x: a, y: b) = t;`
(if not, tuple names aren't very useful?)

Add example: var (x, y) = 
Semantic (cardinality should match, ordering including conversion, 
What are the type rules? `(string s, int x) = (null, 3);`

Deconstruction for `System.ValueTuple`, `System.Tuple` and any other type involves a call to `Deconstruct`.

**Option 2**

Treat deconstruction of a tuple into both existing or new variables as a new kind of node (TODO: more details).


**Implications**

| Option 1 | Option 2 | Code | Notes |
| -------- | -------- | ---- | ----- |
| Not allowed | Succeeds | `int x;   (x, int y) = (0, 0);` | |
| Succeeds | Succeeds | `void M(out (int x, int y) v) { ... }    M(out (int x, int y));` | |

