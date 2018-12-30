
I'll share some notes about the design for async-iterator methods (C# 8.0 feature).

Editorial notes:
- Some sections probably should be skipped on your first reading. I marked them as "optional".
- Although I will gloss over many important parts, I will try to mention them to give a sense of how things fit together.
- `/* ... */` comments indicate omitted code.
- `// ... ` comments apply to code that is there.
- Every illustration of generated code omits some details.


## Overview

Async-iterator methods are methods with both yields and awaits and produce async-enumerables (`IAsyncEnumerable<T>`) or async-enumerators (`IAsyncEnumerator<T>`).
Those are methods that yield a sequence of items (like iterator methods that return `IEnumerable<T>`), but allow for asynchronous computation (like async methods).


## Consumption with `await foreach`

The `await foreach` statement offers the most straightforward way of enumerating an async-enumerable's items.
It is very similar to `foreach` for enumerables. The main difference is that `await foreach` awaits each item (and also disposal).

The parallel jumps out in the interfaces involved:
| `IAsyncEnumerable<T>` used with `GetAsyncEnumerator()` | `IEnumerable<T>` used with `GetEnumerator()` |
| `IAsyncEnumerator<T>` used with `await MoveNextAsync()` and `Current` | `IEnumerator<T>` used with `MoveNext()` and `Current` |
| `IAsyncDisposable` used with `await DisposeAsync()` | `IDisposable` used with `Dispose()` |

So it is unsurprising that the lowering for `await foreach` is very similar to that of a `foreach`.

The following:
```C#
await foreach (var value in asyncEnumerable)
{
   ... body using `value` ...
}
```
translates to:
```C#
var enumerator = asyncEnumerable.GetAsyncEnumerator();
try
{
    while (await enumerator.MoveNextAsync())
    {
        var value = enumerator.Current;
        ... body using `value` ...
    }
}
finally
{
    await enumerator.DisposeAsync();
}
```


### Control flow (caller and background execution)

In a `await foreach`, the caller repeatedly calls `MoveNextAsync()`.
This returns a `ValueTask<bool>` where the `bool` represents the presence or absence of an item.
When you call `MoveNextAsync()`:
- if the code reaches a `yield return` statement, you immediately get a `true`,
- if the code reaches the end of the method, you immediately get a `false`,
- if you reach an `await`, you get a pending task and the code continues to execute in the background.
As the code continues to execute in the background:
- if it reaches a `yield return`, the background execution completes and the task we've previously handed to the caller is fulfilled with `true`,
- if it reaches the end of the method, that task is fulfilled with `false`,
- if it reaches an `await`, execution continues in the background.

Note that the method is moved forward either by the caller or by background execution, but never both.


### Exception handling (optional)

When the caller is moving the method forward and an exception is thrown (without being caught by user code in the async-iterator method), `MoveNextAsync()` will return a task as normal. 
That task is complete (rather than pending). When the caller tries to access its result the task will throw the exception it holds.

When background execution is moving the method forward and an exception is thrown without handling, the exception is similarly caught and passed onto the caller via the task.


### Parallels with iterator and async control flow (optional)

The execution of iterator methods shifts the control between the caller and the method:
- the caller repeatedly calls `MoveNext()` (which executes the method),
- the method yields control back to the caller (when reaching a `yield return`).

The execution of async methods starts by the caller giving control to the method and background execution.
From there on, the control shifts between the method and background execution:
- the background execution resumes the method after a long-running task completes,
- the method yields control back to the background execution (when reaching an `await`).
Only when the method completes with a `return` is the control fully returned to the caller.

The execution of async-iterator methods mixes both of those patterns, with the control shifting between caller (repeatedly calls `MoveNextAsync()`), method (gives control to the caller when reaching a `yield return` and to the background execution when reaching an `await`) and background execution (resumes the method after an `await`).


## Lowering

Clearly, yields and awaits do much of the work. So I'll describe the process of **lowering** async-iterator methods, that is replacing awaits and yields with simpler primitives.
My approach is bottom-up, starting from parts and building up from there.


### Await suspensions

`await expr` in async-iterator methods is lowered as:
```C#
... code before `await expr` ...

{
    var awaiterTemp = <expr>.GetAwaiter();
    if(!awaiterTemp.IsCompleted)
    {
        /* set state to N */
        /* save awaiter temp */
        /* kick off background execution */
        return;

        resumeLabelN:
        /* reset state */
        /* restore awaiter temp */
    }
}

// continue method execution with the result
... awaiterTemp.GetResult()

... code after `await expr` ...
```

Note this is the same pattern used to lower awaits in async methods.

As you can see, we're introducing a suspension and resumption in the middle of the method.
The suspension is the code leading up to the `return` statement. Each suspension is identified by a number `N`.
The resumption is the code starting from label `resumeLabelN:`.

You might wonder how execution resumes from that label. The lowering also adds logic at the beginning of the method to jumps to `resumeLabelN:` when the **state** is `N`.
So `{ ... method body... }` is lowered to start with a **dispatching** `switch` statement:
```C#
{
    switch (state)
    {
        ...
        case N:
            goto resumeLabelN;
        ...
    }

    ... rest of lowered method body, including the suspension for state N and the `resumeLabelN` label ...
}
```

When either the caller or background execution need to move the method forward, execution will resume where we need it to because the state variable was set to `N` and the dispatching logic will jump to the label we want.

Because there is a cost to starting background execution, suspending and resuming, the lowering pattern is optimized: if the task was already completed, we avoid that overhead and instead just move ahead with the result.


#### Locals

Although I won't go into much details on this, it is worth mentioning that all the local variables in the method are converted to fields on a compiler-generated type, so they maintain their values across suspensions/resumptions.

The compiler also generates fields for:
- method parameters,
- the `state` value,
- persisting `awaiterTemp`,
- the `current` value, (see section on "yield return" suspension)
- the machinery for background execution and continuation.


#### Spilling (optional)

Lowering an `await expr` as described about works well when it is an expression statement (`await expr;`) or an assignment statement (`x = await expr;`).
But when it appears inside another expression, such as an invocation `Method1(Method2(), await expr)`, this lowering is not sufficient.
This invocation needs to evaluate `Method2()`, put the result on the stack, get a result from awaiting `expr`, put that result on the stack, then call `Method1` with both arguments on the stack.

The problem is that the lowering for `await expr` involves a suspension (a `return` statement). But returning loses information saved in the current stack frame.

The solution is to do some additional lowering (as an earlier pass) to turn `Method1(Method2(), await expr)` into:
```C#
var temp1 = Method2();
var temp2 = await expr;
Method1(temp1, temp2);
```

This relies on locals (instead of the stack) to store the two results.
So when we rewrite async or async-iterator methods, we can assume that all awaits have been spilled.


#### Nested dispatching (optional)

We have seen how dispatch blocks (switch statements filled with gotos) help to resume execution from a certain point in the method.
But the CLR (and C#) disallow jumping into `try` statements.
So if you have an `await` inside a `try`, that simple dispatching strategy doesn't suffice.

Taking an example:
```C#
... code before try ...
try
{
    ... code before await ...
    await expr; // state N
    ... code after await ...
}
finally
{
    ...
}
```

To allow resumption from `resumeLabelN:` in the lowered `await expr;`, some additional dispatching `switch` statements and some additional labels are introduced:
```C#
switch (state)
{
    ...
    case N:
        goto tryStatementLabel1:
    ...
}

... code before try ...

tryStatementLabel1:
try
{
    switch (state)
    {
        ...
        case N:
            goto resumeLabelN:
        ...
    }

    ... code before await ...
    /* lowered `await expr;` using `resumeLabelN` */
    ... code after await ...
}
finally
{
    ...
}
```

With such nested dispatch blocks, restarting the method with state set to `N` resumes the execution from `resumeLabelN`.

// REMOVE? In terms of implementation, the key is to keep track of which states/suspensions appeared inside a given `try`. So when a `try` statements are lowered, we can inject the necessary labels and dispatch blocks.


#### Awaits in `catch` and `finally` blocks (optional)

TODO cover cases where the `await` occurs inside the `catch` or `finally` blocks


### yield return suspensions

`yield return expr;` is lowered as:
```C#
... code before `yield return expr;` ...

current = <expr>;
/* set state to N */
/* record that a value is available */
return;

resumeLabelN:
/* reset state */
/* conditional jump for disposal, ignore for now */

... code after `yield return expr;` ...
```

The suspension again relies on setting a state to `N` and returning. Additionally, it saves the yielded value (that is how the caller can access it with the `Current` property) and records that a value is available (more on that later).
The resumption is pretty straightforward, if we ignore the disposal logic for now.


## Quick recap so far
Considering a simple async-iterator method:
```C#
async IAsyncEnumerable<int> GetIntegersAsync()
{
    Console.WriteLine(1);
    await expr;
    Console.WriteLine(2);
    yield return 42;
    Console.WriteLine(3);
}
```

The compiler will produce a type with various fields (for locals, `state`, ...) and the following `MoveNext()` method:
```C#
private void MoveNext()
{
    // dispatch block
    switch (state)
    {
        case 1:
            goto resumeLabel1;
        case 2:
            goto resumeLabel2;
    }

    Console.WriteLine(1);

    // lowered `await expr;`
    {
        var awaiterTemp = <expr>.GetAwaiter();
        if(!awaiterTemp.IsCompleted)
        {
            /* set state to 1 */
            /* save awaiter temp */
            /* kick off background execution */
            return;
            resumeLabel1:
            /* reset state */
            /* restore awaiter temp */
        }
    }

    Console.WriteLine(2);

    // lowered `yield return 42;`
    {
        current = 42;
        /* set state to 2 */
        /* record that a value is available */
        return;
        resumeLabel2:
        /* reset state */
    }

    Console.WriteLine(3);

    /* omitted termination logic */
}
```

This lowered method is private, but the compiler also generates some public methods on this type, which both the caller and background execution will interact with.
We'll cover some of those next.


## Public API of async-enumerables

Now we can get back to the production of async-enumerables from async-iterator methods.


#### GetAsyncEnumerator

`GetAsyncEnumerator` is the first method called on an async-enumerable.
You could implement the `IAsyncEnumerable<T>` interface by hand, but I'll focus on the implementation the compiler generates for async-iterator methods.
The compiler generates an `GetAsyncEnumerator` which returns an instance of the type described above.
Most importantly, the returned instance is initialized with a known `state` so that it is ready to execute the user code from the start.

I will skip over some details here. There are some optimizations that avoid allocations but involve keeping a second copy of all the method parameters.
Those copies give us pristine values of the parameters to use if `GetAsyncEnumerator()` is called a second time.


#### MoveNextAsync and Current

TODO





#### DisposeAsync

##### Purpose

It is useful to first understand why enumerators and async-enumerators are disposable.

Let's consider the non-async case with an example:
```C#
IEnumerable<int> GetItemsAndLog()
{
    try
    {
         yield return 1;
         await SomethingAsync();
         yield return 2;
    }
    finally
    {
         logger.Log();
    }
    yield return 3;
    SomethingElse();
}
```

There are three cases when the `finally` block should be evaluated:
- normal execution (ie. after resuming from `yield return 2;` or when reaching a `yield break;`),
- exceptional execution (ie. if `Something()` throws an exception),
- interrupted enumeration.

Let's expound on this last case: it is possible for the caller of `GetItemsAndLog` to call `MoveNext()` a few times without reaching the end of the method, and then decide to terminate the enumeration anyways.

For instance, the caller could terminate the enumeration after just one `MoveNext()` call:
```C#
foreach (var item in GetItemsAndLog())
{
    break; // we get to item `1`, but we cut the enumeration short for some reason
}
```

`Dispose()` should execute different `finally` block(s) depdending on the state the method was suspended at:
- No code should execute when disposing an iterator that was not moved forward.
- After calling `MoveNext()` once or twice (ie. after reaching `yield return 1;` or `yield return 2;`) the `finally` should be evaluated.
- After three or four steps (ie. after reaching `yield return 3;` or the end of the method) the `finally` should not be evaluated.
- You can imagine cases with multiple `try/finally` statements that are nested, where disposal should evaluate more than one `finally` block "on the way out".

// When we break out of this `foreach` loop, `Dispose()` is called and we expect the `finally` block to be executed. This is especially important if the `finally` is disposing some resources.


##### Design

Now that we understand __why__ async-enumerators implement `IAsyncDisposable`, let's look at __how__ we achieve the right disposal behavior for async-iterator methods.

A few notes before getting started:
- Although both iterator and async-iterator methods produce disposal logic, they are implemented quite differently in the Roslyn compiler. I'll focus on disposal of async-iterator methods.
  - One reason for this difference is that unlike disposal of iterator methods, the disposal of async-iterator methods can encounter suspensions from awaits. For instance, an `await` inside a `finally`.
- The caller should only invoke `DisposeAsync()` when the async-iterator method is suspended on a `yield return` statement. Invoking it at other times during the method's execution produces unspecified behavior.
- The language disallows `yield return` statements inside `finally` blocks. So once we start disposing, there is no chance of yielding additional items.


// Because we keep track of `state` when the iterator method is suspended, we know which `finally` block(s) should be evaluated.

When `DisposeAsync()` is called, we should resume from our current suspension state (that's already handled by dispatching), but from there we should not execute any code that isn't inside a `finally`.

This special mode of execution (skipping any code that isn't inside a `finally`) is determined the `disposeMode` boolean field.
So `DisposeAsync` just needs to set the dispose mode flag and resume execution of the async-iterator method.
From the resume label, we should jump straight to the enclosing `finally`. So we add an `if (disposeMode) goto enclosingFinallyLabel;` in the resumption logic for `yield return` statements.
Once that `finally` block completes execution, we should similarly jump to the next enclosing `finally`. So we add a similar conditional jump after the `finally`.

Putting together everything we've seen so far, each `try/finally` is lowered as:
```C#
tryStatementLabelN:
try
{
    /* nested dispatch block */
    ... lowered body of the `try`, with any lowered `yield return` statements conditionally jumping to finallyLabelN ...

    finallyLabelN:
}
finally
{
    ... lowered body of the `finally` ...
}
if (disposeMode) goto enclosingFinallyLabelM;
```

The outermost `finally` is also followed by such a conditional jump, but instead of jumping to the next enclosing `finally`, it jumps to the end of the method.

// TODO consider removing this example
In the following example, if we dispose from `yield return 1;`, we'll resume from that state, then jump to the `finally`, execute the `finally` (including an await suspension), and once the `finally` is done we'll jump to the end of the method (`}`).

```C#
IEnumerable<int> GetItemsWithLongDisposal()
{
    try
    {
         yield return 1;
         Console.Write(1);
    }
    finally
    {
         Console.Write(2);
         await SomethingLong();
         Console.Write(3);
    }
    yield return 3;
    SomethingElse();
}
```


#### Yield break

When a `yield break;` is reached, the relevant `finally` blocks should get executed immediately.

With everything we've seen so far, `yield break` statements become straightforward.
They can simply be lowered as:
```C#
disposeMode = true;
/* jump to enclosing finally or exit */
```

Note that in this case, the caller will not get a result from `MoveNextAsync()` until we've reached the end of the method (**finished** state) and so `DisposeAsync()` will have no work left to do.


### End of method

When you reach the end of the async-iterator method, we set the `state` to a special "finished" value, record the fact that no value remains, and return.



### Generic type parameters (optional)

Consider the following async-iterator method:
```C#
IAsyncEnumerable<T> GetValuesAsync<T>(Task<T> slowValue)
{
    T value = await slowValue;
    yield return value;
    yield return default(T);
}
```

Because the method has a type parameter, the generated type will have one as well (let's call it `T2`). As part of rewriting the body of the method, all references to `T` will be converted to references to `T2`:
```C#
class UnspeakableType<T2>
{
    private void MoveNext()
    {
        /* dispatch block */
        /* lowered `T2 value = await slowValue;` */
        /* lowered `yield return value;` */
        /* lowered `yield return default(T2); */
    }

    /* various fields and public members omitted */
}
IAsyncEnumerable<T> GetValuesAsync<T>(Task<T> slowValue)
{
    return new UnspeakableType<T>();
}
```







Notes on async-iterator methods:
- MoveNextAsync(), Current, promise
- end of method
- cancellation token
- exception handling
- enumerable vs. enumerator
- extracted catch/finally
- code from ILSpy, finish with an actual example?
- mention alternative API design? (no)
Point to Lippert's series
