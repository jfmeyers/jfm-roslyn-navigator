using JFM.RoslynNavigator.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace JFM.RoslynNavigator.Tests.Analyzers;

public class SyncOverAsyncDetectorTests
{
    private readonly SyncOverAsyncDetector _detector = new();

    [Fact]
    public void Detects_Task_Result()
    {
        const string source = """
            using System.Threading.Tasks;
            public class Foo
            {
                public string Get() => Task.FromResult("x").Result;
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP002");
    }

    [Fact]
    public void Detects_Task_Wait()
    {
        const string source = """
            using System.Threading.Tasks;
            public class Foo
            {
                public void Run() { Task.Delay(1).Wait(); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP002");
    }

    [Fact]
    public void Detects_GetAwaiter_GetResult()
    {
        const string source = """
            using System.Threading.Tasks;
            public class Foo
            {
                public string Get() => Task.FromResult("x").GetAwaiter().GetResult();
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP002");
    }
}
