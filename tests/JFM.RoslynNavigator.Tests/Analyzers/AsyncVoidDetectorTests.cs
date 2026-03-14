using JFM.RoslynNavigator.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace JFM.RoslynNavigator.Tests.Analyzers;

public class AsyncVoidDetectorTests
{
    private readonly AsyncVoidDetector _detector = new();

    [Fact]
    public void Detects_Async_Void_Method()
    {
        const string source = """
            using System.Threading.Tasks;
            public class Foo
            {
                public async void DoWork() { await Task.Delay(1); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP001");
    }

    [Fact]
    public void Ignores_Async_Task_Method()
    {
        const string source = """
            using System.Threading.Tasks;
            public class Foo
            {
                public async Task DoWork() { await Task.Delay(1); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
