using System.ComponentModel;
using RoslynLens.Responses;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModelContextProtocol.Server;

namespace RoslynLens;

[McpServerToolType]
public static class AnalyzeControlFlowTool
{
    [McpServerTool(Name = "analyze_control_flow")]
    [Description("Analyze control flow within a method: reachability, return points, and exit points.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Name of the method to analyze")] string methodName,
        [Description("Optional containing class name")] string? className = null,
        CancellationToken ct = default)
    {
        var status = workspace.EnsureReadyOrStatus(ct);
        if (status is not null) return status;

        var (model, body, _) = await SymbolResolver.ResolveMethodBodyAsync(workspace, methodName, className, ct);
        if (model is null || body is null)
            return Json.Serialize(new { error = $"Method '{methodName}' not found or has no body" });

        if (body is not BlockSyntax block || block.Statements.Count == 0)
            return Json.Serialize(new { error = "Control flow analysis requires a block body with statements" });

        var analysis = model.AnalyzeControlFlow(block.Statements.First(), block.Statements.Last());
        if (analysis is null || !analysis.Succeeded)
            return Json.Serialize(new { error = "Control flow analysis failed" });

        var result = new ControlFlowResult(
            methodName,
            analysis.StartPointIsReachable,
            analysis.EndPointIsReachable,
            analysis.ReturnStatements.Length,
            analysis.ExitPoints.Length);

        return Json.Serialize(result);
    }
}
