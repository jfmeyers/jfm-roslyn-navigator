using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class BroadCatchDetectorTests
{
    private readonly BroadCatchDetector _detector = new();

    [Fact]
    public void Detects_Bare_Catch()
    {
        const string source = """
            public class Foo
            {
                void M() { try { } catch { } }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP005");
        violations[0].Message.ShouldContain("Bare catch");
    }

    [Fact]
    public void Detects_Catch_Exception()
    {
        const string source = """
            using System;
            public class Foo
            {
                void M() { try { } catch (Exception ex) { } }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP005");
        violations[0].Message.ShouldContain("Broad catch");
    }

    [Fact]
    public void Detects_Catch_System_Exception()
    {
        const string source = """
            public class Foo
            {
                void M() { try { } catch (System.Exception ex) { } }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP005");
    }

    [Fact]
    public void Ignores_Catch_Exception_With_When_Filter()
    {
        const string source = """
            using System;
            public class Foo
            {
                void M() { try { } catch (Exception ex) when (ex is InvalidOperationException) { } }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_Specific_Exception_Type()
    {
        const string source = """
            using System;
            public class Foo
            {
                void M() { try { } catch (InvalidOperationException ex) { } }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
