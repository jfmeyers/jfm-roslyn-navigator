using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

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

    [Fact]
    public void Ignores_Event_Handler()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            public class Foo
            {
                public async void OnClick(object sender, EventHandler e) { await Task.Delay(1); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_Non_Async_Void_Method()
    {
        const string source = """
            public class Foo
            {
                public void DoWork() { }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
