using RoslynLens.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace RoslynLens.Tests.Analyzers;

public class LoggingInterpolationDetectorTests
{
    private readonly LoggingInterpolationDetector _detector = new();

    [Fact]
    public void Detects_Interpolation_In_LogInformation()
    {
        const string source = """
            public class Service
            {
                public void DoWork(object logger, string name)
                {
                    logger.LogInformation($"User {name} logged in");
                }
            }
            public static class LoggerExtensions
            {
                public static void LogInformation(this object logger, string message) { }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP006");
    }

    [Fact]
    public void Ignores_Template_Based_Logging()
    {
        const string source = """
            public class Service
            {
                public void DoWork(object logger, string name)
                {
                    logger.LogInformation("User {Name} logged in", name);
                }
            }
            public static class LoggerExtensions
            {
                public static void LogInformation(this object logger, string message, params object[] args) { }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Detects_Interpolation_In_LogError()
    {
        const string source = """
            public class Service
            {
                public void DoWork(object logger, int id)
                {
                    logger.LogError($"Failed to process {id}");
                }
            }
            public static class LoggerExtensions
            {
                public static void LogError(this object logger, string message) { }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldContain(v => v.Id == "AP006");
    }

    [Fact]
    public void Ignores_Non_Log_Methods()
    {
        const string source = """
            public class Service
            {
                public void DoWork(string name)
                {
                    Console.WriteLine($"User {name} logged in");
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var violations = _detector.Detect(tree, null, TestContext.Current.CancellationToken).ToList();
        violations.ShouldBeEmpty();
    }
}
