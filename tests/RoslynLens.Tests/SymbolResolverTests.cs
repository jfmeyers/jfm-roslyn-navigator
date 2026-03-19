using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests;

public class SymbolResolverTests
{
    [Theory]
    [InlineData("hello", "world", 4)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("abc", "abc", 0)]
    [InlineData("abc", "abd", 1)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "", 3)]
    [InlineData("SymbolResolver", "SymbolResolvr", 1)]
    [InlineData("WorkspaceManager", "WorkspaceManger", 1)]
    public void LevenshteinDistance_ComputesCorrectly(string a, string b, int expected)
    {
        SymbolResolver.LevenshteinDistance(a, b).ShouldBe(expected);
    }

    [Fact]
    public void LevenshteinDistance_IsCaseInsensitive()
    {
        SymbolResolver.LevenshteinDistance("Hello", "hello").ShouldBe(0);
    }

    [Theory]
    [InlineData("class", true)]
    [InlineData("interface", false)]
    [InlineData("method", false)]
    [InlineData("property", false)]
    [InlineData("field", false)]
    [InlineData("unknown_kind", true)]
    public void MatchesKind_Class(string kind, bool expected)
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText("public class Foo { }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var symbol = compilation.GetTypeByMetadataName("Foo")!;
        SymbolResolver.MatchesKind(symbol, kind).ShouldBe(expected);
    }

    [Fact]
    public void MatchesKind_Interface()
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText("public interface IFoo { }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var symbol = compilation.GetTypeByMetadataName("IFoo")!;
        SymbolResolver.MatchesKind(symbol, "interface").ShouldBeTrue();
        SymbolResolver.MatchesKind(symbol, "class").ShouldBeFalse();
    }

    [Fact]
    public void MatchesKind_Struct()
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText("public struct Bar { }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var symbol = compilation.GetTypeByMetadataName("Bar")!;
        SymbolResolver.MatchesKind(symbol, "struct").ShouldBeTrue();
    }

    [Fact]
    public void MatchesKind_Enum()
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText("public enum Color { Red, Green }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var symbol = compilation.GetTypeByMetadataName("Color")!;
        SymbolResolver.MatchesKind(symbol, "enum").ShouldBeTrue();
    }

    [Fact]
    public void MatchesKind_Record()
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText("public record Rec(string Name);")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var symbol = compilation.GetTypeByMetadataName("Rec")!;
        SymbolResolver.MatchesKind(symbol, "record").ShouldBeTrue();
    }

    [Fact]
    public void MatchesKind_Namespace()
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText("namespace MyNs { public class C { } }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var ns = compilation.GlobalNamespace.GetNamespaceMembers().First(n => n.Name == "MyNs");
        SymbolResolver.MatchesKind(ns, "namespace").ShouldBeTrue();
        SymbolResolver.MatchesKind(ns, "class").ShouldBeFalse();
    }

    [Fact]
    public void MatchesKind_Method()
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText("public class C { public void M() { } }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var method = compilation.GetTypeByMetadataName("C")!.GetMembers("M").First();
        SymbolResolver.MatchesKind(method, "method").ShouldBeTrue();
    }

    [Fact]
    public void MatchesKind_Property()
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText("public class C { public int P { get; set; } }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var prop = compilation.GetTypeByMetadataName("C")!.GetMembers("P").First();
        SymbolResolver.MatchesKind(prop, "property").ShouldBeTrue();
    }

    [Fact]
    public void MatchesKind_Field()
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText("public class C { public int F; }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var field = compilation.GetTypeByMetadataName("C")!.GetMembers("F").First();
        SymbolResolver.MatchesKind(field, "field").ShouldBeTrue();
    }

    [Fact]
    public void MatchesKind_Event()
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText("public class C { public event System.Action E; }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var ev = compilation.GetTypeByMetadataName("C")!.GetMembers("E").First();
        SymbolResolver.MatchesKind(ev, "event").ShouldBeTrue();
    }

    [Fact]
    public void GetLocation_ReturnsFileAndLine()
    {
        var tree = CSharpSyntaxTree.ParseText("public class Foo { }");
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var symbol = compilation.GetTypeByMetadataName("Foo")!;
        var (file, line) = SymbolResolver.GetLocation(symbol);
        line.ShouldBe(1);
    }

    [Fact]
    public void GetLocation_NoSyntaxRef_ReturnsNull()
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText("public class C { }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        // System.Object has no declaring syntax references in source
        var objectSymbol = compilation.GetSpecialType(SpecialType.System_Object);
        var (file, line) = SymbolResolver.GetLocation(objectSymbol);
        file.ShouldBeNull();
        line.ShouldBeNull();
    }
}
