namespace RoslynLens.Analyzers;

/// <summary>
/// GR-GUID: Detects Guid.NewGuid() calls.
/// Granit uses IGuidGenerator.Create() for sequential GUIDs optimized for clustered indexes.
/// </summary>
public sealed class GuidNewGuidDetector : InvocationDetectorBase
{
    protected override IReadOnlyList<string> TargetExpressions { get; } =
        ["Guid.NewGuid", "System.Guid.NewGuid"];

    protected override string Id => "GR-GUID";
    protected override string Message => "Guid.NewGuid() produces random GUIDs — causes index fragmentation";
    protected override string Suggestion => "Use IGuidGenerator.Create() for sequential GUIDs";
}
