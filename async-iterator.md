

In this post, I'll offer an overview of async-iterator methods (C# 8.0 feature). I'll cover technical details about the implementation in a follow-up post.

## Overview

Async-iterator methods are methods which produce async-enumerables (`IAsyncEnumerable<T>`) or async-enumerators (`IAsyncEnumerator<T>`) and contain both yields and awaits.
Those methods yield a sequence of items (like iterator methods that return `IEnumerable<T>` or `IEnumerator<T>`), but allow for asynchronous computation (like async methods).

## Consumption with `await foreach`

The `await foreach` statement offers the most straightforward way of enumerating an async-enumerable's items.
It is very similar to `foreach` for enumerables. The main difference is that `await foreach` awaits the retrieval of each item (and also disposal).

The API for async enumerables should be familiar to developers that have used enumerables:

| async enumerables | enumerables |
| --- | --- |
| `IAsyncEnumerable<T>` used with `GetAsyncEnumerator()` | `IEnumerable<T>` used with `GetEnumerator()` |
| `IAsyncEnumerator<T>` used with `await MoveNextAsync()` and `Current` | `IEnumerator<T>` used with `MoveNext()` and `Current` |
| `IAsyncDisposable` used with `await DisposeAsync()` | `IDisposable` used with `Dispose()` |

So it is unsurprising that the code generated from `await foreach` is very similar to that of a `foreach`.

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

To start looking at how async-iterators work, let's enumerate the result of invoking one: `await foreach (var i in GetItemsAsync()) ...`.

In a `await foreach`, the caller repeatedly calls `MoveNextAsync()`.
Each call returns a `ValueTask<bool>` where the `bool` represents the presence or absence of an item.
The caller will get:
- an immediate `true` when the `GetItemsAsync` code reaches a `yield return`,
- an immediate `false` when the code reaches a `yield break` or the end of the method,
- a faulted task when the code throws an unhandled exception, so that an exception thrown from the task when the caller tries to access the task's result,
- a pending task when the code reaches an `await` of a task that isn't immediately completed and thus needs to continue executing in the background.

As the code continues to execute in the background:
- if it reaches a `yield return`, the background execution completes and the task we've previously handed to the caller is fulfilled with `true`,
- if it reaches a `yield break` or the end of the method, that task is fulfilled with `false`,
- if it reaches an unhandled exception, the task we've previously handed to the caller is faulted with that exception,
- if it reaches an `await`, execution continues in the background and the task we've previously handed to the caller is left as pending.

Note that the method is moved forward either by the caller or by background execution, but never both. Calling `MoveNextAsync()` before the task from the previous call completes produces unspecified results.

### Comparisons with iterator and async control flows

The execution of *iterator* methods shifts the control between the caller and the method:
- the caller repeatedly calls `MoveNext()` (which executes the method),
- the method yields control back to the caller (when reaching a `yield return`, a `yield break`, the end of the method, or an unhandled exception).

The execution of *async* methods starts by the caller giving control to the method, and then shifting control between the method and background execution:
- background execution resumes the method after an awaited task completes asynchronously,
- the method yields control back to the background execution (when reaching an `await` of a task that doesn't complete immediately).
The caller is only able to move forward when the method completes (with a `return` or an unhandled exception).

The execution of *async-iterator* methods mixes both of those patterns, with the control shifting between caller (repeatedly calls `MoveNextAsync()`), method (gives control to the caller when reaching a `yield return`/`yield break`/end-of-method/unhandled-exception, and to the background execution when reaching an `await`) and background execution (resumes the method after an `await`).


----

After an overview of async collection and async-iterator methods in the previous post, we'll dive into the compiler's implementation of those methods.

Editorial notes:
- Some sections probably should be skipped on first reading. I marked them as "optional".
- `/* ... */` comments indicate omitted code, while `// ... ` comments apply to code that is there.
- Every illustration of generated code omits some details.


## Lowering

The ultimate goal of the compiler is to produce IL from C# code. But it is often more convenient to introduce translation steps that produce intermediate C# for complex constructs. This process is called **lowering** as it translates high-level constructs into lower-lever ones.
For example, `foreach` can be expressed in terms of `try/finally`, `while`, invocations, assignments and other simple constructs. Then we can let the existing compiler machinery generate IL for those primitive constructs.
I'll describe the process of lowering async-iterator methods, which produces a `void MoveNext()` method by replacing awaits and yields with simpler primitives.

I'll start with many techniques already used for async methods (await suspensions, extracting locals to fields, spilling awaits, dispatch blocks).
Then I'll explain `yield return` suspensions, disposal and yield breaks.

### `await`

`await expr` in async-iterator methods is lowered following the same pattern used to lower awaits in async methods:
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
        /* reset state to -1 (RunningState) */
        /* restore awaiter temp */
    }
}

// continue method execution with the result
... awaiterTemp.GetResult()

... code after `await expr` ...
```

If you are curious about elided details, look at the end-to-end example at the end of the post for a fully fleshed out version of the lowering pattern.

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

When either the caller or background execution need to move the method forward, execution will resume from the right label because the state variable was set to `N` and the dispatching logic will jump to that label.

Because there is a cost to starting background execution, suspending and resuming, the lowering pattern is optimized: if the task was already completed (`awaiterTemp.IsCompleted`), we avoid that overhead and instead just move ahead with the result.


#### Locals

Local variables that are used across suspensions are converted to fields on a compiler-generated type, so they maintain their values when suspding and resuming.

The compiler also generates fields for:
- method parameters,
- the `state` value,
- persisting `awaiterTemp`,
- the `current` value, (see section on "yield return" suspension)
- the machinery for background execution and continuation.

TODO2 parameters and proxies
TODO2 cancellation token source


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


#### Suspensions in `try` blocks (optional)

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

To allow resumption from `resumeLabelN:` in the lowered `await expr;`, some additional dispatching `switch` statements and labels are introduced:
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

With such nested dispatch blocks, restarting the method with state set to `N` resumes its execution from `resumeLabelN`.


#### `await` in `catch` and `finally` blocks (optional)

The nested dispatching allows us to resume execution at a label inside a `try` block.
But that strategy doesn't allow us to resume from a label inside a `catch` block which is only entered when an exception is thrown.
This is solved by yet another lowering pass which extracts any `catch` or `finally` blocks containing awaits and turns them into regular blocks.

Consider a `finally`:
```C#
try
{
    ... body ...
}
finally
{
    ... some await ...
}
```
We can extract it into a regular block:
```C#
Exception exceptionLocal = null;
try
{
     ... body ...
    goto extractedFinallyLabel;
}
catch (Exception e)
{
    exceptionLocal = e;
}

// extracted finally block
extractedFinallyLabel:
{
    ... some await ...
    if (exceptionLocal != null)
    {
        throw exceptionLocal;
    }
}
```

This pattern can be expanded to extract `catch` handlers: each `catch` block is replaced with logic to pend the exception (save the exception into a local, remember which exception handler we were in) and the original handlers are moved out into the extracted block.


### `yield return`

`yield return expr;` is lowered as:
```C#
... code before `yield return expr;` ...

current = <expr>;
/* set state to N */
/* record that a value is available */
return;

resumeLabelN:
/* reset state to -1 (RunningState) */
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
    try
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
    }
    catch (Exception e)
    {
        /* record that an exception was thrown */
        return;
    }

    /* record that no value remains */
    /* set state to "finished" state */
}
```

Although I won't explain it in details, the generated type includes machinery to produce a `ValueTask<bool>` and complete it in different ways:
- with result `true` (used in `yield return` statements),
- with result `false` (used at the end of the method),
- with an exception (used by the catch-all exception handler).

Note that this lowered method is private. So although it is at the heart of the generated type, the caller and background execution rely on public APIs that wrap it.
We'll look at `GetAsyncEnumerator`, `MoveNextAsync` and `DisposeAsync` next.


## `GetAsyncEnumerator`

The compiler generates an `GetAsyncEnumerator` which returns an instance of the type described above.

Most importantly, the returned instance is initialized with a known `state` so that it is ready from the beginning.

The method also includes some optimizations to avoid allocations. But as a result it has to deal with a second copy of all the method parameters. Those copies give us pristine values of the parameters when `GetAsyncEnumerator()` is called more than once.


## `MoveNextAsync`

`MoveNextAsync` is also quite simple: it calls `MoveNext()` and returns a `ValueTask<bool>` using the machinery mentioned earlier.
Depending on the situation, the returned task may already be completed (with `true` or `false` or an exception) or still pending.

The method is complicated somewhat by optimizations and the need to respect synchronization contexts, but I will skip that.


## `DisposeAsync`

### Purpose

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
- normal execution (ie. after the `try` block completes or when reaching a `yield break;`),
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


### Design

Now that we understand __why__ async-enumerators implement `IAsyncDisposable`, let's look at __how__ we achieve the right disposal behavior for async-iterator methods.

A few notes before getting started:
- Although both iterator and async-iterator methods produce disposal logic, they are implemented quite differently in the Roslyn compiler. I'll focus on disposal of async-iterator methods.
  - One reason for this difference is that unlike disposal of iterator methods, the disposal of async-iterator methods can encounter suspensions from awaits. For instance, an `await` inside a `finally`.
- The caller should only invoke `DisposeAsync()` when the async-iterator method is suspended on a `yield return` statement. Invoking it at other times during the method's execution produces unspecified behavior.
- The language disallows `yield return` statements inside `finally` blocks. So once we start disposing, there is no chance of yielding additional items.

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


### `yield break`

When a `yield break;` is reached, the relevant `finally` blocks should get executed immediately.

With everything we've seen so far, `yield break` statements are straightforward.
They can simply be lowered as:
```C#
disposeMode = true;
/* jump to enclosing finally or exit */
```

Note that in this case, the caller will not get a result from `MoveNextAsync()` until we've reached the end of the method (**finished** state) and so `DisposeAsync()` will have no work left to do.


## End-to-end example

To wrap things up, let's look at the code generated for this async-iterator method `M`:

```C#
class C
{
    public static async IAsyncEnumerable<int> M()
    {
        Console.Write(0);
        await foreach (var i in M2())
        {
            Console.Write(1);
            await Task.Delay(1);
            Console.Write(2);
            yield return i;
            Console.Write(3);
        }
        Console.Write(4);
    }
    public static IAsyncEnumerable<int> M2() { /* omitted */ }
}
```

The pseudo-code below shows what we generate. It was manually edited for readability. In particular, it more closely reflects the lowering patterns and resembles the original code structure than what you would normally see from decompilation.

You can see that:
- method `M` is replaced by a stub method that just returns an instance of `Unspeakable`
- the code for method `M` ends up in `MoveNext` in the generated type
- the type contains fields for the local and temporary variables extracted from `M` (`i`, `__enumerator`) and other machinery (`__state`, `__current`, etc)
- `await` and `yield return` are expanded into small blocks of code with a suspension and a resume label
- dispatching `switch` statements are added to allow resuming the execution from a given state/label
- `await foreach` is expanded into a loop that repeatedly does `await MoveNextAsync()` and gets the value from `Current`, and that loop is inside a `try/finally` which ensures we call `DisposeAsync()` on the enumerator
- that `finally` is extracted so that we can resume from `await __enumerator.DisposeAsync()` at label `resumeDisposeAsyncLabel`
- disposal is largely handled by the `MoveNext` method itself, and it uses a flag to go down a specific control flow path

A few things this example didn't illustrate:
- if `M` had returned `IAsyncEnumerator<int>` instead of `IAsyncEnumerable<int>` then we would generate mostly the same thing, except that `Unspeakable` would not implement `IAsyncEnumerable<int>` and would not have a `GetAsyncEnumerator()` method
- this example didn't include any method type parameters, method parameters, spilled awaits, `yield break` or exception handlers
- it is possible that C# 8.0 will add some mechanism to pass a `CancellationToken` into async-iterator method bodies, but that is not finalized. I'll update this post accordingly

```C#
class C
{
    // stub method
    [AsyncIteratorStateMachine(typeof(Unspeakable))]
    public static IAsyncEnumerable<int> M()
    {
        return new Unspeakable(FinishedState);
    }

    [CompilerGenerated]
    private sealed class Unspeakable : IAsyncEnumerable<int>, IAsyncEnumerator<int>, IAsyncDisposable, IValueTaskSource<bool>, IValueTaskSource, IAsyncStateMachine
    {
        public int __state;
        private int __current;
        private bool __disposeMode;

        private IAsyncEnumerator<int> __asyncEnumerator;
        private int i;

        private object __exception;
        private TaskAwaiter __taskAwaiter;
        private ValueTaskAwaiter<bool> __valueTaskAwaiterBool;
        private ValueTaskAwaiter __valueTaskAwaiter;

        public AsyncIteratorMethodBuilder __builder;
        public ManualResetValueTaskSourceCore<bool> __promiseOfValueOrEnd;
        private int __initialThreadId;

        public Unspeakable(int __state)
        {
            __state = __state;
            __initialThreadId = Environment.CurrentManagedThreadId;
            __builder = AsyncIteratorMethodBuilder.Create();
        }

        // rewritten method M
        private void MoveNext()
        {
            int state = __state;
            try
            {
                // dispatch block
                switch (state)
                {
                    case -4:
                        goto tryDispatchLabel;
                    case 0:
                        goto tryDispatchLabel;
                    case 1:
                        goto tryDispatchLabel;
                    case 2:
                        goto resumeDisposeAsyncLabel;
                }

                if (__disposeMode) goto setResultFalseLabel;
                __state = state = RunningState;

                Console.Write(0);

#region await foreach

                __asyncEnumerator = M2().GetAsyncEnumerator();
                __exception = null;

            tryDispatchLabel:;
                try
                {
                    // nested dispatch block
                    switch (state)
                    {
                        case -4:
                            goto resumeYieldReturnILabel;
                        case 0:
                            goto resumeAwaitDelayLabel;
                        case 1:
                            goto resumeAwaitMoveNextAsyncLabel;
                    }

                    goto moveNextAsyncLabel;
                hasCurrentLabel:;
                    i = __asyncEnumerator.get_Current();

#region body of await foreach

                    Console.Write(1);

                    // await Task.Delay(1);
                    System.Runtime.CompilerServices.TaskAwaiter awaitDelayAwaiter = Task.Delay(1).GetAwaiter();
                    if (!awaitDelayAwaiter.get_IsCompleted())
                    {
                        __state = state = 0; // state 0
                        __taskAwaiter = awaitDelayAwaiter;
                        var temp = this;
                        __builder.AwaitUnsafeOnCompleted(awaitDelayAwaiter, ref temp);
                        return;
                    resumeAwaitDelayLabel:;
                        awaitDelayAwaiter = __taskAwaiter;
                        __taskAwaiter = default;
                        __state = state = RunningState;
                    }
                    awaitDelayAwaiter.GetResult();

                    Console.Write(2);

                    // yield return i;
                    __current = i;
                    __state = state = -4; // state -4
                    goto setResultTrueLabel;
                resumeYieldReturnILabel:;
                    __state = state = RunningState;
                    if (__disposeMode) goto disposeEnumeratorLabel;

                    Console.Write(3);
#endregion

                moveNextAsyncLabel:;

                    // await __asyncEnumerator.MoveNextAsync();
                    System.Runtime.CompilerServices.ValueTaskAwaiter<bool> moveNextAsyncAwaiter = __asyncEnumerator.MoveNextAsync().GetAwaiter();
                    if (!moveNextAsyncAwaiter.get_IsCompleted())
                    {
                        __state = state = 1; // state 1
                        __valueTaskAwaiterBool = moveNextAsyncAwaiter;
                        var temp = this;
                        __builder.AwaitUnsafeOnCompleted(moveNextAsyncAwaiter, ref temp);
                        return;
                    resumeAwaitMoveNextAsyncLabel:;
                        moveNextAsyncAwaiter = __valueTaskAwaiterBool;
                        __valueTaskAwaiterBool = default;
                        __state = state = RunningState;
                    }
                    bool moveNextAsyncResult = moveNextAsyncAwaiter.GetResult();

                    if (moveNextAsyncResult) goto hasCurrentLabel;
                    goto disposeEnumeratorLabel;
                }
                catch (Object)
                {
                }

                // extracted finally from await foreach
            disposeEnumeratorLabel:;
                if (__asyncEnumerator != null)
                {
                    System.Runtime.CompilerServices.ValueTaskAwaiter disposeAsyncAwaiter = __asyncEnumerator.DisposeAsync().GetAwaiter();
                    if (!disposeAsyncAwaiter.get_IsCompleted())
                    {
                        __state = state = 2; // state 2
                        __valueTaskAwaiter = disposeAsyncAwaiter;
                        var temp = this;
                        __builder.AwaitUnsafeOnCompleted(disposeAsyncAwaiter, ref temp);
                        return;
                    resumeDisposeAsyncLabel:;
                        disposeAsyncAwaiter = __valueTaskAwaiter;
                        __valueTaskAwaiter = default;
                        __state = state = RunningState;
                    }
                    disposeAsyncAwaiter.GetResult();
                }

                object exception = __exception;
                if (exception != null)
                {
                    var temp = exception as System.Exception;
                    if (temp != null) throw exception;
                    ExceptionDispatchInfo.Capture(temp).Throw();
                }

                if (__disposeMode) goto setResultFalseLabel;
                __exception = null;
                __asyncEnumerator = null;

#endregion

                Console.Write(4);
                goto setResultFalseLabel;
            }
            catch (System.Exception e)
            {
                __state = FinishedState;
                __promiseOfValueOrEnd.SetException(e);
                return;
            }

        setResultFalseLabel:;
            __state = FinishedState;
            __promiseOfValueOrEnd.SetResult(false);
            return;

        setResultTrueLabel:;
            __promiseOfValueOrEnd.SetResult(true);
            return;
        }

        IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(CancellationToken token)
        {
            Unspeakable result;
            if (__state == FinishedState && __initialThreadId == Environment.CurrentManagedThreadId)
            {
                __state = InitialState;
                result = this;
                __disposeMode = false;
            }
            else
            {
                result = new Unspeakable(InitialState);
            }
            return result;
        }

        ValueTask<bool> IAsyncEnumerator<int>.MoveNextAsync()
        {
            if (__state == FinishedState)
            {
                return default(ValueTask<bool>);
            }

            __promiseOfValueOrEnd.Reset();
            Unspeakable stateMachine = this;
            __builder.MoveNext(ref stateMachine);
            short version = __promiseOfValueOrEnd.Version;
            if (__promiseOfValueOrEnd.GetStatus(version) == ValueTaskSourceStatus.Succeeded)
            {
                return new ValueTask<bool>(__promiseOfValueOrEnd.GetResult(version));
            }

            return new ValueTask<bool>(this, version);
        }

        int IAsyncEnumerator<int>.Current => __current;

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            if (__state >= RunningState)
            {
                throw new NotSupportedException();
            }

            if (__state == FinishedState)
            {
                return default(ValueTask);
            }

            __disposeMode = true;
            __promiseOfValueOrEnd.Reset();
            Unspeakable stateMachine = this;
            __builder.MoveNext(ref stateMachine);
            return new ValueTask(this, __promiseOfValueOrEnd.Version);
        }

        ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
        {
            return __promiseOfValueOrEnd.GetStatus(token);
        }

        bool IValueTaskSource<bool>.GetResult(short token)
        {
            return __promiseOfValueOrEnd.GetResult(token);
        }

        void IValueTaskSource<bool>.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            __promiseOfValueOrEnd.OnCompleted(continuation, state, token, flags);
        }

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        {
            return __promiseOfValueOrEnd.GetStatus(token);
        }

        void IValueTaskSource.GetResult(short token)
        {
            __promiseOfValueOrEnd.GetResult(token);
        }

        void IValueTaskSource.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            __promiseOfValueOrEnd.OnCompleted(continuation, state, token, flags);
        }

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
        {
        }
    }
}
```

## Conclusion

I hope you enjoyed this technical deep dive. Although there are always more details, we've covered a lot of ground, including existing machinery re-used from async methods, as well as the additions for async-iterator methods.
- https://github.com/dotnet/roslyn/blob/master/docs/features/async-streams.md
- https://github.com/dotnet/csharplang/blob/master/proposals/async-streams.md


Point to Lippert's series
https://blogs.msdn.microsoft.com/ericlippert/tag/iterators/ (including explanations for disallowing `yield return` in `catch` or `finally` blocks)
https://blogs.msdn.microsoft.com/ericlippert/tag/async/

