using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class PragmaWithoutRestoreDetectorTests
{
    private readonly PragmaWithoutRestoreDetector _detector = new();

    [Fact]
    public void Detects_Unrestored_Pragma_Disable()
    {
        const string source = """
            #pragma warning disable CS1591
            public class Foo { }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP007");
    }

    [Fact]
    public void Ignores_Restored_Pragma()
    {
        const string source = """
            #pragma warning disable CS1591
            public class Foo { }
            #pragma warning restore CS1591
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Detects_Bare_Pragma_Disable_Without_Restore()
    {
        const string source = """
            #pragma warning disable
            public class Foo { }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP007");
    }

    [Fact]
    public void Ignores_Bare_Pragma_With_Restore()
    {
        const string source = """
            #pragma warning disable
            public class Foo { }
            #pragma warning restore
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Detects_Multiple_Unrestored_Codes()
    {
        const string source = """
            #pragma warning disable CS1591, CS0618
            public class Foo { }
            #pragma warning restore CS1591
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.Count.ShouldBe(1);
        violations[0].Message.ShouldContain("CS0618");
    }
}
