using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class DtoSuffixDetectorTests
{
    private readonly DtoSuffixDetector _detector = new();

    [Fact]
    public void Detects_Class_With_Dto_Suffix()
    {
        const string source = """
            public class UserDto { }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-DTO");
    }

    [Fact]
    public void Detects_Record_With_Dto_Suffix()
    {
        const string source = """
            public record OrderDto(string Name);
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-DTO");
    }

    [Fact]
    public void Ignores_Request_Suffix()
    {
        const string source = """
            public record CreateUserRequest(string Name);
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_Response_Suffix()
    {
        const string source = """
            public record UserResponse(string Name);
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
