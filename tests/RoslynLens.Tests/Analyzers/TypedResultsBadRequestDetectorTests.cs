using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class TypedResultsBadRequestDetectorTests
{
    private readonly TypedResultsBadRequestDetector _detector = new();

    [Fact]
    public void Detects_TypedResults_BadRequest()
    {
        const string source = """
            public class Foo
            {
                object M() => TypedResults.BadRequest("error");
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-BADREQ");
        violations[0].Message.ShouldContain("TypedResults");
    }

    [Fact]
    public void Detects_Results_BadRequest()
    {
        const string source = """
            public class Foo
            {
                object M() => Results.BadRequest("error");
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-BADREQ");
        violations[0].Message.ShouldContain("Results");
    }

    [Fact]
    public void Ignores_TypedResults_Problem()
    {
        const string source = """
            public class Foo
            {
                object M() => TypedResults.Problem("error", statusCode: 400);
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_Other_Receiver()
    {
        const string source = """
            public class Foo
            {
                object M() => MyHelper.BadRequest("error");
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
