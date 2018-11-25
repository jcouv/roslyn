async-streams (C# 8.0)
----------------------

Async-streams are asynchronous variants of enumerables, where getting the next element may involve an asynchronous operation. They are types that implement `IAsyncEnumerable<T>`.

```C#
// Those interfaces will ship as part of .NET Core 3
namespace System.Collections.Generic
{
    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetAsyncEnumerator();
    }

    public interface IAsyncEnumerator<out T> : System.IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask<bool> MoveNextAsync();
        T Current { get; }
    }
}
namespace System
{
    public interface IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask DisposeAsync();
    }
}
```

When you have an async-stream, you can enumerate its items using an asynchronous `foreach` statement: `await foreach (var item in asyncStream) { ... }`.
An `await foreach` statement is just like a `foreach` statement, but it uses `IAsyncEnumerable` instead of `IEnumerable`, each iteration evaluates an `await MoveNextAsync()`, and the disposable of the enumerator is asynchronous.

Similarly, if you have an async-disposable, you can use and dispose it with asynchronous `using` statement: `await using (var resource = asyncDisposable) { ... }`
An `await using` statement is just like a `using` statement, but it uses `IAsyncDisposable` instead of `IDisposable`, and `await DisposeAsync()` instead of `Dispose()`.

The user can implement those interfaces manually, or can take advantage of the compiler generating a state-machine from a user-defined method (called an "async-iterator" method).
An async-iterator method is a method that:
1. is declared `async`,
2. returns an `IAsyncEnumerable<T>` type,
3. uses both `await` expressions and `yield` statements.

For example:
```C#
async IAsyncEnumerable<int> GetValuesFromServer()
{
    while (true)
    {
        IEnumerable<int> batch = await GetNextBatch();
        if (batch == null) yield break;

        foreach (int item in batch)
        {
            yield return item;
        }
    }
}
```

Just like in iterator methods, `yield return` statements are not allowed:
- in a `try` block that has a `catch` clause in async-iterator methods.
- in a `finally` clause, as the semantics would not be clear (what does it mean to yield an element during disposal? what does it mean to yield an element following a `yield break`?)
- in a `catch` clause

### Detailed design for `await using` statement

An asynchronous `using` is lowered just like a regular `using`, except that `Dispose()` is replaced with `await DisposeAsync()`.

### Detailed design for `await foreach` statement

An `await foreach` is lowered just like a regular `foreach`, except that:
- `GetEnumerator()` is replaced with `await GetEnumeratorAsync()`
- `MoveNext()` is replaced with `await MoveNextAsync()`
- `Dispose()` is replaced with `await DisposeAsync()`

Asynchronous foreach loops are disallowed on collections of type dynamic, as there is no asynchronous equivalent of the non-generic `IEnumerable` interface.

```C#
E e = ((C)(x)).GetAsyncEnumerator();
try
{
    while (await e.MoveNextAsync())
    {
        V v = (V)(T)e.Current;  -OR-  (D1 d1, ...) = (V)(T)e.Current;
        // body
    }
}
finally
{
    await e.DisposeAsync();
}
```

### Detailed design for async-iterator methods

An async-iterator method is replaced by a kick-off method, which initializes a state machine. It does not start running the state machine (unlike kick-off methods for regular async method).
The kick-off method method is marked with both `AsyncStateMachineAttribute` and `IteratorStateMachineAttribute`.

The state machine for an async-iterator method primarily implements `IAsyncEnumerable<T>` and `IAsyncEnumerator<T>`.
It is similar to a state machine produced for an async method. It contains builder and awaiter fields, used to run the state machine in the background (when an `await` is reached in the async-iterator). It also captures parameter values (if any) or `this` (if needed).
But it contains additional state:
- a promise of a value-or-end,
- a current yielded value of type `T`,
- an `int` capturing the id of the thread that created it,
- a `bool` flag indicating "dispose mode".

The central method of the state machine is `MoveNext()`. It gets run by `MoveNextAsync()`, or as a background continuation initiated from these from an `await` in the method.

The promise of a value-or-end is returned from `MoveNextAsync`. It can be fulfilled with either:
- `true` (when a value becomes available following background execution of the state machine),
- `false` (if the end is reached),
- an exception.
The promise is implemented as a `ManualResetValueTaskSourceLogic<bool>` (which is a re-usable and allocation-free way of producing and fulfilling `ValueTask<bool>` instances) and its surrounding interfaces on the state machine: `IValueTaskSource<bool>`, `IValueTaskSource` and `IStrongBox<ManualResetValueTaskSourceLogic<bool>>`.

Compared to the state machine for a regular async method, the `MoveNext()` for an async-iterator method adds logic:
- to support handling a `yield return` statement, which saves the current value and fulfills the promise with result `true`,
- to support handling a `yield break` statement, which sets the dispose mode on and jumps to the closest `finally` or exit,
- to exit the method, which fulfills the promise with result `false`,
- to the handling of exceptions, to set the exception into the promise.
(The handling of an `await` is unchanged)

This is reflected in the implementation, which extends the lowering machinery for async methods to:
1. handle `yield return` and `yield break` statements (add methods `VisitYieldReturnStatement` and `VisitYieldBreakStatement` to `AsyncMethodToStateMachineRewriter`),
2. produce additional state and logic for the promise itself (we use `AsyncIteratorRewriter` instead of `AsyncRewriter` to drive the lowering, and produces the other members: `MoveNextAsync`, `Current`, `DisposeAsync`, and some members supporting the resettable `ValueTask`, namely `GetResult`, `SetStatus`, `OnCompleted` and `Value.get`).

```C#
ValueTask<bool> MoveNextAsync()
{
    if (state == StateMachineStates.FinishedStateMachine)
    {
        return default(ValueTask<bool>);
    }
    valueOrEndPromise.Reset();
    var inst = this;
    builder.Start(ref inst);
    return new ValueTask<bool>(this, valueOrEndPromise.Version);
}
```

```C#
T Current => current;
```

The kick-off method and the initialization of the state machine for an async-iterator method follows those for regular iterator methods.
In particular, the synthesized `GetAsyncEnumerator()` method is like `GetEnuemrator()` except that it sets the initial state to to StateMachineStates.NotStartedStateMachine (-1):
```C#
IAsyncEnumerator<T> GetAsyncEnumerator()
{
    {StateMachineType} result;
    if (initialThreadId == /*managedThreadId*/ && state == StateMachineStates.FinishedStateMachine)
    {
        state = StateMachineStates.NotStartedStateMachine;
        result = this;
    }
    else
    {
        result = new {StateMachineType}(StateMachineStates.NotStartedStateMachine);
    }
    /* copy all of the parameter proxies */
}
```
For a discussion of the threadID check, see https://github.com/dotnet/corefx/issues/3481

Similarly, the kick-off method is much like those of regular iterator methods:
```C#
{
    {StateMachineType} result = new {StateMachineType}(StateMachineStates.FinishedStateMachine); // -2
    /* save parameters into parameter proxies */
    return result;
}
```

#### Disposal

Iterator and async-iterator methods need disposal (and therefore such gymnastics) because their execution steps are controlled by the caller and the caller could choose to dispose the enumerator in the middle of an iteration.
For example, `foreach (...) { if (...) break; }`
In contrast, async methods continue running autonomously until they are done. They are never left suspended in the middle of execution from the caller's perspective, so they don't need to be disposed.

`DisposeAsync()` should only be called when the method completed or is suspended on a `yield return`.
It sets a flag on the state machine ("dispose mode") and, if the method wasn't completed, resumes the execution from the current state.
The state machine already has the ability to resume execution from a given state, even if that state is located within a `try`.
When it is resumed, the execution in dispose mode jumps straight to the relevant `finally`.
The `finally` blocks may involve pauses and resumes, but only for `await` expressions. Dispose mode never runs into a `yield return` or `yield break`.
Once a `finally` block completes, the execution in dispose mode jumps to the next relevant `finally`, or the end of the method once we reach the top-level.

A `yield return` is lowered as:
```C#
_current = expression;
_state = <next_state>;
_valueOrEndPromise.SetResult(true);
goto <exit_label>;

// resuming from state=<next_state> will dispatch execution to this label
<next_state_label>: ;
this.state = cachedState = NotStartedStateMachine
if (disposeMode) /* jump to current finally or exit */
```

In the simple case (no `await` expressions in `finally), a `try/finally` is lowered as:
```C#
try
{
    ...
    finallyEntryLabel:
}
finally
{
    ...
}
if (disposeMode) /* jump to current finally or exit */
```

When a `finally` contains `await` expressions, it is extracted before async rewriting. In those cases, so we get:
```C#
try
{
    ...
    goto finallyEntryLabel;
}
catch (Exception e)
{
    ... save exception ...
}
finallyEntryLabel:
{
    ... original code from finally and additional handling for exception ...
}
```

```C#
ValueTask IAsyncDisposable.DisposeAsync()
{
    disposeMode = true;
    if (state == StateMachineStates.FinishedStateMachine ||
        state == StateMachineStates.NotStartedStateMachine)
    {
        return default;
    }
    _valueOrEndPromise.Reset();
    var inst = this;
    _builder.Start(ref inst);
    return new ValueTask(this, _valueOrEndPromise.Version);
}
```
