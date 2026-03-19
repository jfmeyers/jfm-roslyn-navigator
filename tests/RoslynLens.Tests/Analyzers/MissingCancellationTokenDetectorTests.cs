using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class MissingCancellationTokenDetectorTests
{
    private readonly MissingCancellationTokenDetector _detector = new();

    [Fact]
    public void Returns_No_Violations_Without_SemanticModel()
    {
        const string source = """
            using System.Threading.Tasks;
            public class Service
            {
                public async Task DoWorkAsync() { await Task.Delay(1); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Requires_SemanticModel()
    {
        _detector.RequiresSemanticModel.ShouldBeTrue();
    }

    [Fact]
    public void Detects_Public_Async_Method_Without_CancellationToken()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            public class Service
            {
                public async Task DoWorkAsync() { await Task.Delay(1); }
            }
            """;

        var (tree, model) = CreateCompilation(source);
        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP008");
    }

    [Fact]
    public void Ignores_Public_Async_Method_With_CancellationToken()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            public class Service
            {
                public async Task DoWorkAsync(CancellationToken ct) { await Task.Delay(1, ct); }
            }
            """;

        var (tree, model) = CreateCompilation(source);
        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_Private_Async_Method()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            public class Service
            {
                private async Task DoWorkAsync() { await Task.Delay(1); }
            }
            """;

        var (tree, model) = CreateCompilation(source);
        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_Non_Async_Method()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            public class Service
            {
                public Task DoWorkAsync() { return Task.Delay(1); }
            }
            """;

        var (tree, model) = CreateCompilation(source);
        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Detects_By_Syntax_Type_Name_Fallback()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            public class Service
            {
                public async Task ProcessAsync(string data) { await Task.Delay(1); }
            }
            """;

        var (tree, model) = CreateCompilation(source);
        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP008" && v.Message.Contains("ProcessAsync"));
    }

    private static (SyntaxTree Tree, SemanticModel Model) CreateCompilation(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(CancellationToken).Assembly.Location)
        };

        // Add runtime assemblies for System.Threading
        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var runtimeRefs = new[]
        {
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Threading.dll"))
        };

        var compilation = CSharpCompilation.Create("TestAssembly",
            [tree],
            references.Concat(runtimeRefs),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = compilation.GetSemanticModel(tree);
        return (tree, model);
    }
}
