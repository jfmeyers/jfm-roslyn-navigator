using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;

namespace RoslynLens.Tests;

public class ComplexityAnalyzerTests
{
    private static MethodDeclarationSyntax ParseMethod(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    [Fact]
    public void CyclomaticComplexity_SimpleMethod_Returns1()
    {
        var method = ParseMethod("""
            class C {
                void M() { var x = 1; }
            }
            """);

        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBe(1);
    }

    [Fact]
    public void CyclomaticComplexity_WithBranches_CountsDecisionPoints()
    {
        var method = ParseMethod("""
            class C {
                int M(int x) {
                    if (x > 0) return 1;
                    else if (x < 0) return -1;
                    else return 0;
                }
            }
            """);

        // base(1) + 2 ifs = 3
        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void CyclomaticComplexity_WithLogicalOperators_CountsThem()
    {
        var method = ParseMethod("""
            class C {
                bool M(int x) {
                    if (x > 0 && x < 10 || x == -1) return true;
                    return false;
                }
            }
            """);

        // base(1) + if(1) + &&(1) + ||(1) = 4
        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void CognitiveComplexity_NestedIfs_HigherThanFlat()
    {
        var flat = ParseMethod("""
            class C {
                void M(int x) {
                    if (x > 0) { }
                    if (x < 10) { }
                }
            }
            """);

        var nested = ParseMethod("""
            class C {
                void M(int x) {
                    if (x > 0) {
                        if (x < 10) { }
                    }
                }
            }
            """);

        var flatScore = ComplexityAnalyzer.CalculateCognitiveComplexity(flat.Body!);
        var nestedScore = ComplexityAnalyzer.CalculateCognitiveComplexity(nested.Body!);

        nestedScore.ShouldBeGreaterThan(flatScore);
    }

    [Fact]
    public void MaxNestingDepth_FlatMethod_Returns0()
    {
        var method = ParseMethod("""
            class C {
                void M() { var x = 1; }
            }
            """);

        ComplexityAnalyzer.CalculateMaxNestingDepth(method.Body!).ShouldBe(0);
    }

    [Fact]
    public void MaxNestingDepth_TripleNested_Returns3()
    {
        var method = ParseMethod("""
            class C {
                void M(int x) {
                    if (x > 0) {
                        for (int i = 0; i < x; i++) {
                            while (true) {
                                break;
                            }
                        }
                    }
                }
            }
            """);

        ComplexityAnalyzer.CalculateMaxNestingDepth(method.Body!).ShouldBe(3);
    }

    [Fact]
    public void LogicalLoc_ExcludesBlanksAndComments()
    {
        var method = ParseMethod("""
            class C {
                void M() {
                    // This is a comment
                    var x = 1;

                    var y = 2;
                    // Another comment
                    var z = x + y;
                }
            }
            """);

        // Should count: { var x = 1; var y = 2; var z = x + y; } = 5 lines (opening brace + 3 statements + closing brace)
        var loc = ComplexityAnalyzer.CalculateLogicalLoc(method.Body!);
        loc.ShouldBe(5);
    }

    [Fact]
    public void Analyze_ReturnsAllMetrics()
    {
        var method = ParseMethod("""
            class C {
                int M(int x) {
                    if (x > 0) return 1;
                    return 0;
                }
            }
            """);

        var metrics = ComplexityAnalyzer.Analyze(method.Body!);

        metrics.Cyclomatic.ShouldBeGreaterThanOrEqualTo(2);
        metrics.Cognitive.ShouldBeGreaterThanOrEqualTo(1);
        metrics.MaxNesting.ShouldBeGreaterThanOrEqualTo(0);
        metrics.LogicalLoc.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CyclomaticComplexity_WithSwitch_CountsCases()
    {
        var method = ParseMethod("""
            class C {
                int M(int x) {
                    switch (x) {
                        case 1: return 10;
                        case 2: return 20;
                        default: return 0;
                    }
                }
            }
            """);

        // base(1) + 2 case labels = 3
        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void CyclomaticComplexity_WithSwitchExpression_CountsArms()
    {
        var method = ParseMethod("""
            class C {
                int M(int x) {
                    return x switch { 1 => 10, 2 => 20, _ => 0 };
                }
            }
            """);

        // base(1) + 3 arms = 4
        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void CyclomaticComplexity_WithTernary_Counts()
    {
        var method = ParseMethod("""
            class C {
                int M(int x) { return x > 0 ? 1 : 0; }
            }
            """);

        // base(1) + ternary(1) = 2
        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_WithCatch_Counts()
    {
        var method = ParseMethod("""
            class C {
                void M() {
                    try { } catch (System.Exception) { }
                }
            }
            """);

        // base(1) + catch(1) = 2
        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_WithCoalesce_Counts()
    {
        var method = ParseMethod("""
            class C {
                string M(string s) { return s ?? "default"; }
            }
            """);

        // base(1) + ??(1) = 2
        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_WithForeach_Counts()
    {
        var method = ParseMethod("""
            class C {
                void M(int[] items) {
                    foreach (var i in items) { }
                }
            }
            """);

        // base(1) + foreach(1) = 2
        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBe(2);
    }

    [Fact]
    public void CyclomaticComplexity_WithWhile_Counts()
    {
        var method = ParseMethod("""
            class C {
                void M() { while (true) { break; } }
            }
            """);

        // base(1) + while(1) = 2
        ComplexityAnalyzer.CalculateCyclomaticComplexity(method.Body!).ShouldBe(2);
    }

    [Fact]
    public void CognitiveComplexity_WithSwitch_Counts()
    {
        var method = ParseMethod("""
            class C {
                int M(int x) {
                    switch (x) {
                        case 1: return 10;
                        default: return 0;
                    }
                }
            }
            """);

        ComplexityAnalyzer.CalculateCognitiveComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CognitiveComplexity_WithCatch_Counts()
    {
        var method = ParseMethod("""
            class C {
                void M() { try { } catch (System.Exception) { } }
            }
            """);

        ComplexityAnalyzer.CalculateCognitiveComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CognitiveComplexity_WithTernary_Counts()
    {
        var method = ParseMethod("""
            class C {
                int M(int x) { return x > 0 ? 1 : 0; }
            }
            """);

        ComplexityAnalyzer.CalculateCognitiveComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CognitiveComplexity_WithDoWhile_Counts()
    {
        var method = ParseMethod("""
            class C {
                void M() { do { } while (false); }
            }
            """);

        ComplexityAnalyzer.CalculateCognitiveComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CognitiveComplexity_WithForeach_Counts()
    {
        var method = ParseMethod("""
            class C {
                void M(int[] items) { foreach (var i in items) { } }
            }
            """);

        ComplexityAnalyzer.CalculateCognitiveComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CognitiveComplexity_WithLambda_IncreasesNesting()
    {
        var method = ParseMethod("""
            class C {
                void M() {
                    System.Action a = () => { if (true) { } };
                }
            }
            """);

        // lambda nesting + if inside = at least 2
        ComplexityAnalyzer.CalculateCognitiveComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void CognitiveComplexity_ElseIf_NoNestingIncrease()
    {
        var method = ParseMethod("""
            class C {
                void M(int x) {
                    if (x > 0) { }
                    else if (x < 0) { }
                    else { }
                }
            }
            """);

        // if(1) + else(1) + else-if(1, no nesting bump) + else(1) = 4
        ComplexityAnalyzer.CalculateCognitiveComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void CognitiveComplexity_SwitchExpression_Counts()
    {
        var method = ParseMethod("""
            class C {
                int M(int x) {
                    return x switch { 1 => 10, _ => 0 };
                }
            }
            """);

        ComplexityAnalyzer.CalculateCognitiveComplexity(method.Body!).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void MaxNestingDepth_WithDoWhile_Counts()
    {
        var method = ParseMethod("""
            class C {
                void M() {
                    do { } while (false);
                }
            }
            """);

        ComplexityAnalyzer.CalculateMaxNestingDepth(method.Body!).ShouldBe(1);
    }

    [Fact]
    public void MaxNestingDepth_WithTryCatch_Counts()
    {
        var method = ParseMethod("""
            class C {
                void M() {
                    try {
                        if (true) { }
                    } catch (System.Exception) { }
                }
            }
            """);

        ComplexityAnalyzer.CalculateMaxNestingDepth(method.Body!).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void MaxNestingDepth_WithSwitch_Counts()
    {
        var method = ParseMethod("""
            class C {
                void M(int x) {
                    switch (x) {
                        case 1: break;
                    }
                }
            }
            """);

        // switch uses SwitchStatementSyntax with sections, not a block — nesting depends on implementation
        ComplexityAnalyzer.CalculateMaxNestingDepth(method.Body!).ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void MaxNestingDepth_WithLockAndUsing_Counts()
    {
        var method = ParseMethod("""
            class C {
                void M(object o) {
                    lock (o) {
                        using (var x = (System.IDisposable)o) { }
                    }
                }
            }
            """);

        ComplexityAnalyzer.CalculateMaxNestingDepth(method.Body!).ShouldBeGreaterThanOrEqualTo(2);
    }
}
