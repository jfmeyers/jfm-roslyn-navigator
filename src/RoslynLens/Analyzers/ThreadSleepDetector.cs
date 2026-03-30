namespace RoslynLens.Analyzers;

/// <summary>
/// GR-SLEEP: Detects Thread.Sleep() calls.
/// Thread.Sleep blocks the thread; prefer async alternatives.
/// </summary>
public sealed class ThreadSleepDetector : InvocationDetectorBase
{
    protected override IReadOnlyList<string> TargetExpressions { get; } =
        ["Thread.Sleep", "System.Threading.Thread.Sleep"];

    protected override string Id => "GR-SLEEP";
    protected override string Message => "Thread.Sleep() blocks the current thread";
    protected override string Suggestion => "Use Task.Delay() or TimeProvider";
}
