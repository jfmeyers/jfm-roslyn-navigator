using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class ThreadSleepDetectorTests
{
    private readonly ThreadSleepDetector _detector = new();

    [Fact]
    public void Detects_Thread_Sleep()
    {
        const string source = """
            using System.Threading;
            public class Foo
            {
                void M() { Thread.Sleep(1000); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-SLEEP");
    }

    [Fact]
    public void Detects_Fully_Qualified_Thread_Sleep()
    {
        const string source = """
            public class Foo
            {
                void M() { System.Threading.Thread.Sleep(500); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-SLEEP");
    }

    [Fact]
    public void Ignores_Task_Delay()
    {
        const string source = """
            using System.Threading.Tasks;
            public class Foo
            {
                async Task M() { await Task.Delay(1000); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
