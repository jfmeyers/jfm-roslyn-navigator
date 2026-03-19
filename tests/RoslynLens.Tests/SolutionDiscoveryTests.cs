using Shouldly;

namespace RoslynLens.Tests;

public class SolutionDiscoveryTests
{
    [Fact]
    public void FindSolutionPath_With_Explicit_Arg_Returns_Path()
    {
        // Create a temp directory with a solution file
        var tempDir = Path.Combine(Path.GetTempPath(), $"dd-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var slnPath = Path.Combine(tempDir, "Test.slnx");
        File.WriteAllText(slnPath, "<Solution />");

        try
        {
            var result = SolutionDiscovery.FindSolutionPath(["--solution", slnPath]);
            result.ShouldNotBeNull();
            result.ShouldEndWith("Test.slnx");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BfsDiscovery_Finds_Slnx_In_Current_Dir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dd-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "MyApp.slnx"), "<Solution />");

        try
        {
            var result = SolutionDiscovery.BfsDiscovery(tempDir);
            result.ShouldNotBeNull();
            result.ShouldEndWith("MyApp.slnx");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BfsDiscovery_Returns_Null_When_No_Solution()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dd-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = SolutionDiscovery.BfsDiscovery(tempDir);
            result.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BfsDiscovery_Finds_Sln_In_Subdirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dd-test-{Guid.NewGuid():N}");
        var subDir = Path.Combine(tempDir, "src");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "App.sln"), "");

        try
        {
            var result = SolutionDiscovery.BfsDiscovery(tempDir);
            result.ShouldNotBeNull();
            result.ShouldEndWith("App.sln");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BfsDiscovery_Prefers_Shallower_Depth()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dd-test-{Guid.NewGuid():N}");
        var subDir = Path.Combine(tempDir, "src");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(tempDir, "Root.sln"), "");
        File.WriteAllText(Path.Combine(subDir, "Nested.sln"), "");

        try
        {
            var result = SolutionDiscovery.BfsDiscovery(tempDir);
            result.ShouldNotBeNull();
            result.ShouldEndWith("Root.sln");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BfsDiscovery_Skips_Known_Directories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dd-test-{Guid.NewGuid():N}");
        var binDir = Path.Combine(tempDir, "bin");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "Hidden.sln"), "");

        try
        {
            var result = SolutionDiscovery.BfsDiscovery(tempDir);
            result.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindSolutionPath_Without_Explicit_Arg_Uses_Bfs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dd-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var cwd = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDir);
            var result = SolutionDiscovery.FindSolutionPath([]);
            result.ShouldBeNull();
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
            Directory.Delete(tempDir, true);
        }
    }
}
