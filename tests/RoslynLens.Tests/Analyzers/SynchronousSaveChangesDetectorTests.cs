using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class SynchronousSaveChangesDetectorTests
{
    private readonly SynchronousSaveChangesDetector _detector = new();

    [Fact]
    public void RequiresSemanticModel_IsTrue()
    {
        _detector.RequiresSemanticModel.ShouldBeTrue();
    }

    [Fact]
    public void Returns_Empty_When_SemanticModel_Is_Null()
    {
        const string source = """
            public class Foo
            {
                void M() { _context.SaveChanges(); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_SaveChanges_On_Resolved_Non_DbContext_Type()
    {
        const string source = """
            public class Settings
            {
                public int SaveChanges() => 0;
            }
            public class Service
            {
                private readonly Settings _settings = new();
                public void M()
                {
                    _settings.SaveChanges();
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);

        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_Non_SaveChanges_Method()
    {
        const string source = """
            public class AppDbContext
            {
                public void DoSomething() { }
            }
            public class Service
            {
                private readonly AppDbContext _context = new();
                public void M()
                {
                    _context.DoSomething();
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);

        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Ignores_Non_MemberAccess_Invocation()
    {
        const string source = """
            public class Service
            {
                public int SaveChanges() => 0;
                public void M()
                {
                    SaveChanges();
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("Test",
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);

        var violations = _detector.Detect(tree, model, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
