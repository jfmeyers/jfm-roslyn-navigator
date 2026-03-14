namespace JFM.RoslynNavigator.Analyzers;

public record AntiPatternViolation(
    string Id,
    AntiPatternSeverity Severity,
    string Message,
    string? File,
    int? Line,
    string? Suggestion);
