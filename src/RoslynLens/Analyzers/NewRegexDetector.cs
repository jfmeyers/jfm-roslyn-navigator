namespace RoslynLens.Analyzers;

/// <summary>
/// GR-REGEX: Detects new Regex() instantiation.
/// Granit convention requires [GeneratedRegex] for compile-time source generation.
/// </summary>
public sealed class NewRegexDetector : ObjectCreationDetectorBase
{
    protected override IReadOnlyList<string> TargetTypeNames { get; } =
        ["Regex", "System.Text.RegularExpressions.Regex"];

    protected override string Id => "GR-REGEX";
    protected override string Message => "new Regex() uses runtime compilation — slower and not AOT-friendly";
    protected override string Suggestion => "Use [GeneratedRegex] attribute instead";
}
