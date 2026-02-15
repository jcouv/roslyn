// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Debugging;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.PDB
{
    public class PDBLocalFunctionTests : CSharpPDBTestBase
    {
        /// <summary>
        /// Reads the LocalFunctionMap custom debug information for the given method.
        /// </summary>
        private static ImmutableArray<(string name, string loweredMethodName, int startOffset, int length)>
            ReadLocalFunctionMap(Compilation compilation, string containingMethodName)
        {
            var pdbStream = new MemoryStream();
            var peBlob = compilation.EmitToArray(
                EmitOptions.Default.WithDebugInformationFormat(DebugInformationFormat.PortablePdb),
                pdbStream: pdbStream);

            using var peReader = new PEReader(peBlob);
            using var pdbMetadata = new PinnedMetadata(pdbStream.ToImmutable());

            var mdReader = peReader.GetMetadataReader();
            var pdbReader = pdbMetadata.Reader;

            var methodHandle = mdReader.MethodDefinitions
                .Single(m => mdReader.GetString(mdReader.GetMethodDefinition(m).Name) == containingMethodName);

            var cdiHandle = pdbReader.GetCustomDebugInformation(methodHandle)
                .SingleOrDefault(h => pdbReader.GetGuid(pdbReader.GetCustomDebugInformation(h).Kind) == PortableCustomDebugInfoKinds.LocalFunctionScopes);

            if (cdiHandle.IsNil)
            {
                return default;
            }

            var cdi = pdbReader.GetCustomDebugInformation(cdiHandle);
            var blobReader = pdbReader.GetBlobReader(cdi.Value);
            var entries = ArrayBuilder<(string name, string loweredMethodName, int startOffset, int length)>.GetInstance();
            while (blobReader.RemainingBytes > 0)
            {
                var name = blobReader.ReadSerializedString();
                Assert.NotNull(name);
                var rid = blobReader.ReadCompressedInteger();
                var startOffset = blobReader.ReadUInt32();
                var length = blobReader.ReadUInt32();
                var loweredHandle = MetadataTokens.MethodDefinitionHandle(rid);
                var loweredDef = mdReader.GetMethodDefinition(loweredHandle);
                var loweredMethodName = mdReader.GetString(loweredDef.Name);
                Assert.Contains($"g__{name}", loweredMethodName);
                entries.Add((name, loweredMethodName, (int)startOffset, (int)length));
            }

            return entries.ToImmutableAndFree();
        }

        [Fact]
        public void ClosuresInCtor()
        {
            var source = WithWindowsLineBreaks(@"
using System;

class B
{
    public B(Func<int> f) { }
}

class C : B
{
    int r;

    public C(int a, int b) : base(() => a) 
    {
        int c = 1;
        int f() => b;
        int g() => f();
        int h() => c;
        r = g() + h();
    }
}
");

            var c = CreateCompilationWithMscorlib40AndSystemCore(source, options: TestOptions.DebugDll);
            c.VerifyDiagnostics();

            c.VerifyPdb(@"
<symbols>
  <files>
    <file id=""1"" name="""" language=""C#"" />
  </files>
  <methods>
    <method containingType=""B"" name="".ctor"" parameterNames=""f"">
      <customDebugInfo>
        <using>
          <namespace usingCount=""1"" />
        </using>
      </customDebugInfo>
      <sequencePoints>
        <entry offset=""0x0"" startLine=""6"" startColumn=""5"" endLine=""6"" endColumn=""26"" document=""1"" />
        <entry offset=""0x7"" startLine=""6"" startColumn=""27"" endLine=""6"" endColumn=""28"" document=""1"" />
        <entry offset=""0x8"" startLine=""6"" startColumn=""29"" endLine=""6"" endColumn=""30"" document=""1"" />
      </sequencePoints>
      <scope startOffset=""0x0"" endOffset=""0x9"">
        <namespace name=""System"" />
      </scope>
    </method>
    <method containingType=""C"" name="".ctor"" parameterNames=""a, b"">
      <customDebugInfo>
        <forward declaringType=""B"" methodName="".ctor"" parameterNames=""f"" />
        <encLocalSlotMap>
          <slot kind=""30"" offset=""-1"" />
          <slot kind=""30"" offset=""0"" />
        </encLocalSlotMap>
        <encLambdaMap>
          <methodOrdinal>1</methodOrdinal>
          <closure offset=""-1"" />
          <closure offset=""0"" />
          <lambda offset=""-2"" closure=""0"" />
          <lambda offset=""42"" closure=""0"" />
          <lambda offset=""65"" closure=""0"" />
          <lambda offset=""90"" />
        </encLambdaMap>
      </customDebugInfo>
      <sequencePoints>
        <entry offset=""0x0"" hidden=""true"" document=""1"" />
        <entry offset=""0x14"" startLine=""13"" startColumn=""30"" endLine=""13"" endColumn=""43"" document=""1"" />
        <entry offset=""0x27"" startLine=""14"" startColumn=""5"" endLine=""14"" endColumn=""6"" document=""1"" />
        <entry offset=""0x28"" startLine=""15"" startColumn=""9"" endLine=""15"" endColumn=""19"" document=""1"" />
        <entry offset=""0x33"" startLine=""19"" startColumn=""9"" endLine=""19"" endColumn=""23"" document=""1"" />
        <entry offset=""0x47"" startLine=""20"" startColumn=""5"" endLine=""20"" endColumn=""6"" document=""1"" />
      </sequencePoints>
      <scope startOffset=""0x0"" endOffset=""0x48"">
        <local name=""CS$&lt;&gt;8__locals0"" il_index=""0"" il_start=""0x0"" il_end=""0x48"" attributes=""0"" />
        <scope startOffset=""0x27"" endOffset=""0x48"">
          <local name=""CS$&lt;&gt;8__locals1"" il_index=""1"" il_start=""0x27"" il_end=""0x48"" attributes=""0"" />
        </scope>
      </scope>
    </method>
    <method containingType=""C"" name=""&lt;.ctor&gt;g__h|1_3"">
      <customDebugInfo>
        <forward declaringType=""B"" methodName="".ctor"" parameterNames=""f"" />
      </customDebugInfo>
      <sequencePoints>
        <entry offset=""0x0"" startLine=""18"" startColumn=""20"" endLine=""18"" endColumn=""21"" document=""1"" />
      </sequencePoints>
    </method>
    <method containingType=""C+&lt;&gt;c__DisplayClass1_0"" name=""&lt;.ctor&gt;b__0"">
      <customDebugInfo>
        <forward declaringType=""B"" methodName="".ctor"" parameterNames=""f"" />
      </customDebugInfo>
      <sequencePoints>
        <entry offset=""0x0"" startLine=""13"" startColumn=""41"" endLine=""13"" endColumn=""42"" document=""1"" />
      </sequencePoints>
    </method>
    <method containingType=""C+&lt;&gt;c__DisplayClass1_0"" name=""&lt;.ctor&gt;g__f|1"">
      <customDebugInfo>
        <forward declaringType=""B"" methodName="".ctor"" parameterNames=""f"" />
      </customDebugInfo>
      <sequencePoints>
        <entry offset=""0x0"" startLine=""16"" startColumn=""20"" endLine=""16"" endColumn=""21"" document=""1"" />
      </sequencePoints>
    </method>
    <method containingType=""C+&lt;&gt;c__DisplayClass1_0"" name=""&lt;.ctor&gt;g__g|2"">
      <customDebugInfo>
        <forward declaringType=""B"" methodName="".ctor"" parameterNames=""f"" />
      </customDebugInfo>
      <sequencePoints>
        <entry offset=""0x0"" startLine=""17"" startColumn=""20"" endLine=""17"" endColumn=""23"" document=""1"" />
      </sequencePoints>
    </method>
  </methods>
</symbols>
");
        }

        [Fact]
        public void ForEachStatement_Array()
        {
            string source = WithWindowsLineBreaks(@"
using System;

class C
{
    void G(Func<int, int> f) {}

    void F()                       
    {                              
        foreach (int x0 in new[] { 1 })  // Group #0             
        {                                // Group #1
            int x1 = 0;                  
            int f0(int a) => x0;
            int f1(int a) => x1;
            G(f0);   
            G(f1);
        }
    }
}");
            var c = CreateCompilationWithMscorlib40AndSystemCore(source, options: TestOptions.DebugDll);
            c.VerifyDiagnostics();

            // note that the two closures have a different syntax offset
            c.VerifyPdb("C.F", @"
<symbols>
  <files>
    <file id=""1"" name="""" language=""C#"" />
  </files>
  <methods>
    <method containingType=""C"" name=""F"">
      <customDebugInfo>
        <forward declaringType=""C"" methodName=""G"" parameterNames=""f"" />
        <encLocalSlotMap>
          <slot kind=""6"" offset=""41"" />
          <slot kind=""8"" offset=""41"" />
          <slot kind=""30"" offset=""41"" />
          <slot kind=""30"" offset=""108"" />
        </encLocalSlotMap>
        <encLambdaMap>
          <methodOrdinal>1</methodOrdinal>
          <closure offset=""41"" />
          <closure offset=""108"" />
          <lambda offset=""226"" closure=""0"" />
          <lambda offset=""260"" closure=""1"" />
        </encLambdaMap>
      </customDebugInfo>
      <sequencePoints>
        <entry offset=""0x0"" startLine=""9"" startColumn=""5"" endLine=""9"" endColumn=""6"" document=""1"" />
        <entry offset=""0x1"" startLine=""10"" startColumn=""9"" endLine=""10"" endColumn=""16"" document=""1"" />
        <entry offset=""0x2"" startLine=""10"" startColumn=""28"" endLine=""10"" endColumn=""39"" document=""1"" />
        <entry offset=""0xf"" hidden=""true"" document=""1"" />
        <entry offset=""0x11"" hidden=""true"" document=""1"" />
        <entry offset=""0x17"" startLine=""10"" startColumn=""18"" endLine=""10"" endColumn=""24"" document=""1"" />
        <entry offset=""0x20"" hidden=""true"" document=""1"" />
        <entry offset=""0x26"" startLine=""11"" startColumn=""9"" endLine=""11"" endColumn=""10"" document=""1"" />
        <entry offset=""0x27"" startLine=""12"" startColumn=""13"" endLine=""12"" endColumn=""24"" document=""1"" />
        <entry offset=""0x30"" startLine=""15"" startColumn=""13"" endLine=""15"" endColumn=""19"" document=""1"" />
        <entry offset=""0x43"" startLine=""16"" startColumn=""13"" endLine=""16"" endColumn=""19"" document=""1"" />
        <entry offset=""0x56"" startLine=""17"" startColumn=""9"" endLine=""17"" endColumn=""10"" document=""1"" />
        <entry offset=""0x57"" hidden=""true"" document=""1"" />
        <entry offset=""0x5b"" startLine=""10"" startColumn=""25"" endLine=""10"" endColumn=""27"" document=""1"" />
        <entry offset=""0x61"" startLine=""18"" startColumn=""5"" endLine=""18"" endColumn=""6"" document=""1"" />
      </sequencePoints>
      <scope startOffset=""0x0"" endOffset=""0x62"">
        <scope startOffset=""0x11"" endOffset=""0x57"">
          <local name=""CS$&lt;&gt;8__locals0"" il_index=""2"" il_start=""0x11"" il_end=""0x57"" attributes=""0"" />
          <scope startOffset=""0x20"" endOffset=""0x57"">
            <local name=""CS$&lt;&gt;8__locals1"" il_index=""3"" il_start=""0x20"" il_end=""0x57"" attributes=""0"" />
          </scope>
        </scope>
      </scope>
    </method>
  </methods>
</symbols>
");
        }

        [Fact]
        public void ForStatement1()
        {
            string source = WithWindowsLineBreaks(@"
using System;

class C
{
    bool G(Func<int, int> f) => true;

    void F()                       
    {                              
        for (int x0 = 0, x1 = 0; G(a => x0) && G(a => x1);)
        {
            int x2 = 0;
            int f(int a) => x2;
            G(f); 
        }
    }
}");
            var c = CreateCompilationWithMscorlib40AndSystemCore(source, options: TestOptions.DebugDll);
            c.VerifyDiagnostics();

            // note that the two closures have a different syntax offset
            c.VerifyPdb("C.F", @"
<symbols>
  <files>
    <file id=""1"" name="""" language=""C#"" />
  </files>
  <methods>
    <method containingType=""C"" name=""F"">
      <customDebugInfo>
        <forward declaringType=""C"" methodName=""G"" parameterNames=""f"" />
        <encLocalSlotMap>
          <slot kind=""30"" offset=""41"" />
          <slot kind=""30"" offset=""102"" />
          <slot kind=""1"" offset=""41"" />
        </encLocalSlotMap>
        <encLambdaMap>
          <methodOrdinal>1</methodOrdinal>
          <closure offset=""41"" />
          <closure offset=""102"" />
          <lambda offset=""158"" closure=""1"" />
          <lambda offset=""73"" closure=""0"" />
          <lambda offset=""87"" closure=""0"" />
        </encLambdaMap>
      </customDebugInfo>
      <sequencePoints>
        <entry offset=""0x0"" startLine=""9"" startColumn=""5"" endLine=""9"" endColumn=""6"" document=""1"" />
        <entry offset=""0x1"" hidden=""true"" document=""1"" />
        <entry offset=""0x7"" startLine=""10"" startColumn=""14"" endLine=""10"" endColumn=""24"" document=""1"" />
        <entry offset=""0xe"" startLine=""10"" startColumn=""26"" endLine=""10"" endColumn=""32"" document=""1"" />
        <entry offset=""0x15"" hidden=""true"" document=""1"" />
        <entry offset=""0x17"" hidden=""true"" document=""1"" />
        <entry offset=""0x1d"" startLine=""11"" startColumn=""9"" endLine=""11"" endColumn=""10"" document=""1"" />
        <entry offset=""0x1e"" startLine=""12"" startColumn=""13"" endLine=""12"" endColumn=""24"" document=""1"" />
        <entry offset=""0x26"" startLine=""14"" startColumn=""13"" endLine=""14"" endColumn=""18"" document=""1"" />
        <entry offset=""0x39"" startLine=""15"" startColumn=""9"" endLine=""15"" endColumn=""10"" document=""1"" />
        <entry offset=""0x3a"" startLine=""10"" startColumn=""34"" endLine=""10"" endColumn=""58"" document=""1"" />
        <entry offset=""0x64"" hidden=""true"" document=""1"" />
        <entry offset=""0x67"" startLine=""16"" startColumn=""5"" endLine=""16"" endColumn=""6"" document=""1"" />
      </sequencePoints>
      <scope startOffset=""0x0"" endOffset=""0x68"">
        <scope startOffset=""0x1"" endOffset=""0x67"">
          <local name=""CS$&lt;&gt;8__locals0"" il_index=""0"" il_start=""0x1"" il_end=""0x67"" attributes=""0"" />
          <scope startOffset=""0x17"" endOffset=""0x3a"">
            <local name=""CS$&lt;&gt;8__locals1"" il_index=""1"" il_start=""0x17"" il_end=""0x3a"" attributes=""0"" />
          </scope>
        </scope>
      </scope>
    </method>
  </methods>
</symbols>
");
        }

        [Fact]
        public void SwitchStatement1()
        {
            var source = WithWindowsLineBreaks(@"
using System;

class C
{
    bool G(Func<int> f) => true;

    int a = 1;

    void F()
    {
        int x2 = 1;
        int f2() => x2;
        G(f2);

        switch (a)
        {
            case 1:
                int x0 = 1;
                int f0() => x0;
                G(f0);
                break;

            case 2:
                int x1 = 1;
                int f1() => x1;
                G(f1);
                break;
        }
    }
}
");
            var c = CreateCompilationWithMscorlib40AndSystemCore(source, options: TestOptions.DebugDll);
            c.VerifyDiagnostics();

            c.VerifyPdb("C.F", @"
<symbols>
  <files>
    <file id=""1"" name="""" language=""C#"" />
  </files>
  <methods>
    <method containingType=""C"" name=""F"">
      <customDebugInfo>
        <forward declaringType=""C"" methodName=""G"" parameterNames=""f"" />
        <encLocalSlotMap>
          <slot kind=""30"" offset=""0"" />
          <slot kind=""30"" offset=""75"" />
          <slot kind=""35"" offset=""75"" />
          <slot kind=""1"" offset=""75"" />
        </encLocalSlotMap>
        <encLambdaMap>
          <methodOrdinal>2</methodOrdinal>
          <closure offset=""0"" />
          <closure offset=""75"" />
          <lambda offset=""44"" closure=""0"" />
          <lambda offset=""176"" closure=""1"" />
          <lambda offset=""309"" closure=""1"" />
        </encLambdaMap>
      </customDebugInfo>
      <sequencePoints>
        <entry offset=""0x0"" hidden=""true"" document=""1"" />
        <entry offset=""0x6"" startLine=""11"" startColumn=""5"" endLine=""11"" endColumn=""6"" document=""1"" />
        <entry offset=""0x7"" startLine=""12"" startColumn=""9"" endLine=""12"" endColumn=""20"" document=""1"" />
        <entry offset=""0xf"" startLine=""14"" startColumn=""9"" endLine=""14"" endColumn=""15"" document=""1"" />
        <entry offset=""0x22"" hidden=""true"" document=""1"" />
        <entry offset=""0x2f"" hidden=""true"" document=""1"" />
        <entry offset=""0x31"" hidden=""true"" document=""1"" />
        <entry offset=""0x3d"" startLine=""19"" startColumn=""17"" endLine=""19"" endColumn=""28"" document=""1"" />
        <entry offset=""0x45"" startLine=""21"" startColumn=""17"" endLine=""21"" endColumn=""23"" document=""1"" />
        <entry offset=""0x58"" startLine=""22"" startColumn=""17"" endLine=""22"" endColumn=""23"" document=""1"" />
        <entry offset=""0x5a"" startLine=""25"" startColumn=""17"" endLine=""25"" endColumn=""28"" document=""1"" />
        <entry offset=""0x62"" startLine=""27"" startColumn=""17"" endLine=""27"" endColumn=""23"" document=""1"" />
        <entry offset=""0x75"" startLine=""28"" startColumn=""17"" endLine=""28"" endColumn=""23"" document=""1"" />
        <entry offset=""0x77"" startLine=""30"" startColumn=""5"" endLine=""30"" endColumn=""6"" document=""1"" />
      </sequencePoints>
      <scope startOffset=""0x0"" endOffset=""0x78"">
        <local name=""CS$&lt;&gt;8__locals0"" il_index=""0"" il_start=""0x0"" il_end=""0x78"" attributes=""0"" />
        <scope startOffset=""0x22"" endOffset=""0x77"">
          <local name=""CS$&lt;&gt;8__locals1"" il_index=""1"" il_start=""0x22"" il_end=""0x77"" attributes=""0"" />
        </scope>
      </scope>
    </method>
  </methods>
</symbols>");
        }

        [Fact]
        public void UsingStatement1()
        {
            string source = WithWindowsLineBreaks(@"
using System;

class C
{
    static bool G<T>(Func<T> f) => true;
    static int F(object a, object b) => 1;
    static IDisposable D() => null;
    
    static void F()                       
    {                              
        using (IDisposable x0 = D(), y0 = D())
        {
            int x1 = 1;
        
            object f0() => x0;
            object f1() => x1;
            object g0() => y0;
            G(f0);
            G(g0);
            G(f1);
        }
    }
}");
            var c = CreateCompilationWithMscorlib40AndSystemCore(source, options: TestOptions.DebugDll);
            c.VerifyDiagnostics();

            // note that the two closures have a different syntax offset
            c.VerifyPdb("C.F", @"
<symbols>
  <files>
    <file id=""1"" name="""" language=""C#"" />
  </files>
  <methods>
    <method containingType=""C"" name=""F"" parameterNames=""a, b"">
      <customDebugInfo>
        <forward declaringType=""C"" methodName=""G"" parameterNames=""f"" />
      </customDebugInfo>
      <sequencePoints>
        <entry offset=""0x0"" startLine=""7"" startColumn=""41"" endLine=""7"" endColumn=""42"" document=""1"" />
      </sequencePoints>
    </method>
    <method containingType=""C"" name=""F"">
      <customDebugInfo>
        <forward declaringType=""C"" methodName=""G"" parameterNames=""f"" />
        <encLocalSlotMap>
          <slot kind=""30"" offset=""41"" />
          <slot kind=""30"" offset=""89"" />
        </encLocalSlotMap>
        <encLambdaMap>
          <methodOrdinal>3</methodOrdinal>
          <closure offset=""41"" />
          <closure offset=""89"" />
          <lambda offset=""154"" closure=""0"" />
          <lambda offset=""186"" closure=""1"" />
          <lambda offset=""218"" closure=""0"" />
        </encLambdaMap>
      </customDebugInfo>
      <sequencePoints>
        <entry offset=""0x0"" startLine=""11"" startColumn=""5"" endLine=""11"" endColumn=""6"" document=""1"" />
        <entry offset=""0x1"" hidden=""true"" document=""1"" />
        <entry offset=""0x7"" startLine=""12"" startColumn=""16"" endLine=""12"" endColumn=""36"" document=""1"" />
        <entry offset=""0x12"" startLine=""12"" startColumn=""38"" endLine=""12"" endColumn=""46"" document=""1"" />
        <entry offset=""0x1d"" hidden=""true"" document=""1"" />
        <entry offset=""0x23"" startLine=""13"" startColumn=""9"" endLine=""13"" endColumn=""10"" document=""1"" />
        <entry offset=""0x24"" startLine=""14"" startColumn=""13"" endLine=""14"" endColumn=""24"" document=""1"" />
        <entry offset=""0x2e"" startLine=""19"" startColumn=""13"" endLine=""19"" endColumn=""19"" document=""1"" />
        <entry offset=""0x40"" startLine=""20"" startColumn=""13"" endLine=""20"" endColumn=""19"" document=""1"" />
        <entry offset=""0x52"" startLine=""21"" startColumn=""13"" endLine=""21"" endColumn=""19"" document=""1"" />
        <entry offset=""0x64"" startLine=""22"" startColumn=""9"" endLine=""22"" endColumn=""10"" document=""1"" />
        <entry offset=""0x67"" hidden=""true"" document=""1"" />
        <entry offset=""0x7b"" hidden=""true"" document=""1"" />
        <entry offset=""0x7c"" hidden=""true"" document=""1"" />
        <entry offset=""0x7e"" hidden=""true"" document=""1"" />
        <entry offset=""0x92"" hidden=""true"" document=""1"" />
        <entry offset=""0x93"" startLine=""23"" startColumn=""5"" endLine=""23"" endColumn=""6"" document=""1"" />
      </sequencePoints>
      <scope startOffset=""0x0"" endOffset=""0x94"">
        <scope startOffset=""0x1"" endOffset=""0x93"">
          <local name=""CS$&lt;&gt;8__locals0"" il_index=""0"" il_start=""0x1"" il_end=""0x93"" attributes=""0"" />
          <scope startOffset=""0x1d"" endOffset=""0x65"">
            <local name=""CS$&lt;&gt;8__locals1"" il_index=""1"" il_start=""0x1d"" il_end=""0x65"" attributes=""0"" />
          </scope>
        </scope>
      </scope>
    </method>
  </methods>
</symbols>
");
        }

        [Fact]
        public void LocalFunctionScopes_01()
        {
            var source = """
class C
{
    void F(int x)
    {
        int G(int y) { return y + 1; }
        int z = G(x);
    }
}
""";

            var verifier = CompileAndVerify(source, options: TestOptions.DebugDll);
            verifier.VerifyIL("C.F", """
{
  // Code size       10 (0xa)
  .maxstack  1
  .locals init (int V_0) //z
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  ldarg.1
  IL_0003:  call       "int C.<F>g__G|0_0(int)"
  IL_0008:  stloc.0
  IL_0009:  ret
}
""");

            var entries = ReadLocalFunctionMap(verifier.Compilation, "F");
            var entry = Assert.Single(entries);
            Assert.Equal("G", entry.name);
            Assert.Equal("<F>g__G|0_0", entry.loweredMethodName);
            Assert.Equal(0x0, entry.startOffset);
            Assert.Equal(0x9, entry.startOffset + entry.length - 1); // inclusive end
        }

        [Fact]
        public void LocalFunctionScopes_02()
        {
            // multiple local functions
            var source = """
class C
{
    void F()
    {
        int G() => 1;
        int H() => 2;
        int I() => 3;
        _ = G() + H() + I();
    }
}
""";

            var verifier = CompileAndVerify(source, options: TestOptions.DebugDll);
            verifier.VerifyIL("C.F", """
{
  // Code size       23 (0x17)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  nop
  IL_0003:  nop
  IL_0004:  call       "int C.<F>g__G|0_0()"
  IL_0009:  pop
  IL_000a:  call       "int C.<F>g__H|0_1()"
  IL_000f:  pop
  IL_0010:  call       "int C.<F>g__I|0_2()"
  IL_0015:  pop
  IL_0016:  ret
}
""");

            var entries = ReadLocalFunctionMap(verifier.Compilation, "F");
            Assert.Equal(3, entries.Length);

            Assert.Equal("G", entries[0].name);
            Assert.Equal("<F>g__G|0_0", entries[0].loweredMethodName);
            Assert.Equal(0x0, entries[0].startOffset);
            Assert.Equal(0x16, entries[0].startOffset + entries[0].length - 1); // inclusive end

            Assert.Equal("H", entries[1].name);
            Assert.Equal("<F>g__H|0_1", entries[1].loweredMethodName);
            Assert.Equal(0x0, entries[1].startOffset);
            Assert.Equal(0x16, entries[1].startOffset + entries[1].length - 1); // inclusive end

            Assert.Equal("I", entries[2].name);
            Assert.Equal("<F>g__I|0_2", entries[2].loweredMethodName);
            Assert.Equal(0x0, entries[2].startOffset);
            Assert.Equal(0x16, entries[2].startOffset + entries[2].length - 1); // inclusive end
        }

        [Fact]
        public void LocalFunctionScopes_03()
        {
            // with captures
            var source = """
class C
{
    void F(int x)
    {
        int y = 10;
        int G() => x + y;
        _ = G();
    }
}
""";

            var verifier = CompileAndVerify(source, options: TestOptions.DebugDll);
            verifier.VerifyIL("C.F", """
{
  // Code size       28 (0x1c)
  .maxstack  2
  .locals init (C.<>c__DisplayClass0_0 V_0) //CS$<>8__locals0
  IL_0000:  ldloca.s   V_0
  IL_0002:  ldarg.1
  IL_0003:  stfld      "int C.<>c__DisplayClass0_0.x"
  IL_0008:  nop
  IL_0009:  ldloca.s   V_0
  IL_000b:  ldc.i4.s   10
  IL_000d:  stfld      "int C.<>c__DisplayClass0_0.y"
  IL_0012:  nop
  IL_0013:  ldloca.s   V_0
  IL_0015:  call       "int C.<F>g__G|0_0(ref C.<>c__DisplayClass0_0)"
  IL_001a:  pop
  IL_001b:  ret
}
""");

            var entries = ReadLocalFunctionMap(verifier.Compilation, "F");
            var entry = Assert.Single(entries);
            Assert.Equal("G", entry.name);
            Assert.Equal("<F>g__G|0_0", entry.loweredMethodName);
            Assert.Equal(0x0, entry.startOffset);
            Assert.Equal(0x1b, entry.startOffset + entry.length - 1); // inclusive end
        }

        [Fact]
        public void LocalFunctionScopes_04()
        {
            // no local function
            var source = """
class C
{
    void F(int x)
    {
        int z = x + 1;
    }
}
""";

            var verifier = CompileAndVerify(source, options: TestOptions.DebugDll);
            Assert.True(ReadLocalFunctionMap(verifier.Compilation, "F").IsDefault);
        }

        [Fact]
        public void LocalFunctionScopes_05()
        {
            // generic local function
            var source = """
class C
{
    void F()
    {
        T G<T>(T x) => x;
        _ = G(42);
    }
}
""";

            var verifier = CompileAndVerify(source, options: TestOptions.DebugDll);
            verifier.VerifyIL("C.F", """
{
  // Code size       11 (0xb)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  ldc.i4.s   42
  IL_0004:  call       "int C.<F>g__G|0_0<int>(int)"
  IL_0009:  pop
  IL_000a:  ret
}
""");

            var entries = ReadLocalFunctionMap(verifier.Compilation, "F");
            var entry = Assert.Single(entries);
            Assert.Equal("G", entry.name);
            Assert.Equal("<F>g__G|0_0", entry.loweredMethodName);
            Assert.Equal(0x0, entry.startOffset);
            Assert.Equal(0xa, entry.startOffset + entry.length - 1); // inclusive end
        }

        [Fact]
        public void LocalFunctionScopes_06()
        {
            // windows PDB
            var source = """
class C
{
    void F()
    {
        int G() => 1;
        _ = G();
    }
}
""";

            var c = CreateCompilation(source, options: TestOptions.DebugDll);
            c.VerifyDiagnostics();

            var pdbStream = new MemoryStream();
            c.EmitToArray(EmitOptions.Default.WithDebugInformationFormat(DebugInformationFormat.Pdb), pdbStream: pdbStream);

            // The LocalFunctionMap is only emitted to Portable PDBs.
            // We just verify the compilation succeeds without errors.
        }

        [Fact]
        public void LocalFunctionScopes_07()
        {
            // nested local function
            var source = """
class C
{
    void F()
    {
        int G()
        {
            int Inner() => 42;
            return Inner();
        }
        _ = G();
    }
}
""";

            var verifier = CompileAndVerify(source, options: TestOptions.DebugDll);

            // F's body only sees G (Inner is inside G's body, not F's)
            verifier.VerifyIL("C.F", """
{
  // Code size        9 (0x9)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  call       "int C.<F>g__G|0_0()"
  IL_0007:  pop
  IL_0008:  ret
}
""");

            var outerEntries = ReadLocalFunctionMap(verifier.Compilation, "F");
            var outerEntry = Assert.Single(outerEntries);
            Assert.Equal("G", outerEntry.name);
            Assert.Equal("<F>g__G|0_0", outerEntry.loweredMethodName);
            Assert.Equal(0x0, outerEntry.startOffset);
            Assert.Equal(0x8, outerEntry.startOffset + outerEntry.length - 1); // inclusive end

            // G's lowered method body sees Inner
            verifier.VerifyIL("C.<F>g__G|0_0", """
{
  // Code size       12 (0xc)
  .maxstack  1
  .locals init (int V_0)
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  call       "int C.<F>g__Inner|0_1()"
  IL_0007:  stloc.0
  IL_0008:  br.s       IL_000a
  IL_000a:  ldloc.0
  IL_000b:  ret
}
""");

            var innerEntries = ReadLocalFunctionMap(verifier.Compilation, "<F>g__G|0_0");
            var innerEntry = Assert.Single(innerEntries);
            Assert.Equal("Inner", innerEntry.name);
            Assert.Equal("<F>g__Inner|0_1", innerEntry.loweredMethodName);
            Assert.Equal(0x0, innerEntry.startOffset);
            Assert.Equal(0xb, innerEntry.startOffset + innerEntry.length - 1); // inclusive end
        }

        [Fact]
        public void LocalFunctionScopes_08()
        {
            // async
            var source = """
class C
{
    async System.Threading.Tasks.Task F(int x)
    {
        int G() => x + 1;
        await System.Threading.Tasks.Task.Yield();
        _ = G();
    }
}
""";

            var verifier = CompileAndVerify(source, options: TestOptions.DebugDll);

            Assert.True(ReadLocalFunctionMap(verifier.Compilation, "F").IsDefault);

            var entries = ReadLocalFunctionMap(verifier.Compilation, "MoveNext");
            var entry = Assert.Single(entries);
            Assert.Equal("G", entry.name);
            Assert.Contains("g__G", entry.loweredMethodName);
            Assert.True(entry.length > 0);
        }

        [Fact]
        public void LocalFunctionScopes_09()
        {
            // iterator
            var source = """
class C
{
    System.Collections.Generic.IEnumerable<int> F(int x)
    {
        int G() => x + 1;
        yield return G();
        yield return G() + 1;
    }
}
""";

            var verifier = CompileAndVerify(source, options: TestOptions.DebugDll);

            Assert.True(ReadLocalFunctionMap(verifier.Compilation, "F").IsDefault);

            var entries = ReadLocalFunctionMap(verifier.Compilation, "MoveNext");
            var entry = Assert.Single(entries);
            Assert.Equal("G", entry.name);
            Assert.Contains("g__G", entry.loweredMethodName);
            Assert.True(entry.length > 0);
        }

        [Fact]
        public void LocalFunctionScopes_10()
        {
            // async with runtime-async
            var source = """
class C
{
    async System.Threading.Tasks.Task F(int x)
    {
        int G() => x + 1;
        await System.Threading.Tasks.Task.Yield();
        _ = G();
    }
}
""";

            var comp = CreateRuntimeAsyncCompilation(source, TestOptions.DebugDll);

            var entries = ReadLocalFunctionMap(comp, "F");
            var entry = Assert.Single(entries);
            Assert.Equal("G", entry.name);
            Assert.Contains("g__G", entry.loweredMethodName);
            Assert.True(entry.length > 0);
        }

        [Fact]
        public void LocalFunctionScopes_11()
        {
            // async-iterator
            var source = """
class C
{
    async System.Collections.Generic.IAsyncEnumerable<int> F(int x)
    {
        int G() => x + 1;
        await System.Threading.Tasks.Task.Yield();
        yield return G();
        yield return G() + 1;
    }
}
""";

            var verifier = CompileAndVerify(source, targetFramework: TargetFramework.Net80, options: TestOptions.DebugDll, verify: Verification.Skipped);

            Assert.True(ReadLocalFunctionMap(verifier.Compilation, "F").IsDefault);

            var entries = ReadLocalFunctionMap(verifier.Compilation, "MoveNext");
            var entry = Assert.Single(entries);
            Assert.Equal("G", entry.name);
            Assert.Contains("g__G", entry.loweredMethodName);
            Assert.True(entry.length > 0);
        }

        [Fact]
        public void LocalFunctionScopes_12()
        {
            // async-iterator with runtime-async
            var source = """
class C
{
    async System.Collections.Generic.IAsyncEnumerable<int> F(int x)
    {
        int G() => x + 1;
        await System.Threading.Tasks.Task.Yield();
        yield return G();
        yield return G() + 1;
    }
}
""";

            var comp = CreateRuntimeAsyncCompilation(source, TestOptions.DebugDll);

            Assert.True(ReadLocalFunctionMap(comp, "F").IsDefault);

            var entries = ReadLocalFunctionMap(comp, "MoveNext");
            var entry = Assert.Single(entries);
            Assert.Equal("G", entry.name);
            Assert.Contains("g__G", entry.loweredMethodName);
            Assert.True(entry.length > 0);
        }

        [Fact]
        public void LocalFunctionScopes_13()
        {
            // top-level statement
            var source = """
int G() => 42;
_ = G();
""";

            var verifier = CompileAndVerify(source, options: TestOptions.DebugExe);

            var entries = ReadLocalFunctionMap(verifier.Compilation, "<Main>$");
            var entry = Assert.Single(entries);
            Assert.Equal("G", entry.name);
            Assert.Contains("g__G", entry.loweredMethodName);
            Assert.True(entry.length > 0);
        }

        [Fact]
        public void LocalFunctionScopes_14()
        {
            // local function inside a lambda
            var source = """
class C
{
    void F()
    {
        System.Action a = () =>
        {
            int G() => 42;
            _ = G();
        };
        a();
    }
}
""";

            var verifier = CompileAndVerify(source, options: TestOptions.DebugDll);

            Assert.True(ReadLocalFunctionMap(verifier.Compilation, "F").IsDefault);

            // The lambda's lowered method body sees G
            var entries = ReadLocalFunctionMap(verifier.Compilation, "<F>b__0_0");
            var entry = Assert.Single(entries);
            Assert.Equal("G", entry.name);
            Assert.Contains("g__G", entry.loweredMethodName);
            Assert.True(entry.length > 0);
        }

        [Fact]
        public void LocalFunctionScopes_15()
        {
            // local function scoped to if-block
            var source = """
class C
{
    void F(int x)
    {
        if (x > 0)
        {
            int G(int y) { return y + 1; }
            _ = G(x);
        }
        System.Console.Write(x);
    }
}
""";

            var verifier = CompileAndVerify(source, options: TestOptions.DebugDll);
            verifier.VerifyIL("C.F", """
{
  // Code size       27 (0x1b)
  .maxstack  2
  .locals init (bool V_0)
  IL_0000:  nop
  IL_0001:  ldarg.1
  IL_0002:  ldc.i4.0
  IL_0003:  cgt
  IL_0005:  stloc.0
  IL_0006:  ldloc.0
  IL_0007:  brfalse.s  IL_0013
  IL_0009:  nop
  IL_000a:  nop
  IL_000b:  ldarg.1
  IL_000c:  call       "int C.<F>g__G|0_0(int)"
  IL_0011:  pop
  IL_0012:  nop
  IL_0013:  ldarg.1
  IL_0014:  call       "void System.Console.Write(int)"
  IL_0019:  nop
  IL_001a:  ret
}
""");

            var entries = ReadLocalFunctionMap(verifier.Compilation, "F");
            var entry = Assert.Single(entries);
            Assert.Equal("G", entry.name);
            Assert.Equal("<F>g__G|0_0", entry.loweredMethodName);
            // G's scope is the if-block, not the entire method.
            Assert.Equal(0x9, entry.startOffset);
            Assert.Equal(0x13, entry.startOffset + entry.length); // exclusive end
        }
    }
}
