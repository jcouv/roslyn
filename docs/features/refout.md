# Producing reference assemblies

Ref assemblies should have the minimum amount of stuff to preserve the compile-time behavior of consumers. Some metadata should not be emitted in ref assemblies and some diagnostics should not affect emitting ref assemblies.


The compiler already supports emitting "metadata-only" assemblies. This is used by the IDE for cross-language dependencies. Those include private members, but do not include any method bodies.

We will be introducing a second concept, which is "reference assemblies" (also called skeleton assemblies). Those are a further stripped down versions metadata-only assemblies (they won't include private types or members, for instance). They will be used for build scenarios. Producing ref assemblies will be driven by the /refonly and /refout parameters.
The guiding principle of ref assemblies is that they should have the minimum amount of stuff to preserve the compile-time behavior of consumers.

But, as a starting point (to unblock work on msbuild), we are using metadata-only assemblies as a close approximation of ref assemblies. That gives us many of the benefits of ref assemblies at much lower cost. As we narrow down what is included in ref assemblies (see #17612), the two concepts will diverge. When they do, we will likely need specific emit or compilation options (TBD) to indicate that we're producing ref assemblies.



ReferenceAssemblyAttribute 

private types and members
internal types and members
structs with only private members
Non-public attributes on public APIs 
doc comment for every API that goes into the primary output.
effect on diagnostics 


# Related issues
- Produce ref assemblies from command-line and msbuild (https://github.com/dotnet/roslyn/issues/2184)
- Refine what is in reference assemblies and what diagnostics prevent generating one (https://github.com/dotnet/roslyn/issues/17612)
