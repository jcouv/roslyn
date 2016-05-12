

**Option 1**

Treat deconstruction of a tuple into existing variables as a kind of assignment.

Treat deconstruction of a tuple into new variables as a new kind of node (AssignmentExpression). 
It would pick up the behavior of each contexts where new variables can be declared (TODO: need to list). For instance, in LINQ, new variables go into a transparent identifiers.
It is seen as deconstructing into separate variables (we don't introduce transparent identifiers in contexts where they didn't exist previously).

###Deconstruction-assignment (deconstruction into into exising variables):
No syntax change (one kind of unary_expression is already tuple_literal).

- Static semantic: The LHS of the an assignment-expression used be a L-value, but now it can be L-value -- which uses existing rules -- or tuple_literal. The new rules for tuple_literal on the LHS...
- Dynamic semantic

Open issues and assumptions:

- I assume this should work even if `System.ValueTuple` is not present.
- How is the Deconstruct method resolved? I assumed there can be no ambiguity. Only one `Deconstruct` is allowed (in nesting cases we have no type to guide the resolution process).
- Do the names matter? `int x, y; (a: x, b: y) = M();`
- Can we deconstruct into a single out variable? I assume no.
- I assume no compound assignment `(x, y) += M();`


(note: assignment should be assignment_expression in C# spec)

We can re-use the existing assignment syntax node (AssignmentExpression). What is on the left is a tuple expression.

The binding for assignment (which currently checks if the left is can be assigned to and if the two sides are compatible) would be updated:
- Each item on the left needs to be assignable and needs to be compatible with corresponding position on the right
- Needs to handle nesting case such as `(x, (y, z)) = M();`, but note that the second item in the top-level group has no discernable type.

The lowering for assignment would translate: (expressionX, expressionY, expressionZ) = (expressionA, expressionB, expressionC) into:
```
tempX = &evaluate expressionX
tempY = &evaluate expressionY
tempZ = &evaluate expressionZ

tempRight = evaluate right and evaluate Deconstruct

tempX = tempRight.A (including conversions)
tempY = tempRight.B (including conversions)
tempZ = tempRight.C (including conversions)

“return/continue” with newTupleIncludingNames tempRight (so you can do get Item1 from the assignment)?
```

The evaluation order for nesting `(x, (y, z))` is:
```
tempX = &evaluate expressionX

tempRight = evaluate right and evaluate Deconstruct

tempX = tempRight.A (including conversions)
tempLNested = tempRight.B (no conversions)

tempY = &evaluate expressionY
tempZ = &evaluate expressionZ

tempRNest = evaluate Deconstruct on tempRight

tempY = tempRNest.B (including conversions)
tempZ = tempRNest.C (including conversions)

```

The evaluation order for the simplest cases (locals, fields, array indexers, or anything returning ref) without needing conversion:
```
evaluate side-effect on the left-hand-side variables
evaluate Deconstruct passing the references directly in
```

Note that the feature is built around `Deconstruct`. `ValueTuple` and `System.Tuple` will rely on that same mechanism, except that the compiler may need to synthesize the proper `Deconstruct` methods.

Target typing and type inference are likely to just work. (TODO: is there any target typing or type inference here?)


###Deconstruction-declaration (deconstruction into new variables):

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

**References**

[C# Design Notes for Apr 12-22, 2016](https://github.com/dotnet/roslyn/issues/11031)
