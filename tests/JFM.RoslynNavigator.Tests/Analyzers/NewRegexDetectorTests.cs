using JFM.RoslynNavigator.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace JFM.RoslynNavigator.Tests.Analyzers;

public class NewRegexDetectorTests
{
    private readonly NewRegexDetector _detector = new();

    [Fact]
    public void Detects_New_Regex()
    {
        const string source = """
            using System.Text.RegularExpressions;
            public class Foo
            {
                private static readonly Regex Pattern = new Regex(@"\d+");
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-REGEX");
    }

    [Fact]
    public void Ignores_Other_New_Expressions()
    {
        const string source = """
            public class Foo
            {
                private readonly List<string> Items = new List<string>();
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
