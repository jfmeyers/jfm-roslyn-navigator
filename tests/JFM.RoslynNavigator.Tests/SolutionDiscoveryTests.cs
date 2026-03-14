using Shouldly;

namespace JFM.RoslynNavigator.Tests;

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
}
