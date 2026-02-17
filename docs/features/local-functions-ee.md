# Local Function Support in the Expression Evaluator

## Problem

When debugging inside a method that contains local functions, the expression evaluator (EE) cannot call those local functions. For example:

```csharp
class C
{
    void F(int x)
    {
        int G(int y) { return y + 1; }
        int z = G(x); // breakpoint here
    }
}
```

If the debugger stops at the breakpoint and the user evaluates `G(1)`, the EE reports:

> error CS0103: The name 'G' does not exist in the current context

See [dotnet/roslyn#60358](https://github.com/dotnet/roslyn/issues/60358).

## Root cause

During compilation, local functions are lowered into ordinary methods with generated names on the containing type.
For example, `G` becomes `C.<F>g__G|0_0`.
The original method `F` in the emitted IL contains no trace of `G` as a local function — it simply calls `<F>g__G|0_0` directly.

The EE reconstructs the debugging context from metadata and PDB information. It already reconstructs:

- **Local variables**: from IL local signatures + PDB name/slot/scope data
- **Captured variables**: by analyzing display class fields reachable from method parameters
- **Import scopes**: from PDB import records
- **Hoisted local scopes**: from `StateMachineHoistedLocalScopes` custom debug info
- **Primary constructor context**: from `PrimaryConstructorInformationBlob` custom debug info

However, there is no mechanism to recover local function information. The EE's binder chain for method `F` includes:

1. `NamespaceBinder` — namespace imports
2. `InContainerBinder(C)` — type members
3. `PlaceholderLocalBinder` — pseudo-variables (`$exception`, etc.)
4. `SimpleLocalScopeBinder` — display class variables (outside)
5. `EEMethodBinder` — method parameters (via `InMethodBinder`)
6. `SimpleLocalScopeBinder` — locals (inside)
7. `ExecutableCodeBinder` — expression binding

No binder in this chain knows about local functions.
The `EEMethodBinder` creates an `InMethodBinder` that only looks up **parameters**.
In normal compilation, local functions are resolved by binders created from the method body's syntax tree,
but in the EE there is no syntax tree for the method body — only PE metadata.

## Design

We solve this in two parts:
1. Emit local function metadata in the PDB during compilation
2. Read and use that metadata in the EE during expression evaluation

### Part 1: PDB metadata — `LocalFunctionScopes`

#### Format

A new custom debug information record attached to the `MethodDefinitionHandle` of the containing user-defined method.
This uses the existing `CustomDebugInformation` table in the Portable PDB — no [spec](https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md) change is required.

```
GUID: PortableCustomDebugInfoKinds.LocalFunctionScopes (new GUID)
Parent: MethodDefinitionHandle (containing method, e.g., C.F)

Blob layout:
  Repeated until end of blob:
    SerString:             source name of the local function (e.g., "G")
                           (ECMA-335 SerString format: compressed integer byte length followed by UTF-8 bytes)
    CompressedInteger:     row ID of the lowered method in the MethodDef table
    UInt32:                scope start offset (IL offset where the function becomes visible)
    UInt32:                scope length (number of IL bytes the function is visible for)
```

#### Example

For the source:

```csharp
void F(int x)
{
    int G(int y) { return y + 1; }
    int z = G(x);
}
```

The compiler emits `C.<F>g__G|0_0` for `G`. The `LocalFunctionScopes` blob on method `F` would contain:

| Field | Value |
|---|---|
| Source name | `"G"` (SerString: length-prefixed UTF-8) |
| Lowered method RID | row ID of `<F>g__G|0_0` |
| Scope start | IL offset of `G`'s declaration point |
| Scope length | distance from declaration to end of enclosing block |

For nested local functions:

```csharp
void F(int x)
{
    int G(int y)
    {
        int H() => y + 1;  // H is inside G
        return H();
    }
    G(x);
}
```

`H` would appear in the `LocalFunctionScopes` for `<F>g__G|0_0` (not `F`), because when debugging inside `G`, the current frame is `<F>g__G|0_0`.

#### Scoping

Local functions in C# follow block scoping rules:

```csharp
void F()
{
    if (condition)
    {
        int G() => 1;  // G visible only within this block
        G();
    }
    // G is NOT visible here
}
```

The `(startOffset, length)` pair captures the IL range where the local function name is valid. This follows the same pattern used by:
- `LocalScope` table entries (for local variables)
- `StateMachineHoistedLocalScopes` (for hoisted locals in async/iterator methods)

The compiler already tracks these scopes during lowering (see `Closure Conversion.md`).
The start offset corresponds to the IL offset where the local function declaration appears,
and the length extends to the end of the enclosing lexical scope.
This matches how local variable scopes are determined — the `LocalScope` table already records exactly this range
for the display class local that the compiler introduces at the local function's declaration point.

**Open question**: Should we reuse the scope information from the existing `LocalScope` entries
for the display class locals associated with local functions, or emit independent scope ranges?
Reusing existing scope data would be simpler but couples the `LocalFunctionScopes`
to implementation details of display class lowering. Independent scope ranges are more robust.

#### Compatibility

| Scenario | Behavior |
|---|---|
| New PDB + old debugger | Old debugger doesn't recognize the GUID, skips the `CustomDebugInformation` row. No impact. |
| Old PDB + new debugger | `TryGetCustomDebugInformation` returns `false`. EE falls back to current behavior (local functions not callable). |
| Windows PDB (native) | Not supported. The native PDB reader in `ReadMethodDebugInfo` will return an empty local functions array, matching the precedent set by `PrimaryConstructorInformationBlob` (which hardcodes `isPrimaryConstructor: false` in the native path). Newer EE features are Portable PDB only. |

#### Edit and Continue (EnC)

No special EnC handling is required. The `LocalFunctionScopes` blob is emitted as part of `SerializeMethodDebugInfo`, which runs for every method body — baseline and delta alike.

When a method is re-emitted in a delta:
- The blob is re-emitted with a complete snapshot of the method's local functions (including any added in this edit, excluding any removed).
- The blob stores global method tokens, which resolve against the merged metadata the EE already has.
- The EE reads the correct delta PDB via `GetPortableDebugMetadataByVersion(methodVersion)`.

If a method is **not** re-emitted, the baseline blob is used, which correctly reflects the baseline state.

EnC should be validated during testing but requires no dedicated implementation work.

### Part 2: Expression Evaluator consumption

#### Data flow

```
Compiler emit
  └─ MetadataWriter.PortablePdb.cs: SerializeLocalFunctions()
       └─ Writes LocalFunctionScopes blob to CustomDebugInformation table

EE read
  └─ MethodDebugInfo.Portable.cs: ReadFromPortable()
       └─ ReadMethodCustomDebugInformation() decodes blob
            └─ Produces ImmutableArray<LocalFunctionInfo>
                 └─ Filtered by ilOffset in EvaluationContext.CreateMethodContext()

EE binding
  └─ CompilationContext constructor receives local function info
       └─ ExtendBinderChain() inserts EELocalFunctionBinder
            └─ Resolves "G" to the lowered method symbol
                 └─ EEMethodSymbol.GenerateMethodBody handles call rewriting
```

#### Step 1: Add `LocalFunctionInfo` record

A new type to carry per-local-function data through the pipeline:

```csharp
// In ExpressionEvaluator/Core/Source/ExpressionCompiler/
internal readonly struct LocalFunctionInfo
{
    public readonly string Name;               // Source name, e.g., "G"
    public readonly int LoweredMethodToken;    // MethodDef token of the lowered method
    public readonly int ScopeStartOffset;
    public readonly int ScopeLength;
}
```

#### Step 2: Extend `MethodDebugInfo`

Add a `LocalFunctions` field alongside the existing `HoistedLocalScopeRecords`, `LocalVariableNames`, etc.:

```csharp
public readonly ImmutableArray<LocalFunctionInfo> LocalFunctions;
```

Decoded in `ReadMethodCustomDebugInformation` (Portable PDB) and `ReadMethodDebugInfo` (native PDB) using the same pattern as hoisted local scopes.

#### Step 3: Filter by IL offset in `EvaluationContext.CreateMethodContext`

Same pattern used for `GetInScopeHoistedLocalIndices`:

```csharp
var inScopeLocalFunctions = debugInfo.LocalFunctions
    .Where(lf => ilOffset >= lf.ScopeStartOffset &&
                 ilOffset < lf.ScopeStartOffset + lf.ScopeLength)
    .ToImmutableArray();
```

Pass this to `CompilationContext`.

#### Step 4: Build local function method symbols in `CompilationContext`

In the `CompilationContext` constructor, for each in-scope local function:

1. Look up the `PEMethodSymbol` using the method token from the `LocalFunctionInfo`
2. Create an `EELocalFunctionMethodSymbol` wrapper that:
   - Exposes the original source name (e.g., `G`)
   - Hides the synthesized display class parameters (those with `IsDisplayClassParameter` == true)
   - Presents a user-facing signature matching the original source declaration

```csharp
// Conceptual wrapper
internal sealed class EELocalFunctionMethodSymbol : WrappedMethodSymbol
{
    private readonly string _sourceName;
    private readonly ImmutableArray<ParameterSymbol> _visibleParameters;  // excludes display class params

    public override string Name => _sourceName;
    public override ImmutableArray<ParameterSymbol> Parameters => _visibleParameters;
    // ... delegates everything else to the underlying PEMethodSymbol
}
```

#### Step 5: Add `EELocalFunctionBinder`

A new binder inserted into `ExtendBinderChain`, placed **above** `EEMethodBinder` (so local functions are visible in the method scope):

```csharp
internal sealed class EELocalFunctionBinder : Binder
{
    private readonly ImmutableArray<EELocalFunctionMethodSymbol> _localFunctions;

    internal override void LookupSymbolsInSingleBinder(
        LookupResult result, string name, int arity, ...)
    {
        foreach (var lf in _localFunctions)
        {
            if (lf.Name == name)
            {
                result.MergeEqual(originalBinder.CheckViability(lf, arity, options, ...));
            }
        }
    }
}
```

The binder chain in `ExtendBinderChain` becomes:

```
NamespaceBinder
  └─ InContainerBinder(C)                    // type members
      └─ PlaceholderLocalBinder              // $exception, etc.
          └─ SimpleLocalScopeBinder(outside)  // captured variables (outside)
              └─ EEMethodBinder              // method parameters
                  └─ EELocalFunctionBinder   // ← NEW: local function resolution
                      └─ SimpleLocalScopeBinder(inside)  // locals (inside)
                          └─ ExecutableCodeBinder
```

#### Step 6: Call rewriting

When the binder resolves `G(1)`, it produces a `BoundCall` targeting the `EELocalFunctionMethodSymbol`.
During lowering/emit, the call must be rewritten to target the actual lowered method, supplying the hidden display class arguments.

The display class instances are already available in `CompilationContext._displayClassVariables`. The rewriting would:

1. Map the `EELocalFunctionMethodSymbol` back to the underlying `PEMethodSymbol`
2. For each hidden display class parameter in the lowered method's signature, find the corresponding display class instance from `_displayClassVariables`
3. Prepend these as arguments to the call

This is conceptually the reverse of what `EEMethodBinder` already does.
`EEMethodBinder` hides display class parameters when looking up names; the call rewriter exposes them when generating the call.

## Test cases

The following scenarios should be tested (building on the existing `LocalFunctionTests.cs`):

1. **Basic call** — call a local function with no captures from the enclosing method
2. **Call with captured variables** — local function captures a parameter or local from the enclosing method
3. **Nested local functions** — call a local function from within another local function
4. **Generic local functions** — local function with its own type parameters
5. **Local function scoping** — local function not visible before its declaration or outside its block
6. **Overloaded local functions** — two local functions with the same name but different arities
7. **Local function in async method** — local function inside an async method (state machine)
8. **Local function in iterator** — local function inside an iterator method
9. **Local function calling another local function** — evaluate `G(H(1))` where both are local functions
10. **Assigning local function to delegate** — `Func<int,int> f = G;`

## Implementation plan

### Phase 1: Emit local function metadata (compiler)

1. Define `PortableCustomDebugInfoKinds.LocalFunctionScopes` GUID
2. Collect local function information during closure conversion (the lowered method and its source scope are already known at this point)
3. Plumb the information through `IMethodBody` to the PDB writer
4. Serialize the blob in `MetadataWriter.PortablePdb.cs` (Portable PDB only — no Windows PDB support, consistent with `PrimaryConstructorInformationBlob`)
5. Add PDB emit tests verifying the blob content

### Phase 2: Read local function metadata (EE)

6. Add `LocalFunctionInfo` record type
7. Extend `MethodDebugInfo` with `LocalFunctions` field
8. Decode the blob in `ReadMethodCustomDebugInformation` (Portable PDB path only; the native PDB path returns an empty array)
9. Filter by IL offset in `EvaluationContext.CreateMethodContext`
10. Pass to `CompilationContext`

### Phase 3: EE binding and invocation

11. Create `EELocalFunctionMethodSymbol` wrapper
12. Create `EELocalFunctionBinder`
13. Insert binder into `ExtendBinderChain`
14. Implement call rewriting to supply display class arguments
15. Update `CallLocalFunction` test to verify success
16. Add additional test cases from the list above

### Phase 4: Polish

17. Verify VB EE is unaffected (VB has no local functions)
18. Verify backward/forward compatibility with old PDBs
19. Verify Windows PDB graceful degradation (feature simply unavailable)
20. Verify EnC scenarios (add/remove/modify local functions between edits)

## Known limitations

### Generic local functions with inherited type parameters

When a local function is declared inside a generic method (or a method inside a generic class), the lowered PE method inherits type parameters from containing scopes via closure conversion's `TypeMap.ConcatMethodTypeParameters`. For example:

```csharp
void F<T>(T x)
{
    T G() { return x; }        // lowered as C.<F>g__G|0_0<T>()
    U H<U>(T a, U b) { ... }   // lowered as C.<F>g__H|0_1<T, U>()
}
```

The `EELocalFunctionMethodSymbol` currently exposes **all** PE method type parameters as the local function's own `TypeParameters`. This causes:

| Scenario | Failure | Root cause |
|---|---|---|
| `G()` — no source type params, containing method is generic | CS0411 (can't infer type args) | Symbol has arity 1 but source `G` has arity 0. Binder can't infer `T`. |
| `H<string>(x, "hello")` — local function + containing method type params | CS0305 (wrong number of type args) | Symbol has arity 2 but source `H<U>` has arity 1. |
| `G()` inside generic class `C<T>` | Crash: "Unexpected type parameter T owned by C\<T\>" | `ConcatMethodTypeParameters` only walks methods, so class type params aren't on the PE method — but the `EETypeParameterSymbol` encounters a class-owned type parameter during substitution. |

#### Root cause

The `EELocalFunctionMethodSymbol` needs to partition the PE method's type parameters into:
- **Inherited** (from containing methods) — should be invisible to the user
- **Own** (declared on the local function itself) — should be the symbol's `TypeParameters`

The natural approach is to compute `sourceArity = peMethod.Arity - containingFrame.Arity`, which gives the correct count. However, the return type and parameter types of the local function reference the inherited type parameters. To substitute them correctly, the `TypeMap` needs to map inherited PE type params to the EE method's alpha-renamed type parameters — but the `EELocalFunctionMethodSymbol` is constructed **before** the `EEMethodSymbol` exists (in `CompilationContext`'s constructor, line 131), creating a chicken-and-egg problem.

#### Options considered

- **Option A: Restructure initialization order** — Move `GetLocalFunctionMethodSymbols` to run after `EEMethodSymbol` is created, then pass the EE method's type parameters to the `EELocalFunctionMethodSymbol` constructor. Requires refactoring the `CompilationContext`/`EEMethodSymbol` initialization flow.

- **Option B: Two-phase initialization** — Create `EELocalFunctionMethodSymbol` with a lazy/deferred type map that gets filled in when `EEMethodSymbol` becomes available.

- **Option C: Store source arity in PDB** — Extend `LocalFunctionScope` to include the original source arity. Cleanest long-term but requires a PDB format addition and backward compatibility handling.

This is tracked by the TODO2 comments in tests `LocalFunction_38`, `LocalFunction_39`, and `LocalFunction_40`.
