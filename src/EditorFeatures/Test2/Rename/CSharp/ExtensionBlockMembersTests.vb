' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Rename.ConflictEngine

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.Rename
    ' Extension block members can be accessed both via extension access syntax or
    ' static implementation invocation syntax.
    ' For a method, you can either do: `receiver.M()` or `E.M(receiver)`.
    ' For a property, you can either do: `receiver.P` or `E.get_P(receiver)`/`E.set_P(receiver, value)`.
    ' When renaming we update both extension references and implementation invocation references.
    <[UseExportProvider]>
    <Trait(Traits.Feature, Traits.Features.Rename)>
    Public Class ExtensionBlockMembersTests

        Private ReadOnly _outputHelper As Abstractions.ITestOutputHelper

        Public Sub New(outputHelper As Abstractions.ITestOutputHelper)
            _outputHelper = outputHelper
        End Sub

        <Theory, CombinatorialData>
        Public Sub RenameMethod(host As RenameTestHost)
            Using result = RenameEngineResult.Create(_outputHelper,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" LanguageVersion="14.0">
                        <Document>
static class E
{
    extension(int i)
    {
        public void [|$$M|]() { }
    }
}

class C
{
    void Test()
    {
        42.[|M|]();
        E.[|M|](42);
    }
}
                        </Document>
                    </Project>
                </Workspace>,
                host:=host,
                renameTo:="M2")
            End Using
        End Sub

        <Theory, CombinatorialData>
        Public Sub RenameMethod_FromExtensionInvocation(host As RenameTestHost)
            Using result = RenameEngineResult.Create(_outputHelper,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" LanguageVersion="14.0">
                        <Document>
static class E
{
    extension(int i)
    {
        public void [|M|]() { }
    }
}

class C
{
    void Test()
    {
        42.[|$$M|]();
        E.[|M|](42);
    }
}
                    </Document>
                    </Project>
                </Workspace>,
                host:=host,
                renameTo:="M2")
            End Using
        End Sub

        <Theory, CombinatorialData>
        Public Sub RenameMethod_FromImplementationMethodInvocation(host As RenameTestHost)
            Using result = RenameEngineResult.Create(_outputHelper,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" LanguageVersion="14.0">
                        <Document>
static class E
{
    extension(int i)
    {
        public void [|M|]() { }
    }
}

class C
{
    void Test()
    {
        42.[|M|]();
        E.[|$$M|](42);
    }
}
                        </Document>
                    </Project>
                </Workspace>,
                host:=host,
                renameTo:="M2")
            End Using
        End Sub

        <Theory, CombinatorialData>
        Public Sub RenameProperty_FromDefinition_Simple(host As RenameTestHost)
            Dim workspaceXml =
                <Workspace>
                    <Project Language="C#" CommonReferences="true" LanguageVersion="14.0">
                        <Document>
static class E
{
    extension(string s)
    {
        public int {|def:$$P|}
        {
            get => 0;
            set { }
        }
    }
}

class C
{
    void Test()
    {
        _ = E.{|getter:get_P|}("");
    }
}
                        </Document>
                    </Project>
                </Workspace>

            Using result = RenameEngineResult.Create(_outputHelper, workspaceXml, host:=host, renameTo:="Q")
                result.AssertLabeledSpansAre("def", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("getter", replacement:="get_Q", type:=RelatedLocationType.NoConflict)
            End Using
        End Sub

        <Theory, CombinatorialData>
        Public Sub RenameProperty_FromDefinition(host As RenameTestHost)
            Dim workspaceXml =
                <Workspace>
                    <Project Language="C#" CommonReferences="true" LanguageVersion="14.0">
                        <Document>
static class E
{
    extension(string s)
    {
        public int {|def:$$P|}
        {
            get => 0;
            set { }
        }
    }
}

class C
{
    void Test()
    {
        _ = "".{|read:P|};
        "".{|write:P|} = 1;

        _ = E.{|getter:get_P|}("");
        E.{|setter:set_P|}("", 1);
    }
}
                        </Document>
                    </Project>
                </Workspace>

            Using result = RenameEngineResult.Create(_outputHelper, workspaceXml, host:=host, renameTo:="Q")
                result.AssertLabeledSpansAre("def", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("read", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("write", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("getter", replacement:="get_Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("setter", replacement:="set_Q", type:=RelatedLocationType.NoConflict)
            End Using
        End Sub

        <Theory, CombinatorialData>
        Public Sub RenameProperty_FromAccess(host As RenameTestHost)
            Dim workspaceXml =
                <Workspace>
                    <Project Language="C#" CommonReferences="true" LanguageVersion="14.0">
                        <Document>
static class E
{
    extension(string s)
    {
        public int {|def:P|}
        {
            get => 0;
            set { }
        }
    }
}

class C
{
    void Test()
    {
        _ = "".{|read:$$P|};
        "".{|write:P|} = 1;

        _ = E.{|getter:get_P|}("");
        E.{|setter:set_P|}("", 1);
    }
}
                        </Document>
                    </Project>
                </Workspace>

            Using result = RenameEngineResult.Create(_outputHelper, workspaceXml, host:=host, renameTo:="Q")
                result.AssertLabeledSpansAre("def", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("read", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("write", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("getter", replacement:="get_Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("setter", replacement:="set_Q", type:=RelatedLocationType.NoConflict)
            End Using
        End Sub

        <Theory, CombinatorialData>
        Public Sub RenameProperty_FromDefinition_ToConflictingName_Simple(host As RenameTestHost)
            ' TODO2
            Dim workspaceXml =
                <Workspace>
                    <Project Language="C#" CommonReferences="true" LanguageVersion="14.0">
                        <Document>
static class E
{
    extension(string s)
    {
        public int {|def:$$P|}
        {
            get => 0;
            set { }
        }
    }
    public static void get_Q(this string s) { }
}

class C
{
    void Test()
    {
        _ = E.{|getter:get_P|}("");
    }
}
                        </Document>
                    </Project>
                </Workspace>

            Using result = RenameEngineResult.Create(_outputHelper, workspaceXml, host:=host, renameTo:="Q")
                result.AssertLabeledSpansAre("def", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("getter", replacement:="get_Q", type:=RelatedLocationType.UnresolvableConflict)
            End Using
        End Sub

        <Theory, CombinatorialData>
        Public Sub RenameProperty_FromDefinition_ToConflictingName(host As RenameTestHost)
            ' TODO2
            Dim workspaceXml =
                <Workspace>
                    <Project Language="C#" CommonReferences="true" LanguageVersion="14.0">
                        <Document>
static class E
{
    extension(string s)
    {
        public int {|def:$$P|}
        {
            get => 0;
            set { }
        }
    }
    public static void get_Q(this string s) { }
}

class C
{
    void Test()
    {
        _ = "".{|read:P|};
        "".{|write:P|} = 1;

        _ = E.{|getter:get_P|}("");
        E.{|setter:set_P|}("", 1);
    }
}
                        </Document>
                    </Project>
                </Workspace>

            Using result = RenameEngineResult.Create(_outputHelper, workspaceXml, host:=host, renameTo:="Q")
                result.AssertLabeledSpansAre("def", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("read", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("write", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("getter", replacement:="get_Q", type:=RelatedLocationType.UnresolvableConflict)
                result.AssertLabeledSpansAre("setter", replacement:="set_Q", type:=RelatedLocationType.UnresolvableConflict)
            End Using
        End Sub

        <Theory, CombinatorialData>
        Public Sub RenameProperty_FromDefinition_MultiDocument(host As RenameTestHost)
            Dim workspaceXml =
                <Workspace>
                    <Project Language="C#" CommonReferences="true" LanguageVersion="14.0">
                        <Document>
static class E
{
    extension(string s)
    {
        public int {|def:$$P|}
        {
            get => 0;
            set { }
        }
    }
}
                        </Document>
                        <Document>
class C1
{
    void Test()
    {
        _ = "".{|read:P|};
    }
}
                        </Document>
                        <Document>
class C2
{
    void Test()
    {
        "".{|write:P|} = 1;
    }
}
                        </Document>
                        <Document>
class C3
{
    void Test()
    {
        _ = E.{|getter:get_P|}("");
    }
}
                        </Document>
                        <Document>
class C4
{
    void Test()
    {
        E.{|setter:set_P|}("", 1);
    }
}
                        </Document>
                    </Project>
                </Workspace>

            Using result = RenameEngineResult.Create(_outputHelper, workspaceXml, host:=host, renameTo:="Q")
                result.AssertLabeledSpansAre("def", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("read", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("write", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("getter", replacement:="get_Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("setter", replacement:="set_Q", type:=RelatedLocationType.NoConflict)
            End Using
        End Sub

        <Theory, CombinatorialData>
        Public Sub RenameProperty_FromAccess_MultiDocument(host As RenameTestHost)
            Dim workspaceXml =
                <Workspace>
                    <Project Language="C#" CommonReferences="true" LanguageVersion="14.0">
                        <Document>
static class E
{
    extension(string s)
    {
        public int {|def:P|}
        {
            get => 0;
            set { }
        }
    }
}
                        </Document>
                        <Document>
class C1
{
    void Test()
    {
        _ = "".{|read:$$P|};
    }
}
                        </Document>
                        <Document>
class C2
{
    void Test()
    {
        "".{|write:P|} = 1;
    }
}
                        </Document>
                        <Document>
class C3
{
    void Test()
    {
        _ = E.{|getter:get_P|}("");
    }
}
                        </Document>
                        <Document>
class C4
{
    void Test()
    {
        E.{|setter:set_P|}("", 1);
    }
}
                        </Document>
                    </Project>
                </Workspace>

            Using result = RenameEngineResult.Create(_outputHelper, workspaceXml, host:=host, renameTo:="Q")
                result.AssertLabeledSpansAre("def", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("read", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("write", replacement:="Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("getter", replacement:="get_Q", type:=RelatedLocationType.NoConflict)
                result.AssertLabeledSpansAre("setter", replacement:="set_Q", type:=RelatedLocationType.NoConflict)
            End Using
        End Sub

    End Class
End Namespace
