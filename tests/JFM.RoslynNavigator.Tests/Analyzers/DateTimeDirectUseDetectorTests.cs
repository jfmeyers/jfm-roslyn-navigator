using JFM.RoslynNavigator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace JFM.RoslynNavigator.Tests.Analyzers;

public class DateTimeDirectUseDetectorTests
{
    private readonly DateTimeDirectUseDetector _detector = new();

    [Fact]
    public void Detects_DateTime_Now()
    {
        const string source = """
            using System;
            public class Foo
            {
                public DateTime GetTime() => DateTime.Now;
            }
            """;

        var violations = RunDetector(source);
        violations.ShouldContain(v => v.Id == "AP004");
    }

    [Fact]
    public void Detects_DateTime_UtcNow()
    {
        const string source = """
            using System;
            public class Foo
            {
                public DateTime GetTime() => DateTime.UtcNow;
            }
            """;

        var violations = RunDetector(source);
        violations.ShouldContain(v => v.Id == "AP004");
    }

    [Fact]
    public void Ignores_Other_DateTime_Members()
    {
        const string source = """
            using System;
            public class Foo
            {
                public DateTime Parse(string s) => DateTime.Parse(s);
            }
            """;

        var violations = RunDetector(source);
        violations.ShouldBeEmpty();
    }

    private List<AntiPatternViolation> RunDetector(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
    }
}
