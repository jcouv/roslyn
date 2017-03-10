# Reference assemblies

Reference assemblies are metadata-only assemblies with the minimum amount of metadata to preserve the compile-time behavior of consumers.

## Definition of ref assemblies
The definition of what goes into ref assemblies is incremental, with the starting point of metadata-only assemblies (which simply have their method bodies removed) and then removing un-necessary metadata.



- ReferenceAssemblyAttribute 
- private types and members
- internal types and members
- structs with only private members
- Non-public attributes on public APIs 
- effect on diagnostics 
- error codes

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

Open question: can `csc.exe` return an error code but still produce a ref assembly (and not produce its primary outputs)?




### CscTask/CoreCompile

### CodeAnalysis APIs
It is already possible to produce metadata-only assemblies by using `EmitOptions.EmitMetadataOnly`, which is used in IDE scenarios with cross-language dependencies.

We will need to expose another flag (TBD) for filtering out data that is un-necessary for ref assemblies.

## Open questions
- ref assemblies and NoPia
- `/refout` and `/addmodule`

## Related issues
- Produce ref assemblies from command-line and msbuild (https://github.com/dotnet/roslyn/issues/2184)
- Refine what is in reference assemblies and what diagnostics prevent generating one (https://github.com/dotnet/roslyn/issues/17612)
- [Are private members part of the API surface?](http://blog.paranoidcoding.com/2016/02/15/are-private-members-api-surface.html)
