# Adding a Detector

## 1. Create the detector

Create `src/RoslynLens/Analyzers/MyDetector.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynLens.Analyzers;

public class MyDetector : IAntiPatternDetector
{
    public IEnumerable<AntiPatternViolation> Detect(
        SyntaxTree tree,
        SemanticModel? model,
        CancellationToken cancellationToken)
    {
        var root = tree.GetRoot(cancellationToken);

        foreach (var node in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (node.ToString().Contains("BadPattern"))
            {
                var line = node.GetLocation().GetLineSpan();
                yield return new AntiPatternViolation(
                    Id: "MY-001",
                    Message: "Avoid BadPattern — use GoodPattern instead",
                    FilePath: tree.FilePath,
                    Line: line.StartLinePosition.Line + 1,
                    Severity: AntiPatternSeverity.Warning);
            }
        }
    }
}
```

### Conventions

- **ID format**: `APxxx` for general .NET, `GR-*` for domain-specific
- **Handle null SemanticModel**: If your detector needs semantic analysis, check
  `if (model is null) yield break;` at the top
- **Use CancellationToken**: Pass it to `GetRoot()` and check it in long loops

## 2. Create the test

Create `tests/RoslynLens.Tests/Analyzers/MyDetectorTests.cs`:

```csharp
using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class MyDetectorTests
{
    private readonly MyDetector _detector = new();

    [Fact]
    public void Detects_BadPattern()
    {
        const string source = """
            public class Foo
            {
                public void Run() { BadPattern(); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "MY-001");
    }

    [Fact]
    public void Ignores_GoodPattern()
    {
        const string source = """
            public class Foo
            {
                public void Run() { GoodPattern(); }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
```

### Test conventions

- Use `TestContext.Current.CancellationToken` (xUnit v3 requirement)
- Use `CSharpSyntaxTree.ParseText()` — no full compilation needed for syntax detectors
- Test both positive (detects the bad pattern) and negative (ignores good code) cases

## 3. Verify

```bash
dotnet build
dotnet test
```

The detector is automatically picked up by `DetectAntiPatternsTool` via reflection —
no registration needed.
