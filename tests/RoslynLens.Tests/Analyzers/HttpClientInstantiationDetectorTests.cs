using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class HttpClientInstantiationDetectorTests
{
    private readonly HttpClientInstantiationDetector _detector = new();

    [Fact]
    public void Detects_New_HttpClient()
    {
        const string source = """
            using System.Net.Http;
            public class Foo
            {
                void M() { var client = new HttpClient(); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP003");
    }

    [Fact]
    public void Detects_Fully_Qualified_New_HttpClient()
    {
        const string source = """
            public class Foo
            {
                void M() { var client = new System.Net.Http.HttpClient(); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP003");
    }

    [Fact]
    public void Detects_Implicit_New_HttpClient()
    {
        const string source = """
            using System.Net.Http;
            public class Foo
            {
                void M() { HttpClient client = new(); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP003");
    }

    [Fact]
    public void Ignores_Other_Object_Creation()
    {
        const string source = """
            using System.Collections.Generic;
            public class Foo
            {
                void M() { var list = new List<string>(); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
