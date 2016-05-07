
For some background, there are different kinds of syntax:

* Statement
* Expression
* Declarator


Two major options to start from:

**Option 1**
Treat deconstruction of a tuple into existing variables as a kind of assignment.

Treat deconstruction of a tuple into new variables as a new kind of node (TODO: is it assignment or expression?). 
It would pick up the behavior of each contexts where new variables can be declared (TODO: need to list). For instance, in LINQ, new variables go into a transparent identifiers.
It is seen as deconstructing into separate variables (we don't introduce transparent identifiers in contexts where they didn't exist previously).


**Option 2**




**Implications**

| Option 1 | Option 2 | Code | Notes |
| -------- | -------- | ---- | ----- |
| Not allowed | Succeeds | `int x;   (x, int y) = (0, 0);` | |
