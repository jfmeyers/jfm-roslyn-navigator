using JFM.RoslynNavigator.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace JFM.RoslynNavigator.Tests.Analyzers;

public class GuidNewGuidDetectorTests
{
    private readonly GuidNewGuidDetector _detector = new();

    [Fact]
    public void Detects_Guid_NewGuid()
    {
        const string source = """
            using System;
            public class Foo
            {
                public Guid Create() => Guid.NewGuid();
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-GUID");
    }

    [Fact]
    public void Ignores_Other_Guid_Methods()
    {
        const string source = """
            using System;
            public class Foo
            {
                public Guid Parse(string s) => Guid.Parse(s);
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
