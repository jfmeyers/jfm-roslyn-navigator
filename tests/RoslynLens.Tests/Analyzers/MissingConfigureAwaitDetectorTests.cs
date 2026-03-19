using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class MissingConfigureAwaitDetectorTests
{
    private readonly MissingConfigureAwaitDetector _detector = new();

    [Fact]
    public void RequiresSemanticModel_IsTrue()
    {
        _detector.RequiresSemanticModel.ShouldBeTrue();
    }

    [Fact]
    public void Returns_Empty_When_SemanticModel_Is_Null()
    {
        const string source = """
            using System.Threading.Tasks;
            public class Foo
            {
                async Task M() { await Task.Delay(1); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Detects_Await_Without_ConfigureAwait()
    {
        const string source = """
            using System.Threading.Tasks;
            public class MyLib
            {
                public async Task DoWorkAsync()
                {
                    await Task.Delay(100);
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CreateCompilation(tree, "MyLib");
        var model = compilation.GetSemanticModel(tree);

        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "GR-CFGAWAIT");
    }

    [Fact]
    public void Ignores_Await_With_ConfigureAwait()
    {
        const string source = """
            using System.Threading.Tasks;
            public class MyLib
            {
                public async Task DoWorkAsync()
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CreateCompilation(tree, "MyLib");
        var model = compilation.GetSemanticModel(tree);

        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Skips_Test_Projects()
    {
        const string source = """
            using System.Threading.Tasks;
            public class MyLibTests
            {
                public async Task TestMethod()
                {
                    await Task.Delay(100);
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CreateCompilation(tree, "MyLib.Tests");
        var model = compilation.GetSemanticModel(tree);

        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Skips_Test_Project_Suffix()
    {
        const string source = """
            using System.Threading.Tasks;
            public class Foo
            {
                public async Task M() { await Task.Delay(1); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CreateCompilation(tree, "App.Test");
        var model = compilation.GetSemanticModel(tree);

        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Skips_Test_Project_InMiddle()
    {
        const string source = """
            using System.Threading.Tasks;
            public class Foo
            {
                public async Task M() { await Task.Delay(1); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CreateCompilation(tree, "App.Tests.Unit");
        var model = compilation.GetSemanticModel(tree);

        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    private static CSharpCompilation CreateCompilation(SyntaxTree tree, string assemblyName)
    {
        var runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return CSharpCompilation.Create(assemblyName,
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
             MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")),
             MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
