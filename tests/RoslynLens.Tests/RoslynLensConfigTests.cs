using Microsoft.Extensions.Logging;
using Shouldly;

namespace RoslynLens.Tests;

public class RoslynLensConfigTests
{
    [Fact]
    public void FromEnvironment_ReturnsDefaults_WhenNoEnvVars()
    {
        Environment.SetEnvironmentVariable("ROSLYN_LENS_TIMEOUT_SECONDS", null);
        Environment.SetEnvironmentVariable("ROSLYN_LENS_MAX_RESULTS", null);
        Environment.SetEnvironmentVariable("ROSLYN_LENS_CACHE_SIZE", null);
        Environment.SetEnvironmentVariable("ROSLYN_LENS_LOG_LEVEL", null);

        var config = RoslynLensConfig.FromEnvironment();

        config.TimeoutSeconds.ShouldBe(30);
        config.MaxResults.ShouldBe(100);
        config.CacheSize.ShouldBe(50);
        config.LogLevel.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public void FromEnvironment_ReadsEnvVars()
    {
        Environment.SetEnvironmentVariable("ROSLYN_LENS_TIMEOUT_SECONDS", "60");
        Environment.SetEnvironmentVariable("ROSLYN_LENS_MAX_RESULTS", "200");
        Environment.SetEnvironmentVariable("ROSLYN_LENS_CACHE_SIZE", "25");
        Environment.SetEnvironmentVariable("ROSLYN_LENS_LOG_LEVEL", "Warning");

        try
        {
            var config = RoslynLensConfig.FromEnvironment();

            config.TimeoutSeconds.ShouldBe(60);
            config.MaxResults.ShouldBe(200);
            config.CacheSize.ShouldBe(25);
            config.LogLevel.ShouldBe(LogLevel.Warning);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROSLYN_LENS_TIMEOUT_SECONDS", null);
            Environment.SetEnvironmentVariable("ROSLYN_LENS_MAX_RESULTS", null);
            Environment.SetEnvironmentVariable("ROSLYN_LENS_CACHE_SIZE", null);
            Environment.SetEnvironmentVariable("ROSLYN_LENS_LOG_LEVEL", null);
        }
    }

    [Fact]
    public void FromEnvironment_FallsBackToDefaults_WhenInvalid()
    {
        Environment.SetEnvironmentVariable("ROSLYN_LENS_TIMEOUT_SECONDS", "not_a_number");
        Environment.SetEnvironmentVariable("ROSLYN_LENS_MAX_RESULTS", "-5");
        Environment.SetEnvironmentVariable("ROSLYN_LENS_LOG_LEVEL", "InvalidLevel");

        try
        {
            var config = RoslynLensConfig.FromEnvironment();

            config.TimeoutSeconds.ShouldBe(30);
            config.MaxResults.ShouldBe(100);
            config.LogLevel.ShouldBe(LogLevel.Information);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROSLYN_LENS_TIMEOUT_SECONDS", null);
            Environment.SetEnvironmentVariable("ROSLYN_LENS_MAX_RESULTS", null);
            Environment.SetEnvironmentVariable("ROSLYN_LENS_LOG_LEVEL", null);
        }
    }
}
