# Reference assemblies

Reference assemblies are metadata-only assemblies with the minimum amount of metadata to preserve the compile-time behavior of consumers.

## Scenarios
There are 4 scenarios:

1. The traditional one, where an assembly is emitted as the primary output (`/out` command-line parameter, or `peStream` parameter in `Compilation.Emit` APIs).
2. The IDE scenario, where the metadata-only assembly is emitted (via `Emit` API), still as the primary output. Later on, the IDE is interested to get metadata-only assemblies even when there are errors in the compilation.
3. The CoreFX scenario, where only the ref assembly is emitted, still as the primary output (`/refonly` command-line parameter) 
4. The MSBuild scenario, which is the new scenario, where both a real assembly is emitted as the primary output, and a ref assembly is emitted as the secondary output (`/refout` command-line parameter, or `metadataPeStream` parameter in `Emit`).


## Progression
1. Add `/refout` and `/refonly` command-line parameters (for MSBuild)
2. Strip out all private members (using the `EmitOptions.IncludePrivateMembers`), with some caveats:
    - structs with private fields: ref case, struct case, generic case
3. If there are no `InternalsVisibleTo` attributes, do the same for internal members
---------------------
4. Produce ref assemblies even when there are errors outside method bodies
5. Produce "public ref assemblies"


## Definition of ref assemblies
The definition of what goes into ref assemblies is incremental, with the starting point of metadata-only assemblies (which simply have their method bodies removed) and then removing un-necessary metadata.



- ReferenceAssemblyAttribute 
- private types and members
- internal types and members
- structs with only private members
- Non-public attributes on public APIs (emit attribute based on accessibility rule)
- effect on diagnostics 
- error codes
structs with private fields


controlling internals (producing public ref assemblies)
tolerate methods with no bodies in source code (for CoreFX)

 some diagnostics should not affect emitting ref assemblies.
 
## API changes

### Command-line
Two command-line parameters will be added:
- `/refout`
- `/refonly`

The `/refout` parameter specifies a file path where the ref assembly should be output.

The `/refonly` parameter is a flag that indicates that a ref assembly should be output instead of an implementation assembly. 
The `/refonly` parameter is not allowed together with the `/refout` parameter, as it doesn't make sense to have both the primary and secondary outputs be ref assemblies. Also, the `/refonly` parameter silently disables outputting PDBs, as ref assemblies cannot be executed.

When the compiler produces documentation, the contents produced will match the APIs that go into the primary output. In other words, the documentation will be filtered down when using the `/refonly` parameter.

The compilation from the command-line will either produce both assemblies (implementation and ref) or neither.


### CscTask/CoreCompile

### CodeAnalysis APIs
It is already possible to produce metadata-only assemblies by using `EmitOptions.EmitMetadataOnly`, which is used in IDE scenarios with cross-language dependencies.
The compiler will be updated to honour the `EmitOptions.IncludePrivateMembers` flag as well. When combined with `EmitMetadataOnly` in `Emit`, a ref assembly will be produced.
Later on, the `EmitOptions.TolerateErrors` flag will allow emitting error types as well.


## Open questions
- ref assemblies and NoPia
- `/refout` and `/addmodule`
- private fields in structs and interop code (https://github.com/dotnet/roslyn/pull/17558#issuecomment-287209362)
- how to handle types that are used in modreq?

## Related issues
- Produce ref assemblies from command-line and msbuild (https://github.com/dotnet/roslyn/issues/2184)
- Refine what is in reference assemblies and what diagnostics prevent generating one (https://github.com/dotnet/roslyn/issues/17612)
- [Are private members part of the API surface?](http://blog.paranoidcoding.com/2016/02/15/are-private-members-api-surface.html)
