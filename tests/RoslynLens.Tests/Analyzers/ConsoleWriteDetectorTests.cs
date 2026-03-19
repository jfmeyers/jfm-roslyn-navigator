using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class ConsoleWriteDetectorTests
{
    private readonly ConsoleWriteDetector _detector = new();

    [Fact]
    public void Detects_Console_WriteLine()
    {
        const string source = """
            using System;
            public class Foo
            {
                void M() { Console.WriteLine("hello"); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-CONSOLE");
    }

    [Fact]
    public void Detects_Console_Write()
    {
        const string source = """
            using System;
            public class Foo
            {
                void M() { Console.Write("hello"); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-CONSOLE");
    }

    [Fact]
    public void Detects_Fully_Qualified_Console_WriteLine()
    {
        const string source = """
            public class Foo
            {
                void M() { System.Console.WriteLine("hello"); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-CONSOLE");
    }

    [Fact]
    public void Ignores_Logger_Calls()
    {
        const string source = """
            public class Foo
            {
                void M() { _logger.LogInformation("hello"); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
