namespace JFM.RoslynNavigator;

/// <summary>
/// Represents the current state of the Roslyn workspace.
/// </summary>
public enum WorkspaceState
{
    NotStarted,
    Loading,
    Ready,
    Error
}
